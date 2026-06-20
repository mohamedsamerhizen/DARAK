using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class CollectionsLegalComplianceServiceTests
{
    [Fact]
    public async Task CreatePenaltyRuleAsync_CreatesCompoundScopedRule()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-1");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreatePenaltyRuleAsync(Guid.NewGuid(), new CreatePenaltyRuleRequest
        {
            CompoundId = compound.Id,
            Name = "Utility late fee",
            TargetType = PenaltyRuleTargetType.UtilityBill,
            CalculationType = PenaltyCalculationType.FixedAmount,
            GracePeriodDays = 5,
            Amount = 10_000m,
            PauseWhenDisputed = true
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(PenaltyRuleStatus.Active);
        dbContext.PenaltyRules.Should().ContainSingle(rule => rule.CompoundId == compound.Id && rule.Name == "Utility late fee");
    }

    [Fact]
    public async Task CreateCollectionCaseAsync_RejectsDuplicateOpenSourceCase()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-2");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Collections Resident");
        var service = CreateService(dbContext, compound.Id);
        var sourceId = Guid.NewGuid();
        dbContext.UtilityBills.Add(new UtilityBill
        {
            Id = sourceId,
            CompoundId = compound.Id,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "UB-CLC-DUP-1",
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 10),
            TotalAmount = 250_000m,
            PaidAmount = 0m,
            BillStatus = BillStatus.Overdue
        });
        await dbContext.SaveChangesAsync();

        var request = new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            SourceId = sourceId,
            AmountDue = 250_000m,
            Reason = "Overdue utility bill"
        };

        var first = await service.CreateCollectionCaseAsync(Guid.NewGuid(), request);
        var second = await service.CreateCollectionCaseAsync(Guid.NewGuid(), request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.CollectionCases.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAndIssueLegalNoticeAsync_MovesDraftNoticeToIssued()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-3");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Legal Notice Resident");
        var service = CreateService(dbContext, compound.Id);
        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 500_000m,
            Reason = "Long overdue balance"
        });

        var notice = await service.CreateLegalNoticeAsync(Guid.NewGuid(), new CreateLegalNoticeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collectionCase.Value!.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Title = "Final payment notice",
            Body = "Please settle the outstanding amount.",
            Status = LegalNoticeStatus.Draft
        });

        var issued = await service.IssueLegalNoticeAsync(
            notice.Value!.Id,
            Guid.NewGuid(),
            new IssueLegalNoticeRequest { DeliveryChannel = "Manual", DeliveryReference = "Gate delivery" });

        issued.IsSuccess.Should().BeTrue(issued.Message);
        issued.Value!.Status.Should().Be(LegalNoticeStatus.Issued);
        issued.Value.IssuedAtUtc.Should().NotBeNull();
        dbContext.LegalNotices.Single().DeliveryReference.Should().Be("Gate delivery");
    }

    [Fact]
    public async Task CreatePaymentPlanAsync_CreatesInstallmentsAndSettlesCaseWhenPaid()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-4");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Plan Resident");
        var service = CreateService(dbContext, compound.Id);
        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 300_000m,
            Reason = "Debt settlement"
        });

        var plan = await service.CreatePaymentPlanAsync(Guid.NewGuid(), new CreatePaymentPlanRequest
        {
            CollectionCaseId = collectionCase.Value!.Id,
            TotalAmount = 300_000m,
            InstallmentCount = 3,
            StartDate = new DateOnly(2026, 7, 1),
            Notes = "Three monthly payments"
        });

        plan.IsSuccess.Should().BeTrue(plan.Message);
        plan.Value!.Installments.Should().HaveCount(3);
        dbContext.CollectionCases.Single().Status.Should().Be(CollectionCaseStatus.PaymentPlanActive);

        foreach (var installment in plan.Value.Installments)
        {
            await service.PayPaymentPlanInstallmentAsync(
                plan.Value.Id,
                installment.Id,
                new PayPaymentPlanInstallmentRequest { Amount = installment.Amount });
        }

        dbContext.PaymentPlans.Single().Status.Should().Be(PaymentPlanStatus.Completed);
        dbContext.CollectionCases.Single().Status.Should().Be(CollectionCaseStatus.Settled);
    }

    [Fact]
    public async Task GetResidentComplianceProfileAsync_ReturnsCriticalWhenCollectionsAndLegalNoticesExist()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-5");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Compliance Resident");
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = compound.Id,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "UB-CLC-5",
            IssueDate = new DateOnly(2026, 5, 1),
            DueDate = new DateOnly(2026, 5, 10),
            TotalAmount = 1_200_000m,
            PaidAmount = 0m,
            BillStatus = BillStatus.Overdue
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);
        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 1_200_000m,
            Reason = "Critical overdue balance"
        });
        await service.CreateLegalNoticeAsync(Guid.NewGuid(), new CreateLegalNoticeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collectionCase.Value!.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Title = "Final notice",
            Body = "Final notice body",
            Status = LegalNoticeStatus.Issued
        });

        var profile = await service.GetResidentComplianceProfileAsync(resident.Id);

        profile.IsSuccess.Should().BeTrue(profile.Message);
        profile.Value!.ComplianceStatus.Should().Be("Critical");
        profile.Value.OverdueItemCount.Should().Be(1);
        profile.Value.OpenCollectionCaseCount.Should().Be(1);
        profile.Value.ActiveLegalNoticeCount.Should().Be(1);
    }


    [Fact]
    public async Task GetCollectionFollowUpQueueAsync_ReturnsHighPriorityForOverdueCase()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-6");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Overdue Follow-up Resident");
        var service = CreateService(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 1_500_000m,
            DueDate = today.AddDays(-75),
            Reason = "Long overdue balance requiring follow-up"
        });

        dbContext.CollectionCases.Single().LastActionAtUtc = DateTime.UtcNow.AddDays(-21);
        await dbContext.SaveChangesAsync();

        var queue = await service.GetCollectionFollowUpQueueAsync(new CollectionFollowUpQueueQueryRequest
        {
            CompoundId = compound.Id
        });

        var item = queue.Items.Should().ContainSingle().Subject;
        item.CollectionCaseId.Should().Be(collectionCase.Value!.Id);
        item.FollowUpPriority.Should().Be("High");
        item.DaysOverdue.Should().BeGreaterThanOrEqualTo(75);
        item.DaysSinceLastAction.Should().BeGreaterThanOrEqualTo(21);
        item.RecommendedAction.Should().Contain("Offer a payment plan");
        item.Reasons.Should().Contain(reason => reason.Contains("overdue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCollectionFollowUpQueueAsync_ReportsActivePaymentPlanInstallmentDue()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-7");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Payment Plan Follow-up Resident");
        var service = CreateService(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 300_000m,
            DueDate = today.AddDays(-10),
            Reason = "Balance under payment plan"
        });

        await service.CreatePaymentPlanAsync(Guid.NewGuid(), new CreatePaymentPlanRequest
        {
            CollectionCaseId = collectionCase.Value!.Id,
            TotalAmount = 300_000m,
            InstallmentCount = 3,
            StartDate = today,
            Notes = "Resident promised monthly settlement"
        });

        var queue = await service.GetCollectionFollowUpQueueAsync(new CollectionFollowUpQueueQueryRequest
        {
            CompoundId = compound.Id
        });

        var item = queue.Items.Should().ContainSingle().Subject;
        item.HasActivePaymentPlan.Should().BeTrue();
        item.NextPaymentPlanDueDate.Should().Be(today);
        item.NextPaymentPlanOutstandingAmount.Should().Be(100_000m);
        item.RecommendedAction.Should().Contain("due payment plan installment");
    }

    [Fact]
    public async Task GetCollectionFollowUpQueueAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "CLC-8A");
        var blockedCompound = await AddCompoundAsync(dbContext, "CLC-8B");
        var allowedResident = await AddResidentAsync(dbContext, allowedCompound.Id, "Allowed Follow-up Resident");
        var blockedResident = await AddResidentAsync(dbContext, blockedCompound.Id, "Blocked Follow-up Resident");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = allowedCompound.Id,
            ResidentProfileId = allowedResident.Id,
            AmountDue = 100_000m,
            DueDate = today.AddDays(-5),
            Reason = "Allowed case",
            LastActionAtUtc = DateTime.UtcNow.AddDays(-5)
        });
        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = blockedCompound.Id,
            ResidentProfileId = blockedResident.Id,
            AmountDue = 900_000m,
            DueDate = today.AddDays(-60),
            Reason = "Blocked case",
            LastActionAtUtc = DateTime.UtcNow.AddDays(-60)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, allowedCompound.Id);

        var queue = await service.GetCollectionFollowUpQueueAsync(new CollectionFollowUpQueueQueryRequest());

        var item = queue.Items.Should().ContainSingle().Subject;
        item.CompoundId.Should().Be(allowedCompound.Id);
        item.ResidentProfileId.Should().Be(allowedResident.Id);
    }


    [Fact]
    public async Task GetLegalCaseManagementDashboardAsync_ReturnsExecutiveLegalMetrics()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-P4-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Legal Dashboard Resident");
        var service = CreateService(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 2_750_000m,
            DueDate = today.AddDays(-95),
            Reason = "Long overdue legal balance"
        });

        await service.CreateLegalNoticeAsync(Guid.NewGuid(), new CreateLegalNoticeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collectionCase.Value!.Id,
            NoticeType = LegalNoticeType.LegalEscalationNotice,
            Title = "Legal escalation notice",
            Body = "Legal notice body",
            Status = LegalNoticeStatus.Issued,
            DeadlineDate = today.AddDays(-3)
        });

        var dashboard = await service.GetLegalCaseManagementDashboardAsync(compound.Id);

        dashboard.IsSuccess.Should().BeTrue(dashboard.Message);
        dashboard.Value!.OpenCaseCount.Should().Be(1);
        dashboard.Value.ReadyForLegalEscalationCount.Should().Be(1);
        dashboard.Value.ActiveLegalNoticeCount.Should().Be(1);
        dashboard.Value.OverdueLegalNoticeCount.Should().Be(1);
        dashboard.Value.OpenCollectionAmount.Should().Be(2_750_000m);
        dashboard.Value.ExecutiveAlerts.Should().Contain(alert => alert.Contains("legal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetLegalEscalationQueueAsync_FlagsReadyCaseWithExpiredLegalNotice()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-P4-2");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Legal Queue Resident");
        var service = CreateService(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 1_500_000m,
            DueDate = today.AddDays(-70),
            Reason = "Legal-ready overdue balance"
        });

        await service.CreateLegalNoticeAsync(Guid.NewGuid(), new CreateLegalNoticeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collectionCase.Value!.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Title = "Final payment notice",
            Body = "Final notice body",
            Status = LegalNoticeStatus.Issued,
            DeadlineDate = today.AddDays(-1)
        });

        var queue = await service.GetLegalCaseEscalationQueueAsync(new LegalCaseEscalationQueueQueryRequest
        {
            CompoundId = compound.Id,
            OnlyReadyForEscalation = true
        });

        var item = queue.Items.Should().ContainSingle().Subject;
        item.CollectionCaseId.Should().Be(collectionCase.Value.Id);
        item.IsReadyForLegalEscalation.Should().BeTrue();
        item.LegalPriority.Should().Be("Critical");
        item.BlockingIssues.Should().BeEmpty();
        item.RecommendedLegalAction.Should().Contain("deadline has expired");
    }

    [Fact]
    public async Task GetLegalNoticeServiceQueueAsync_ReturnsDraftAndDeadlineActions()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-P4-3");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Legal Notice Queue Resident");
        var service = CreateService(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await service.CreateLegalNoticeAsync(Guid.NewGuid(), new CreateLegalNoticeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Title = "Draft final notice",
            Body = "Draft final notice body",
            Status = LegalNoticeStatus.Draft,
            DeadlineDate = today.AddDays(2)
        });

        var queue = await service.GetLegalNoticeServiceQueueAsync(new LegalNoticeServiceQueueQueryRequest
        {
            CompoundId = compound.Id,
            OnlyActionRequired = true
        });

        var item = queue.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(LegalNoticeStatus.Draft);
        item.IsActionRequired.Should().BeTrue();
        item.ServicePriority.Should().Be("High");
        item.RecommendedAction.Should().Contain("issue the notice");
    }

    [Fact]
    public async Task GetLegalCaseFileAsync_ReturnsNoticesPaymentPlansAndTimeline()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "CLC-P4-4");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Legal File Resident");
        var service = CreateService(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 900_000m,
            DueDate = today.AddDays(-45),
            Reason = "Case file overdue balance"
        });

        await service.CreateLegalNoticeAsync(Guid.NewGuid(), new CreateLegalNoticeRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collectionCase.Value!.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Title = "Final notice",
            Body = "Final notice body",
            Status = LegalNoticeStatus.Issued,
            DeadlineDate = today.AddDays(7)
        });

        await service.CreatePaymentPlanAsync(Guid.NewGuid(), new CreatePaymentPlanRequest
        {
            CollectionCaseId = collectionCase.Value.Id,
            TotalAmount = 900_000m,
            InstallmentCount = 3,
            StartDate = today,
            Notes = "Legal file plan"
        });

        var caseFile = await service.GetLegalCaseFileAsync(collectionCase.Value.Id);

        caseFile.IsSuccess.Should().BeTrue(caseFile.Message);
        caseFile.Value!.CollectionCaseId.Should().Be(collectionCase.Value.Id);
        caseFile.Value.LegalNotices.Should().ContainSingle();
        caseFile.Value.PaymentPlans.Should().ContainSingle();
        caseFile.Value.Timeline.Should().Contain(item => item.EventType == "CollectionCaseOpened");
        caseFile.Value.Timeline.Should().Contain(item => item.EventType == "LegalNoticeCreated");
        caseFile.Value.Timeline.Should().Contain(item => item.EventType == "PaymentPlanCreated");
    }

    private static CollectionsLegalComplianceService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new CollectionsLegalComplianceService(dbContext, new FakeCompoundAccessService(allowedCompoundIds));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            FullName = fullName,
            UserId = Guid.NewGuid(),
            IsActive = true
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }
}
