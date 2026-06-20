using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class SaasTenantIntelligenceServiceTests
{
    [Fact]
    public async Task GetPortfolioOverviewAsync_ReturnsOnlyAccessibleTenantsAndLicenseCapacity()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await SeedSaasTenantAsync(dbContext, "SAAS-A", true);
        await SeedSaasTenantAsync(dbContext, "SAAS-B", false);
        var service = CreateService(dbContext, allowed.Compound.Id);

        var result = await service.GetPortfolioOverviewAsync();

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalAccessibleCompounds.Should().Be(1);
        result.Value.PrioritizedTenants.Should().ContainSingle(item => item.CompoundId == allowed.Compound.Id);
        result.Value.PrioritizedTenants.Single().PriorityBand.Should().BeOneOf("Critical", "High");
        result.Value.License.IsCapacityExceeded.Should().BeTrue();
        result.Value.CommercialActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTenantReadinessAsync_BlocksUnauthorizedCompoundAndReportsReadiness()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await SeedSaasTenantAsync(dbContext, "SAAS-READ", true);
        var other = await SeedSaasTenantAsync(dbContext, "SAAS-DENY", true);
        var service = CreateService(dbContext, allowed.Compound.Id);

        var allowedResult = await service.GetTenantReadinessAsync(allowed.Compound.Id);
        var forbiddenResult = await service.GetTenantReadinessAsync(other.Compound.Id);

        allowedResult.IsSuccess.Should().BeTrue(allowedResult.Message);
        allowedResult.Value!.FinancialSnapshot.OutstandingAmount.Should().Be(250000);
        allowedResult.Value.Blockers.Should().NotBeEmpty();
        allowedResult.Value.IsCommerciallyReady.Should().BeFalse();
        forbiddenResult.IsSuccess.Should().BeFalse();
        forbiddenResult.Message!.ToLowerInvariant().Should().Contain("access");
    }

    [Fact]
    public async Task GetPrioritizationBrainAsync_ReturnsAreaFilteredActions()
    {
        await using var dbContext = TestDb.Create();
        var seeded = await SeedSaasTenantAsync(dbContext, "SAAS-BRAIN", true);
        var service = CreateService(dbContext, seeded.Compound.Id);

        var result = await service.GetPrioritizationBrainAsync("Financial");

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Actions.Should().NotBeEmpty();
        result.Value.Actions.Should().OnlyContain(item => item.Area == "Financial");
        result.Value.Actions.First().PriorityScore.Should().BeGreaterThan(0);
        result.Value.ExecutiveSummary.Should().NotBeEmpty();
    }

    private static SaasTenantIntelligenceService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new SaasTenantIntelligenceService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<SeededSaasTenant> SeedSaasTenantAsync(
        ApplicationDbContext dbContext,
        string code,
        bool risky)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = $"SaaS Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Commercial"
        };
        var unitOne = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            UnitNumber = $"{code}-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 2
        };
        var unitTwo = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            UnitNumber = $"{code}-102",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available,
            AreaSquareMeters = 90,
            Bedrooms = 2,
            Bathrooms = 1
        };
        var resident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = $"SaaS Resident {code}",
            PhoneNumber = "07700000000"
        };
        var cycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(-3)
        };
        var bill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unitOne.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = cycle.Id,
            BillNumber = $"SAAS-BILL-{code}",
            BillStatus = risky ? BillStatus.Overdue : BillStatus.Paid,
            IssueDate = today.AddDays(-20),
            DueDate = today.AddDays(-5),
            SubtotalAmount = risky ? 300000 : 100000,
            TotalAmount = risky ? 300000 : 100000,
            PaidAmount = risky ? 50000 : 100000
        };
        var support = new SupportCase
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unitOne.Id,
            Category = SupportCaseCategory.Billing,
            Priority = SupportCasePriority.High,
            Status = risky ? SupportCaseStatus.Escalated : SupportCaseStatus.Closed,
            Title = "SaaS escalation",
            Description = "Commercial readiness blocker",
            DueAtUtc = DateTime.UtcNow.AddDays(1)
        };
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unitOne.Id,
            Title = "SaaS work order",
            Description = "Operational item",
            Priority = WorkOrderPriority.High,
            Status = risky ? WorkOrderStatus.InProgress : WorkOrderStatus.Completed,
            DueAtUtc = DateTime.UtcNow.AddDays(2)
        };
        var collection = new CollectionCase
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Status = risky ? CollectionCaseStatus.Open : CollectionCaseStatus.Closed,
            Stage = CollectionStage.FirstNotice,
            AmountDue = risky ? 250000 : 0,
            Reason = "SaaS financial follow-up"
        };
        var legal = new LegalNotice
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CollectionCaseId = collection.Id,
            NoticeType = LegalNoticeType.FirstPaymentNotice,
            Status = risky ? LegalNoticeStatus.Issued : LegalNoticeStatus.Acknowledged,
            Title = "Payment notice",
            Body = "Demo notice"
        };
        var notification = new NotificationOutbox
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Channel = NotificationChannel.Email,
            EventType = NotificationEventType.General,
            Priority = NotificationPriority.High,
            Status = risky ? NotificationStatus.Failed : NotificationStatus.Sent,
            RecipientName = resident.FullName,
            RecipientEmail = "resident@darak.local",
            Subject = "SaaS notification",
            Body = "Demo notification"
        };
        var riskFlag = new ResidentRiskFlag
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unitOne.Id,
            CreatedByUserId = Guid.NewGuid(),
            FlagType = ResidentRiskFlagType.RepeatedLatePayments,
            Severity = ResidentRiskFlagSeverity.High,
            Status = risky ? ResidentRiskFlagStatus.Active : ResidentRiskFlagStatus.Resolved,
            Title = "SaaS risk",
            Description = "Commercial blocker",
            RecommendedAction = "Review before onboarding"
        };

        if (!dbContext.LicenseProfiles.Any())
        {
            dbContext.LicenseProfiles.Add(new LicenseProfile
            {
                Id = Guid.NewGuid(),
                LicensedTo = "DARAK Demo Buyer",
                LicenseKeyFingerprint = "DEMO-FINGERPRINT",
                Plan = LicensePlan.Professional,
                Status = LicenseStatus.Active,
                MaxCompounds = 1,
                MaxUnits = 1,
                IssuedAtUtc = DateTime.UtcNow.AddDays(-10),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(90)
            });
        }

        dbContext.Compounds.Add(compound);
        dbContext.PropertyUnits.AddRange(unitOne, unitTwo);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.BillingCycles.Add(cycle);
        dbContext.UtilityBills.Add(bill);
        dbContext.SupportCases.Add(support);
        dbContext.WorkOrders.Add(workOrder);
        dbContext.CollectionCases.Add(collection);
        dbContext.LegalNotices.Add(legal);
        dbContext.NotificationOutboxes.Add(notification);
        dbContext.ResidentRiskFlags.Add(riskFlag);
        await dbContext.SaveChangesAsync();

        return new SeededSaasTenant(compound, resident);
    }

    private sealed record SeededSaasTenant(
        Compound Compound,
        ResidentProfile Resident);
}
