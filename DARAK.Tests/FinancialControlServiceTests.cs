using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class FinancialControlServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ReturnsOutstandingRevenueAndAdjustmentCounts()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-D1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Resident One");
        await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 1000m, 250m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5));
        await AddRentInvoiceAsync(dbContext, compound.Id, resident.Id, 2000m, 500m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40));
        dbContext.Payments.Add(new Payment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 250m,
            Currency = "IQD",
            PaymentReference = "PAY-DASH-1",
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        });
        dbContext.FinancialAdjustments.Add(new FinancialAdjustment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AdjustmentType = FinancialAdjustmentType.Credit,
            Status = FinancialAdjustmentStatus.PendingApproval,
            Amount = 100m,
            Currency = "IQD",
            Reason = "Pending correction",
            RequestedByUserId = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetDashboardAsync(new FinancialDashboardQuery
        {
            CompoundId = compound.Id,
            FromDate = today.AddDays(-10),
            ToDate = today
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ActiveResidentCount.Should().Be(1);
        result.Value.TotalOutstandingAmount.Should().Be(2250m);
        result.Value.TotalOverdueAmount.Should().Be(2250m);
        result.Value.NetCollectedAmount.Should().Be(250m);
        result.Value.PendingAdjustmentCount.Should().Be(1);
    }

    [Fact]
    public async Task GetResidentStatementAsync_CombinesChargesPaymentsRefundsAndAdjustments()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-S1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Statement Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 1000m, 1000m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7));
        dbContext.Payments.Add(new Payment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = bill.Id,
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 700m,
            Currency = "IQD",
            PaymentReference = "PAY-STAT-1",
            CompletedAt = DateTime.UtcNow.AddDays(-2)
        });
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Direction = FinancialLedgerEntryDirection.Credit,
            SourceType = FinancialLedgerSourceType.FinancialAdjustment,
            SourceId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "IQD",
            Reference = "ADJ-STAT-1",
            Description = "Goodwill credit",
            OccurredAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetResidentStatementAsync(resident.Id, new ResidentStatementQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalDebits.Should().Be(1000m);
        result.Value.TotalCredits.Should().Be(800m);
        result.Value.ClosingBalance.Should().Be(200m);
        result.Value.Lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetResidentStatementAsync_FlagsFinancialDisputesAndViolationAppealsOnStatementLines()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-GOV-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Governance Statement Resident");
        var bill = await AddUtilityBillAsync(
            dbContext,
            compound.Id,
            resident.Id,
            1200m,
            0m,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7));
        var violation = new Violation
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            ViolationType = ViolationType.NoiseAfterHours,
            Title = "Noise violation",
            Description = "Repeated noise after quiet hours.",
            CreatedByUserId = Guid.NewGuid()
        };
        var fine = new ViolationFine
        {
            ViolationId = violation.Id,
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Amount = 250m,
            PaidAmount = 0m,
            Status = ViolationFineStatus.Unpaid,
            Reason = "Noise fine",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)
        };
        var dispute = new FinancialDispute
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = bill.Id,
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "Incorrect bill",
            ResidentMessage = "Please review this bill.",
            CreatedByUserId = Guid.NewGuid()
        };
        var appeal = new ViolationAppeal
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            ViolationId = violation.Id,
            ViolationFineId = fine.Id,
            Status = ViolationAppealStatus.Submitted,
            Reason = "Fine appeal",
            ResidentMessage = "The fine is unfair.",
            CreatedByUserId = Guid.NewGuid()
        };

        dbContext.Violations.Add(violation);
        dbContext.ViolationFines.Add(fine);
        dbContext.FinancialDisputes.Add(dispute);
        dbContext.ViolationAppeals.Add(appeal);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetResidentStatementAsync(resident.Id, new ResidentStatementQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.FinancialReviewLineCount.Should().Be(2);
        result.Value.Lines.Should().Contain(line =>
            line.SourceType == FinancialLedgerSourceType.UtilityBill
            && line.SourceId == bill.Id
            && line.IsUnderFinancialReview
            && line.FinancialDisputeId == dispute.Id
            && line.FinancialDisputeStatus == FinancialDisputeStatus.UnderReview);
        result.Value.Lines.Should().Contain(line =>
            line.SourceType == FinancialLedgerSourceType.ViolationFine
            && line.SourceId == fine.Id
            && line.IsUnderFinancialReview
            && line.ViolationAppealId == appeal.Id
            && line.ViolationAppealStatus == ViolationAppealStatus.Submitted);
    }

    [Fact]
    public async Task GetResidentStatementAsync_ExposesAppliedFinancialAdjustmentMetadata()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-GOV-2");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Applied Adjustment Statement Resident");
        var adjustment = new FinancialAdjustment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AdjustmentType = FinancialAdjustmentType.Credit,
            Status = FinancialAdjustmentStatus.Applied,
            Amount = 300m,
            Currency = "IQD",
            Reason = "Accepted dispute credit",
            RequestedByUserId = Guid.NewGuid(),
            AppliedByUserId = Guid.NewGuid(),
            AppliedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.FinancialAdjustments.Add(adjustment);
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            FinancialAdjustmentId = adjustment.Id,
            Direction = FinancialLedgerEntryDirection.Credit,
            SourceType = FinancialLedgerSourceType.FinancialAdjustment,
            SourceId = adjustment.Id,
            Amount = adjustment.Amount,
            Currency = adjustment.Currency,
            Reference = "ADJ-GOV-1",
            Description = adjustment.Reason,
            OccurredAtUtc = adjustment.AppliedAtUtc.Value
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetResidentStatementAsync(resident.Id, new ResidentStatementQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.AppliedCreditAdjustmentAmount.Should().Be(300m);
        result.Value.AppliedDebitAdjustmentAmount.Should().Be(0m);
        result.Value.Lines.Should().ContainSingle(line =>
            line.SourceType == FinancialLedgerSourceType.FinancialAdjustment
            && line.SourceId == adjustment.Id
            && line.FinancialAdjustmentId == adjustment.Id
            && line.FinancialAdjustmentStatus == FinancialAdjustmentStatus.Applied
            && line.CreditAmount == 300m);
    }

    [Fact]
    public async Task GetResidentStatementAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "F16-A");
        var blockedCompound = await AddCompoundAsync(dbContext, "F16-B");
        var resident = await AddResidentAsync(dbContext, blockedCompound.Id, "Blocked Resident");
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetResidentStatementAsync(resident.Id, new ResidentStatementQuery());

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GetResidentStatementForUserAsync_ReturnsStatementForSingleActiveProfile()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-RS1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Resident Statement User");
        await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 500m, 0m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetResidentStatementForUserAsync(resident.UserId, new ResidentStatementQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ResidentProfileId.Should().Be(resident.Id);
        result.Value.TotalDebits.Should().Be(500m);
    }

    [Fact]
    public async Task GetResidentStatementForUserAsync_RequiresProfileIdWhenUserHasMultipleProfiles()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-RS2");
        var userId = Guid.NewGuid();
        var first = await AddResidentForUserAsync(dbContext, compound.Id, userId, "Resident Profile One");
        var second = await AddResidentForUserAsync(dbContext, compound.Id, userId, "Resident Profile Two");
        await AddUtilityBillAsync(dbContext, compound.Id, second.Id, 300m, 0m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));
        var service = CreateService(dbContext, compound.Id);

        var missingProfile = await service.GetResidentStatementForUserAsync(userId, new ResidentStatementQuery());
        var selectedProfile = await service.GetResidentStatementForUserAsync(
            userId,
            new ResidentStatementQuery { ResidentProfileId = second.Id });

        missingProfile.Status.Should().Be(ServiceResultStatus.BadRequest);
        missingProfile.Message.Should().Contain("residentProfileId");
        selectedProfile.Status.Should().Be(ServiceResultStatus.Success);
        selectedProfile.Value!.ResidentProfileId.Should().Be(second.Id);
        selectedProfile.Value.ResidentProfileId.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task GetResidentStatementForUserAsync_ReturnsNotFoundForAnotherUsersProfile()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-RS3");
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var first = await AddResidentForUserAsync(dbContext, compound.Id, firstUser, "Resident Profile Owner");
        var second = await AddResidentForUserAsync(dbContext, compound.Id, secondUser, "Other Resident Profile");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetResidentStatementForUserAsync(
            first.UserId,
            new ResidentStatementQuery { ResidentProfileId = second.Id });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_CreatesPendingApprovalActivityAndNotification()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-A1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Adjustment Resident");
        var userId = Guid.NewGuid();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateAdjustmentAsync(
            userId,
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 150m,
                Reason = "Billing correction"
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(FinancialAdjustmentStatus.PendingApproval);
        result.Value.ApprovalRequestId.Should().NotBeNull();
        dbContext.ApprovalRequests.Should().ContainSingle(approval =>
            approval.ActionType == ApprovalActionType.ManualFinancialCorrection
            && approval.EntityType == ApprovalEntityType.ResidentProfile
            && approval.EntityId == resident.Id);
        dbContext.ActivityEvents.Should().ContainSingle(activity => activity.EventType == ActivityEventType.FinancialAdjustmentRequested);
        dbContext.NotificationOutboxes.Should().ContainSingle(notification => notification.EventType == NotificationEventType.FinancialAdjustmentRequested);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_ReturnsNotFoundWhenResidentIsOutsideCompound()
    {
        await using var dbContext = TestDb.Create();
        var compoundA = await AddCompoundAsync(dbContext, "F16-A2");
        var compoundB = await AddCompoundAsync(dbContext, "F16-B2");
        var resident = await AddResidentAsync(dbContext, compoundB.Id, "Wrong Compound Resident");
        var service = CreateService(dbContext, compoundA.Id);

        var result = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = compoundA.Id,
                ResidentProfileId = resident.Id,
                AdjustmentType = FinancialAdjustmentType.Debit,
                Amount = 100m,
                Reason = "Should fail"
            });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        dbContext.FinancialAdjustments.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_RequiresApprovedApprovalRequest()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-AP1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Approval Required Resident");
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 300m,
                Reason = "Pending approval"
            });

        var result = await service.ApplyAdjustmentAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new ApplyFinancialAdjustmentRequest { Notes = "Try early" });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.ResidentLedgerEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_CreatesLedgerEntryAndMarksApprovalExecuted()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-AP2");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Apply Resident");
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                AdjustmentType = FinancialAdjustmentType.Debit,
                Amount = 450m,
                Reason = "Manual debit"
            });
        var approval = dbContext.ApprovalRequests.Single();
        approval.Status = ApprovalStatus.Approved;
        approval.ExecutionStatus = ApprovalExecutionStatus.ReadyForExecution;
        approval.DecidedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var result = await service.ApplyAdjustmentAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new ApplyFinancialAdjustmentRequest { Notes = "Approved correction applied" });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(FinancialAdjustmentStatus.Applied);
        dbContext.ResidentLedgerEntries.Should().ContainSingle(entry =>
            entry.FinancialAdjustmentId == created.Value.Id
            && entry.Direction == FinancialLedgerEntryDirection.Debit
            && entry.Amount == 450m);
        approval.ExecutionStatus.Should().Be(ApprovalExecutionStatus.Executed);
        dbContext.ActivityEvents.Should().Contain(activity => activity.EventType == ActivityEventType.FinancialAdjustmentApplied);
        dbContext.NotificationOutboxes.Should().Contain(notification => notification.EventType == NotificationEventType.FinancialAdjustmentApplied);
    }

    [Fact]
    public async Task CancelAdjustmentAsync_CancelsPendingApprovalRequest()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-C1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Cancel Resident");
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 80m,
                Reason = "Cancel me"
            });

        var result = await service.CancelAdjustmentAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new CancelFinancialAdjustmentRequest { Reason = "Mistaken correction" });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(FinancialAdjustmentStatus.Cancelled);
        dbContext.ApprovalRequests.Single().Status.Should().Be(ApprovalStatus.Cancelled);
        dbContext.ResidentLedgerEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAgingReportAsync_BucketsOutstandingAmounts()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-AGE");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Aging Resident");
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 100m, 0m, asOfDate.AddDays(5));
        await AddRentInvoiceAsync(dbContext, compound.Id, resident.Id, 200m, 0m, asOfDate.AddDays(-20));
        await AddInstallmentAsync(dbContext, compound.Id, resident.Id, 300m, 0m, asOfDate.AddDays(-70));
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetAgingReportAsync(new FinancialAgingReportQuery { CompoundId = compound.Id, AsOfDate = asOfDate });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.CurrentAmount.Should().Be(100m);
        result.Value.Days1To30Amount.Should().Be(200m);
        result.Value.Days61To90Amount.Should().Be(300m);
        result.Value.TotalOutstandingAmount.Should().Be(600m);
    }

    [Fact]
    public async Task GetAgingRiskReportAsync_FlagsDisputedOverdueItemsAndPenaltyPauseRecommendation()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-RISK");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Risk Resident");
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 1000m, 0m, asOfDate.AddDays(-25));
        dbContext.PenaltyRules.Add(new PenaltyRule
        {
            CompoundId = compound.Id,
            Name = "Utility penalty pause",
            TargetType = PenaltyRuleTargetType.UtilityBill,
            CalculationType = PenaltyCalculationType.FixedAmount,
            Status = PenaltyRuleStatus.Active,
            GracePeriodDays = 5,
            Amount = 50m,
            PauseWhenDisputed = true,
            EffectiveFrom = asOfDate.AddMonths(-1)
        });
        dbContext.FinancialDisputes.Add(new FinancialDispute
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = bill.Id,
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "Meter reading dispute",
            ResidentMessage = "This bill should be reviewed.",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetAgingRiskReportAsync(new FinancialAgingRiskReportQuery
        {
            CompoundId = compound.Id,
            AsOfDate = asOfDate
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ResidentCount.Should().Be(1);
        result.Value.HighRiskResidentCount.Should().Be(1);
        result.Value.TotalOutstandingAmount.Should().Be(1000m);
        result.Value.TotalOverdueAmount.Should().Be(1000m);
        result.Value.UnderFinancialReviewAmount.Should().Be(1000m);
        result.Value.PenaltyPauseRecommendedAmount.Should().Be(1000m);
        result.Value.Residents.Should().ContainSingle(residentRisk =>
            residentRisk.ResidentProfileId == resident.Id
            && residentRisk.ActiveFinancialDisputeCount == 1
            && residentRisk.PenaltyPauseRecommendedAmount == 1000m
            && residentRisk.Items.Single().PenaltyPauseRecommended);
    }

    [Fact]
    public async Task GetAgingRiskReportAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "F16-RISK-A");
        var blockedCompound = await AddCompoundAsync(dbContext, "F16-RISK-B");
        var resident = await AddResidentAsync(dbContext, blockedCompound.Id, "Blocked Risk Resident");
        await AddRentInvoiceAsync(dbContext, blockedCompound.Id, resident.Id, 900m, 0m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40));
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetAgingRiskReportAsync(new FinancialAgingRiskReportQuery { CompoundId = blockedCompound.Id });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GetRevenueSummaryAsync_GroupsByPaymentMethodAndTargetType()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-REV");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Revenue Resident");
        dbContext.Payments.AddRange(
            new Payment
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                TargetType = PaymentTargetType.UtilityBill,
                TargetId = Guid.NewGuid(),
                PaymentMethod = PaymentMethod.Cash,
                PaymentStatus = PaymentStatus.Succeeded,
                Amount = 100m,
                Currency = "IQD",
                PaymentReference = "PAY-REV-1",
                CompletedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Payment
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                TargetType = PaymentTargetType.RentInvoice,
                TargetId = Guid.NewGuid(),
                PaymentMethod = PaymentMethod.BankTransfer,
                PaymentStatus = PaymentStatus.Succeeded,
                Amount = 250m,
                Currency = "IQD",
                PaymentReference = "PAY-REV-2",
                CompletedAt = DateTime.UtcNow.AddDays(-1)
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetRevenueSummaryAsync(new RevenueSummaryQuery
        {
            CompoundId = compound.Id,
            FromDate = today.AddDays(-10),
            ToDate = today
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.CollectedAmount.Should().Be(350m);
        result.Value.ByPaymentMethod.Should().HaveCount(2);
        result.Value.ByTargetType.Should().Contain(item => item.TargetType == PaymentTargetType.UtilityBill && item.CollectedAmount == 100m);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_AppendsFinancialAndLedgerAuditEntries()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-AUD");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Audit Finance Resident");
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                AdjustmentType = FinancialAdjustmentType.Credit,
                Amount = 700m,
                Reason = "Audit approved credit"
            });
        var approval = dbContext.ApprovalRequests.Single();
        approval.Status = ApprovalStatus.Approved;
        approval.ExecutionStatus = ApprovalExecutionStatus.ReadyForExecution;
        await dbContext.SaveChangesAsync();

        var result = await service.ApplyAdjustmentAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new ApplyFinancialAdjustmentRequest { Notes = "Apply with commercial audit." });

        result.IsSuccess.Should().BeTrue(result.Message);
        dbContext.AuditLogEntries.Should().Contain(audit =>
            audit.ActionType == AuditActionType.FinancialAdjustmentApplied
            && audit.EntityType == AuditEntityType.FinancialAdjustment
            && audit.EntityId == created.Value.Id
            && audit.Severity == AuditSeverity.Critical);
        dbContext.AuditLogEntries.Should().Contain(audit =>
            audit.ActionType == AuditActionType.LedgerEntryCreated
            && audit.EntityType == AuditEntityType.ResidentLedgerEntry
            && audit.Severity == AuditSeverity.High);
    }

    [Fact]
    public async Task GetFinancialClosureSummaryAsync_CombinesReconciliationAgingAndCollectionActions()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "F16-CLOSE-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Closure Resident");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 1_250_000m, 0m, today.AddDays(-75));

        dbContext.PaymentReconciliationBatches.Add(new PaymentReconciliationBatch
        {
            CompoundId = compound.Id,
            Provider = "ZainCashMock",
            StatementReference = "ST-CLOSURE-1",
            StatementDate = today.AddDays(-1),
            Status = PaymentReconciliationBatchStatus.Open,
            Items =
            [
                new PaymentReconciliationItem
                {
                    ProviderTransactionId = "TX-CLOSURE-ISSUE",
                    ProviderAmount = 500_000m,
                    ProviderStatus = PaymentStatus.Succeeded,
                    MatchStatus = PaymentReconciliationItemStatus.MissingInDarak,
                    IssueReason = "Provider transaction was not found in DARAK."
                }
            ]
        });
        dbContext.FinancialDisputes.Add(new FinancialDispute
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = bill.Id,
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "Bill amount dispute",
            ResidentMessage = "Please review the bill amount.",
            CreatedByUserId = Guid.NewGuid()
        });
        dbContext.PenaltyRules.Add(new PenaltyRule
        {
            CompoundId = compound.Id,
            Name = "Utility penalty pause while disputed",
            TargetType = PenaltyRuleTargetType.UtilityBill,
            CalculationType = PenaltyCalculationType.FixedAmount,
            Status = PenaltyRuleStatus.Active,
            PauseWhenDisputed = true,
            Amount = 10_000m
        });
        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            SourceId = bill.Id,
            Stage = CollectionStage.FinalNotice,
            Status = CollectionCaseStatus.LegalEscalated,
            AmountDue = 1_250_000m,
            DueDate = today.AddDays(-75),
            Reason = "Overdue disputed utility bill",
            LastActionAtUtc = DateTime.UtcNow.AddDays(-20),
            LegalNotices =
            [
                new LegalNotice
                {
                    CompoundId = compound.Id,
                    ResidentProfileId = resident.Id,
                    NoticeType = LegalNoticeType.FinalPaymentNotice,
                    Status = LegalNoticeStatus.Issued,
                    Title = "Final notice",
                    Body = "Please settle the overdue amount."
                }
            ]
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetFinancialClosureSummaryAsync(new FinancialClosureSummaryQuery
        {
            CompoundId = compound.Id,
            AsOfDate = today,
            ReconciliationLookbackDays = 30,
            MinimumOverdueDays = 1
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OpenReconciliationBatchCount.Should().Be(1);
        result.Value.UnreviewedReconciliationIssueItemCount.Should().Be(1);
        result.Value.HighRiskResidentCount.Should().Be(1);
        result.Value.TotalOverdueAmount.Should().Be(1_250_000m);
        result.Value.UnderFinancialReviewAmount.Should().Be(1_250_000m);
        result.Value.PenaltyPauseRecommendedAmount.Should().Be(1_250_000m);
        result.Value.HighPriorityCollectionCaseCount.Should().Be(1);
        result.Value.ActionItems.Should().Contain(item => item.Category == "Payment Reconciliation");
        result.Value.ActionItems.Should().Contain(item => item.Category == "Aging Risk" && item.ResidentProfileId == resident.Id);
        result.Value.ActionItems.Should().Contain(item => item.Category == "Collection Follow-up" && item.ResidentProfileId == resident.Id);
    }

    [Fact]
    public async Task GetFinancialClosureSummaryAsync_ReturnsNotFoundForUnauthorizedCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "F16-CLOSE-A");
        var blockedCompound = await AddCompoundAsync(dbContext, "F16-CLOSE-B");
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetFinancialClosureSummaryAsync(new FinancialClosureSummaryQuery
        {
            CompoundId = blockedCompound.Id
        });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static FinancialControlService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new FinancialControlService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds),
            new AuditLogService(dbContext, new FakeCompoundAccessService(allowedCompoundIds), new HttpContextAccessor()));
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

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = await AddResidentForUserAsync(dbContext, compoundId, Guid.NewGuid(), fullName);
        return resident;
    }

    private static async Task<ResidentProfile> AddResidentForUserAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid userId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            UserId = userId,
            CompoundId = compoundId,
            FullName = fullName,
            PhoneNumber = "+9647700000000",
            IsActive = true
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<UtilityBill> AddUtilityBillAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        decimal totalAmount,
        decimal paidAmount,
        DateOnly dueDate)
    {
        var bill = new UtilityBill
        {
            CompoundId = compoundId,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = residentProfileId,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = $"BILL-{Guid.NewGuid():N}",
            BillStatus = paidAmount >= totalAmount ? BillStatus.Paid : paidAmount > 0 ? BillStatus.PartiallyPaid : BillStatus.Unpaid,
            IssueDate = dueDate.AddDays(-10),
            DueDate = dueDate,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount
        };
        dbContext.UtilityBills.Add(bill);
        await dbContext.SaveChangesAsync();
        return bill;
    }

    private static async Task<RentInvoice> AddRentInvoiceAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        decimal totalAmount,
        decimal paidAmount,
        DateOnly dueDate)
    {
        var invoice = new RentInvoice
        {
            RentContractId = Guid.NewGuid(),
            CompoundId = compoundId,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = residentProfileId,
            InvoiceNumber = $"RENT-{Guid.NewGuid():N}",
            Year = dueDate.Year,
            Month = dueDate.Month,
            IssueDate = dueDate.AddDays(-10),
            DueDate = dueDate,
            RentAmount = totalAmount,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            RentInvoiceStatus = paidAmount >= totalAmount ? RentInvoiceStatus.Paid : paidAmount > 0 ? RentInvoiceStatus.PartiallyPaid : RentInvoiceStatus.Unpaid
        };
        dbContext.RentInvoices.Add(invoice);
        await dbContext.SaveChangesAsync();
        return invoice;
    }

    private static async Task<InstallmentScheduleItem> AddInstallmentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        decimal amount,
        decimal paidAmount,
        DateOnly dueDate)
    {
        var contract = new PropertySaleContract
        {
            CompoundId = compoundId,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = residentProfileId,
            ContractNumber = $"SALE-{Guid.NewGuid():N}",
            ContractStatus = SaleContractStatus.Active,
            SaleType = SaleType.Installment,
            PropertyPrice = amount,
            DownPaymentAmount = 0m,
            ContractDate = dueDate.AddMonths(-1)
        };
        var installment = new InstallmentScheduleItem
        {
            PropertySaleContractId = contract.Id,
            PropertySaleContract = contract,
            CompoundId = compoundId,
            PropertyUnitId = contract.PropertyUnitId,
            ResidentProfileId = residentProfileId,
            InstallmentNumber = 1,
            DueDate = dueDate,
            Amount = amount,
            PaidAmount = paidAmount,
            InstallmentStatus = paidAmount >= amount ? InstallmentStatus.Paid : paidAmount > 0 ? InstallmentStatus.PartiallyPaid : InstallmentStatus.Pending
        };
        dbContext.PropertySaleContracts.Add(contract);
        dbContext.InstallmentScheduleItems.Add(installment);
        await dbContext.SaveChangesAsync();
        return installment;
    }
}
