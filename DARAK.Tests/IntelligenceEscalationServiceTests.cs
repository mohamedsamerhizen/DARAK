using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class IntelligenceEscalationServiceTests
{
    [Fact]
    public async Task GetCompoundEscalationDashboardAsync_AggregatesFinancialOperationsLegalCommunicationAndNotificationSignals()
    {
        await using var dbContext = TestDb.Create();
        var seeded = await SeedEscalationScenarioAsync(dbContext, "INT-DASH");
        var service = CreateService(dbContext, seeded.Compound.Id);

        var result = await service.GetCompoundEscalationDashboardAsync(seeded.Compound.Id, 10);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalOpenEscalations.Should().BeGreaterThanOrEqualTo(6);
        result.Value.FinancialEscalations.Should().BeGreaterThan(0);
        result.Value.OperationsEscalations.Should().BeGreaterThan(0);
        result.Value.LegalEscalations.Should().BeGreaterThan(0);
        result.Value.CommunicationEscalations.Should().BeGreaterThan(0);
        result.Value.NotificationEscalations.Should().BeGreaterThan(0);
        result.Value.TopEscalations.Should().NotBeEmpty();
        result.Value.ExecutiveActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCompoundEscalationQueueAsync_FiltersByAreaAndSeverity()
    {
        await using var dbContext = TestDb.Create();
        var seeded = await SeedEscalationScenarioAsync(dbContext, "INT-QUEUE");
        var service = CreateService(dbContext, seeded.Compound.Id);

        var result = await service.GetCompoundEscalationQueueAsync(seeded.Compound.Id, "Legal", "High", 20);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().NotBeEmpty();
        result.Value.Items.Should().OnlyContain(item => item.Area == "Legal" && item.Severity == "High");
    }

    [Fact]
    public async Task GetResidentDecisionBriefAsync_ReturnsDecisionBandBlockersAndCompoundScopeProtection()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await SeedEscalationScenarioAsync(dbContext, "INT-ALLOW");
        var other = await SeedEscalationScenarioAsync(dbContext, "INT-OTHER");
        var service = CreateService(dbContext, allowed.Compound.Id);

        var allowedResult = await service.GetResidentDecisionBriefAsync(allowed.Resident.Id);
        var forbiddenResult = await service.GetResidentDecisionBriefAsync(other.Resident.Id);

        allowedResult.IsSuccess.Should().BeTrue(allowedResult.Message);
        allowedResult.Value!.ResidentId.Should().Be(allowed.Resident.Id);
        allowedResult.Value.EscalationScore.Should().BeGreaterThan(0);
        allowedResult.Value.FinancialExposure.Should().BeGreaterThan(0);
        allowedResult.Value.DecisionBlockers.Should().NotBeEmpty();
        allowedResult.Value.RecommendedActions.Should().NotBeEmpty();
        forbiddenResult.IsSuccess.Should().BeFalse();
        forbiddenResult.Message!.ToLowerInvariant().Should().Contain("access");
    }

    private static IntelligenceEscalationService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new IntelligenceEscalationService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<SeededEscalationScenario> SeedEscalationScenarioAsync(ApplicationDbContext dbContext, string code)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Demo"
        };
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            Name = $"Building {code}",
            Code = $"B-{code}",
            NumberOfFloors = 1
        };
        var floor = new Floor
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            BuildingId = building.Id,
            FloorNumber = 1,
            Name = "First"
        };
        var unit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            BuildingId = building.Id,
            FloorId = floor.Id,
            UnitNumber = $"{code}-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 125,
            Bedrooms = 2,
            Bathrooms = 2
        };
        var resident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = $"Resident {code}",
            PhoneNumber = "07700000000"
        };
        var billingCycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(-10)
        };
        var utilityBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = $"BILL-{code}",
            BillStatus = BillStatus.Overdue,
            IssueDate = today.AddDays(-20),
            DueDate = today.AddDays(-10),
            SubtotalAmount = 250000,
            TotalAmount = 250000,
            PaidAmount = 50000
        };
        var rentInvoice = new RentInvoice
        {
            Id = Guid.NewGuid(),
            RentContractId = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            InvoiceNumber = $"RENT-{code}",
            Year = today.Year,
            Month = today.Month,
            IssueDate = today.AddDays(-40),
            DueDate = today.AddDays(-35),
            RentAmount = 1200000,
            TotalAmount = 1200000,
            PaidAmount = 0,
            RentInvoiceStatus = RentInvoiceStatus.Overdue
        };
        var collectionCase = new CollectionCase
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Stage = CollectionStage.FinalNotice,
            Status = CollectionCaseStatus.Open,
            AmountDue = 1200000,
            DueDate = today.AddDays(-3),
            Reason = "Overdue rent",
            OpenedAtUtc = now.AddDays(-12)
        };
        var legalNotice = new LegalNotice
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collectionCase.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Status = LegalNoticeStatus.Issued,
            Title = "Final payment notice",
            Body = "Please settle balance.",
            DeadlineDate = today.AddDays(-1),
            CreatedAtUtc = now.AddDays(-5)
        };
        var dispute = new FinancialDispute
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = utilityBill.Id,
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "Billing objection",
            ResidentMessage = "Meter looks wrong.",
            CreatedByUserId = resident.UserId,
            CreatedAtUtc = now.AddDays(-8)
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            Status = ConversationStatus.PendingAdminReply,
            Priority = ConversationPriority.High,
            LastMessageAtUtc = now.AddDays(-3),
            CreatedAtUtc = now.AddDays(-4)
        };
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            Title = "Emergency water pump repair",
            Description = "Pump stopped.",
            Priority = WorkOrderPriority.Emergency,
            Status = WorkOrderStatus.InProgress,
            CreatedAtUtc = now.AddDays(-2),
            DueAtUtc = now.AddHours(-3)
        };
        var supportCase = new SupportCase
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            Priority = SupportCasePriority.Critical,
            Status = SupportCaseStatus.Open,
            Title = "Move-out clearance blocker",
            Description = "Resident waiting for decision.",
            DueAtUtc = now.AddHours(-5),
            CreatedAtUtc = now.AddDays(-1)
        };
        var notification = new NotificationOutbox
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Channel = NotificationChannel.Email,
            EventType = NotificationEventType.General,
            Priority = NotificationPriority.High,
            Status = NotificationStatus.Failed,
            RecipientName = resident.FullName,
            Subject = "Payment follow-up failed",
            Body = "Payment follow-up could not be delivered.",
            CreatedAtUtc = now.AddDays(-1),
            ScheduledAtUtc = now.AddHours(-8),
            RetryCount = 3
        };
        var riskFlag = new ResidentRiskFlag
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            CreatedByUserId = Guid.NewGuid(),
            FlagType = ResidentRiskFlagType.RepeatedLatePayments,
            Severity = ResidentRiskFlagSeverity.High,
            Status = ResidentRiskFlagStatus.Active,
            Title = "Payment risk",
            Description = "Repeated late payments"
        };

        dbContext.Compounds.Add(compound);
        dbContext.Buildings.Add(building);
        dbContext.Floors.Add(floor);
        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.BillingCycles.Add(billingCycle);
        dbContext.UtilityBills.Add(utilityBill);
        dbContext.RentInvoices.Add(rentInvoice);
        dbContext.CollectionCases.Add(collectionCase);
        dbContext.LegalNotices.Add(legalNotice);
        dbContext.FinancialDisputes.Add(dispute);
        dbContext.Conversations.Add(conversation);
        dbContext.WorkOrders.Add(workOrder);
        dbContext.SupportCases.Add(supportCase);
        dbContext.NotificationOutboxes.Add(notification);
        dbContext.ResidentRiskFlags.Add(riskFlag);
        await dbContext.SaveChangesAsync();

        return new SeededEscalationScenario(compound, unit, resident);
    }

    private sealed record SeededEscalationScenario(
        Compound Compound,
        PropertyUnit Unit,
        ResidentProfile Resident);
}
