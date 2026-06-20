using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class FinancialGovernanceServiceTests
{
    [Fact]
    public async Task CreateFinancialDisputeAsync_CreatesFormalDisputeAndAuditEntry()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Dispute Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 125_000m);
        var userId = Guid.NewGuid();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateFinancialDisputeAsync(
            userId,
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = bill.Id,
                Reason = "Meter reading mismatch",
                Message = "The bill does not match the meter photo."
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(FinancialDisputeStatus.Open);
        result.Value.TargetReference.Should().Be(bill.BillNumber);
        dbContext.FinancialDisputes.Should().ContainSingle(dispute =>
            dispute.TargetType == FinancialDisputeTargetType.UtilityBill
            && dispute.TargetId == bill.Id
            && dispute.ResidentProfileId == resident.Id);
        dbContext.AuditLogEntries.Should().ContainSingle(audit =>
            audit.ActionType == AuditActionType.FinancialDisputeOpened
            && audit.EntityType == AuditEntityType.FinancialDispute
            && audit.ActorUserId == userId);
    }

    [Fact]
    public async Task CreateFinancialDisputeAsync_RejectsDuplicateActiveDisputeForSameTarget()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-2");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Duplicate Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 80_000m);
        var service = CreateService(dbContext, compound.Id);
        var request = new CreateFinancialDisputeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = bill.Id,
            Reason = "Duplicate check",
            Message = "First dispute."
        };

        var first = await service.CreateFinancialDisputeAsync(Guid.NewGuid(), request);
        first.IsSuccess.Should().BeTrue(first.Message);

        var second = await service.CreateFinancialDisputeAsync(Guid.NewGuid(), request);

        second.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.FinancialDisputes.Should().ContainSingle();
    }

    [Fact]
    public async Task TransitionFinancialDisputeAsync_MovesOpenDisputeToAccepted()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-3");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Transition Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 90_000m);
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateFinancialDisputeAsync(
            Guid.NewGuid(),
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = bill.Id,
                Reason = "Wrong total",
                Message = "Please review."
            });

        var result = await service.TransitionFinancialDisputeAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new TransitionFinancialDisputeRequest
            {
                Transition = FinancialDisputeTransition.Accept,
                Notes = "Resident evidence accepted.",
                ResolutionSummary = "Billing team will issue an adjustment."
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(FinancialDisputeStatus.Accepted);
        result.Value.ReviewedByUserId.Should().NotBeNull();
        dbContext.AuditLogEntries.Should().Contain(audit => audit.ActionType == AuditActionType.FinancialDisputeStatusChanged);
    }

    [Fact]
    public async Task CreateResidentFinancialDisputeAsync_RejectsTargetOwnedByAnotherResident()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-4");
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = await AddResidentAsync(dbContext, compound.Id, "Owner Resident", ownerUserId);
        await AddResidentAsync(dbContext, compound.Id, "Other Resident", otherUserId);
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, owner.Id, 50_000m);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateResidentFinancialDisputeAsync(
            otherUserId,
            new CreateResidentFinancialDisputeRequest
            {
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = bill.Id,
                Reason = "Should not see",
                Message = "This is not mine."
            });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task CreateViolationAppealAsync_CreatesAppealAndCanCancelLinkedFine()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-5");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Appeal Resident");
        var violation = await AddViolationAsync(dbContext, compound.Id, resident.Id);
        var fine = await AddViolationFineAsync(dbContext, compound.Id, resident.Id, violation.Id, 25_000m);
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateViolationAppealAsync(
            Guid.NewGuid(),
            new CreateViolationAppealRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                ViolationId = violation.Id,
                ViolationFineId = fine.Id,
                Reason = "Fine is unfair",
                Message = "Please cancel this fine."
            });

        created.IsSuccess.Should().BeTrue(created.Message);

        var result = await service.TransitionViolationAppealAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new TransitionViolationAppealRequest
            {
                Transition = ViolationAppealTransition.CancelFine,
                Notes = "Fine cancelled after review."
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ViolationAppealStatus.FineCancelled);
        dbContext.ViolationFines.Single().Status.Should().Be(ViolationFineStatus.Cancelled);
        dbContext.AuditLogEntries.Should().Contain(audit => audit.ActionType == AuditActionType.ViolationAppealStatusChanged);
    }

    [Fact]
    public async Task CreateAdjustmentForFinancialDisputeAsync_LinksPendingAdjustmentToAcceptedDispute()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-6");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Adjustment Dispute Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 100_000m);
        var userId = Guid.NewGuid();
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateFinancialDisputeAsync(
            userId,
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = bill.Id,
                Reason = "Wrong amount",
                Message = "Please correct this bill."
            });
        await service.TransitionFinancialDisputeAsync(
            userId,
            created.Value!.Id,
            new TransitionFinancialDisputeRequest
            {
                Transition = FinancialDisputeTransition.Accept,
                Notes = "Accepted.",
                ResolutionSummary = "Credit adjustment should be requested."
            });

        var result = await service.CreateAdjustmentForFinancialDisputeAsync(
            userId,
            created.Value.Id,
            new CreateGovernanceFinancialAdjustmentRequest
            {
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 15_000m,
                Currency = "IQD",
                Reason = "Bill overcharge correction"
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.FinancialAdjustmentId.Should().NotBeNull();
        dbContext.FinancialAdjustments.Should().ContainSingle(adjustment =>
            adjustment.Id == result.Value.FinancialAdjustmentId
            && adjustment.Status == FinancialAdjustmentStatus.PendingApproval
            && adjustment.AdjustmentType == FinancialAdjustmentType.Credit
            && adjustment.Amount == 15_000m);
        dbContext.ApprovalRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAdjustmentForViolationAppealAsync_LinksCreditAdjustmentToReducedFineAppeal()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-7");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Adjustment Appeal Resident");
        var violation = await AddViolationAsync(dbContext, compound.Id, resident.Id);
        var fine = await AddViolationFineAsync(dbContext, compound.Id, resident.Id, violation.Id, 50_000m);
        var userId = Guid.NewGuid();
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateViolationAppealAsync(
            userId,
            new CreateViolationAppealRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                ViolationId = violation.Id,
                ViolationFineId = fine.Id,
                Reason = "Reduce fine",
                Message = "Fine is too high."
            });
        await service.TransitionViolationAppealAsync(
            userId,
            created.Value!.Id,
            new TransitionViolationAppealRequest
            {
                Transition = ViolationAppealTransition.ReduceFine,
                Notes = "Reduced after review.",
                ReducedFineAmount = 30_000m
            });

        var result = await service.CreateAdjustmentForViolationAppealAsync(
            userId,
            created.Value.Id,
            new CreateGovernanceFinancialAdjustmentRequest
            {
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 20_000m,
                Currency = "IQD",
                Reason = "Fine reduction credit"
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.FinancialAdjustmentId.Should().NotBeNull();
        dbContext.FinancialAdjustments.Should().ContainSingle(adjustment =>
            adjustment.Id == result.Value.FinancialAdjustmentId
            && adjustment.Status == FinancialAdjustmentStatus.PendingApproval
            && adjustment.Amount == 20_000m);
    }

    [Fact]
    public async Task SearchResidentFinancialDisputesAsync_ReturnsOnlyCurrentResidentDisputes()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-8");
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = await AddResidentAsync(dbContext, compound.Id, "Owner Search Resident", ownerUserId);
        var other = await AddResidentAsync(dbContext, compound.Id, "Other Search Resident", otherUserId);
        var ownerBill = await AddUtilityBillAsync(dbContext, compound.Id, owner.Id, 70_000m);
        var otherBill = await AddUtilityBillAsync(dbContext, compound.Id, other.Id, 90_000m);
        var service = CreateService(dbContext, compound.Id);

        await service.CreateFinancialDisputeAsync(
            Guid.NewGuid(),
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = owner.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = ownerBill.Id,
                Reason = "Owner dispute",
                Message = "Owner can see this."
            });
        await service.CreateFinancialDisputeAsync(
            Guid.NewGuid(),
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = other.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = otherBill.Id,
                Reason = "Other dispute",
                Message = "Owner must not see this."
            });

        var result = await service.SearchResidentFinancialDisputesAsync(
            ownerUserId,
            new FinancialDisputeSearchQuery { PageNumber = 1, PageSize = 10 });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle(item => item.ResidentProfileId == owner.Id);
    }

    [Fact]
    public async Task SearchResidentViolationAppealsAsync_ReturnsOnlyCurrentResidentAppeals()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-9");
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = await AddResidentAsync(dbContext, compound.Id, "Owner Appeal Search Resident", ownerUserId);
        var other = await AddResidentAsync(dbContext, compound.Id, "Other Appeal Search Resident", otherUserId);
        var ownerViolation = await AddViolationAsync(dbContext, compound.Id, owner.Id);
        var otherViolation = await AddViolationAsync(dbContext, compound.Id, other.Id);
        var service = CreateService(dbContext, compound.Id);

        await service.CreateViolationAppealAsync(
            Guid.NewGuid(),
            new CreateViolationAppealRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = owner.Id,
                ViolationId = ownerViolation.Id,
                Reason = "Owner appeal",
                Message = "Owner can see this."
            });
        await service.CreateViolationAppealAsync(
            Guid.NewGuid(),
            new CreateViolationAppealRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = other.Id,
                ViolationId = otherViolation.Id,
                Reason = "Other appeal",
                Message = "Owner must not see this."
            });

        var result = await service.SearchResidentViolationAppealsAsync(
            ownerUserId,
            new ViolationAppealSearchQuery { PageNumber = 1, PageSize = 10 });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle(item => item.ResidentProfileId == owner.Id);
    }

    [Fact]
    public async Task GetResidentFinancialGovernanceSummaryAsync_ReturnsCurrentResidentOpenReviewCounts()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-10");
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = await AddResidentAsync(dbContext, compound.Id, "Owner Summary Resident", ownerUserId);
        var other = await AddResidentAsync(dbContext, compound.Id, "Other Summary Resident", otherUserId);
        var ownerBill = await AddUtilityBillAsync(dbContext, compound.Id, owner.Id, 65_000m);
        var otherBill = await AddUtilityBillAsync(dbContext, compound.Id, other.Id, 95_000m);
        var ownerViolation = await AddViolationAsync(dbContext, compound.Id, owner.Id);
        var service = CreateService(dbContext, compound.Id);

        await service.CreateFinancialDisputeAsync(
            Guid.NewGuid(),
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = owner.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = ownerBill.Id,
                Reason = "Owner summary dispute",
                Message = "Open owner dispute."
            });
        await service.CreateFinancialDisputeAsync(
            Guid.NewGuid(),
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = other.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = otherBill.Id,
                Reason = "Other summary dispute",
                Message = "Should not count for owner."
            });
        await service.CreateViolationAppealAsync(
            Guid.NewGuid(),
            new CreateViolationAppealRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = owner.Id,
                ViolationId = ownerViolation.Id,
                Reason = "Owner summary appeal",
                Message = "Open owner appeal."
            });

        var result = await service.GetResidentFinancialGovernanceSummaryAsync(ownerUserId);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OpenFinancialDisputeCount.Should().Be(1);
        result.Value.ActiveFinancialDisputeCount.Should().Be(1);
        result.Value.SubmittedViolationAppealCount.Should().Be(1);
        result.Value.ActiveViolationAppealCount.Should().Be(1);
        result.Value.FinancialReviewItemCount.Should().Be(2);
        result.Value.LinkedFinancialAdjustmentCount.Should().Be(0);
    }


    [Fact]
    public async Task GetAdminFinancialGovernanceSummaryAsync_ReturnsCompoundScopedReviewCounts()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "FG-11");
        var blockedCompound = await AddCompoundAsync(dbContext, "FG-12");
        var allowedResident = await AddResidentAsync(dbContext, allowedCompound.Id, "Allowed Admin Summary Resident");
        var blockedResident = await AddResidentAsync(dbContext, blockedCompound.Id, "Blocked Admin Summary Resident");
        var allowedBill = await AddUtilityBillAsync(dbContext, allowedCompound.Id, allowedResident.Id, 110_000m);
        var blockedBill = await AddUtilityBillAsync(dbContext, blockedCompound.Id, blockedResident.Id, 210_000m);
        var allowedViolation = await AddViolationAsync(dbContext, allowedCompound.Id, allowedResident.Id);
        var seeder = CreateService(dbContext, allowedCompound.Id, blockedCompound.Id);
        var userId = Guid.NewGuid();

        var dispute = await seeder.CreateFinancialDisputeAsync(
            userId,
            new CreateFinancialDisputeRequest
            {
                CompoundId = allowedCompound.Id,
                ResidentProfileId = allowedResident.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = allowedBill.Id,
                Reason = "Allowed dispute",
                Message = "Should be counted."
            });
        await seeder.TransitionFinancialDisputeAsync(
            userId,
            dispute.Value!.Id,
            new TransitionFinancialDisputeRequest
            {
                Transition = FinancialDisputeTransition.Accept,
                Notes = "Accepted for summary.",
                ResolutionSummary = "Create adjustment."
            });
        await seeder.CreateAdjustmentForFinancialDisputeAsync(
            userId,
            dispute.Value.Id,
            new CreateGovernanceFinancialAdjustmentRequest
            {
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 10_000m,
                Currency = "IQD",
                Reason = "Summary linked adjustment"
            });
        await seeder.CreateFinancialDisputeAsync(
            userId,
            new CreateFinancialDisputeRequest
            {
                CompoundId = blockedCompound.Id,
                ResidentProfileId = blockedResident.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = blockedBill.Id,
                Reason = "Blocked dispute",
                Message = "Should not be counted."
            });
        await seeder.CreateViolationAppealAsync(
            userId,
            new CreateViolationAppealRequest
            {
                CompoundId = allowedCompound.Id,
                ResidentProfileId = allowedResident.Id,
                ViolationId = allowedViolation.Id,
                Reason = "Allowed appeal",
                Message = "Should be counted."
            });
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetAdminFinancialGovernanceSummaryAsync(new FinancialGovernanceSummaryQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalFinancialDisputeCount.Should().Be(1);
        result.Value.AcceptedFinancialDisputeCount.Should().Be(1);
        result.Value.TotalViolationAppealCount.Should().Be(1);
        result.Value.SubmittedViolationAppealCount.Should().Be(1);
        result.Value.LinkedFinancialAdjustmentCount.Should().Be(1);
        result.Value.PendingLinkedFinancialAdjustmentCount.Should().Be(1);
        result.Value.FinancialReviewItemCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAdminResidentFinancialGovernanceSnapshotAsync_ReturnsResidentScopedCounts()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FG-13");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Snapshot Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 75_000m);
        var violation = await AddViolationAsync(dbContext, compound.Id, resident.Id);
        var service = CreateService(dbContext, compound.Id);
        var userId = Guid.NewGuid();

        await service.CreateFinancialDisputeAsync(
            userId,
            new CreateFinancialDisputeRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                TargetType = FinancialDisputeTargetType.UtilityBill,
                TargetId = bill.Id,
                Reason = "Snapshot dispute",
                Message = "Open dispute."
            });
        await service.CreateViolationAppealAsync(
            userId,
            new CreateViolationAppealRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                ViolationId = violation.Id,
                Reason = "Snapshot appeal",
                Message = "Open appeal."
            });

        var result = await service.GetAdminResidentFinancialGovernanceSnapshotAsync(resident.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ResidentProfileId.Should().Be(resident.Id);
        result.Value.ResidentName.Should().Be("Snapshot Resident");
        result.Value.Summary.ActiveFinancialDisputeCount.Should().Be(1);
        result.Value.Summary.ActiveViolationAppealCount.Should().Be(1);
        result.Value.Summary.FinancialReviewItemCount.Should().Be(2);
        result.Value.LatestFinancialDisputeCreatedAtUtc.Should().NotBeNull();
        result.Value.LatestViolationAppealCreatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAdminResidentFinancialGovernanceSnapshotAsync_RejectsResidentOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "FG-14");
        var blockedCompound = await AddCompoundAsync(dbContext, "FG-15");
        var blockedResident = await AddResidentAsync(dbContext, blockedCompound.Id, "Blocked Snapshot Resident");
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetAdminResidentFinancialGovernanceSnapshotAsync(blockedResident.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static FinancialGovernanceService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        var compoundAccessService = new FakeCompoundAccessService(allowedCompoundIds);
        var auditLogService = new AuditLogService(dbContext, compoundAccessService, new HttpContextAccessor());
        var financialControlService = new FinancialControlService(dbContext, compoundAccessService, auditLogService);

        return new FinancialGovernanceService(
            dbContext,
            compoundAccessService,
            auditLogService,
            financialControlService);
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada"
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string fullName,
        Guid? userId = null)
    {
        var user = new ApplicationUser
        {
            Id = userId ?? Guid.NewGuid(),
            UserName = $"{Guid.NewGuid():N}@test.local",
            Email = $"{Guid.NewGuid():N}@test.local",
            FullName = fullName
        };
        var resident = new ResidentProfile
        {
            UserId = user.Id,
            CompoundId = compoundId,
            FullName = fullName,
            PhoneNumber = "+9647700000000",
            IsActive = true
        };

        dbContext.Users.Add(user);
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<UtilityBill> AddUtilityBillAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        decimal totalAmount)
    {
        var bill = new UtilityBill
        {
            CompoundId = compoundId,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = residentProfileId,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = $"BILL-{Guid.NewGuid():N}",
            BillStatus = BillStatus.Unpaid,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
            TotalAmount = totalAmount,
            PaidAmount = 0m
        };
        dbContext.UtilityBills.Add(bill);
        await dbContext.SaveChangesAsync();
        return bill;
    }

    private static async Task<Violation> AddViolationAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId)
    {
        var violation = new Violation
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            ViolationType = ViolationType.NoiseAfterHours,
            Title = "Noise violation",
            Description = "Repeated noise after quiet hours.",
            CreatedByUserId = Guid.NewGuid()
        };
        dbContext.Violations.Add(violation);
        await dbContext.SaveChangesAsync();
        return violation;
    }

    private static async Task<ViolationFine> AddViolationFineAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        Guid violationId,
        decimal amount)
    {
        var fine = new ViolationFine
        {
            ViolationId = violationId,
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            Amount = amount,
            PaidAmount = 0m,
            Status = ViolationFineStatus.Unpaid,
            Reason = "Noise fine",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10))
        };
        dbContext.ViolationFines.Add(fine);
        await dbContext.SaveChangesAsync();
        return fine;
    }
}
