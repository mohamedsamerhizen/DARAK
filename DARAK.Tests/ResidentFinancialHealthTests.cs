using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class ResidentFinancialHealthTests
{
    [Fact]
    public async Task FinancialHealth_AtRiskResident_ReturnsMetricsAndRiskReasons()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedFinancialHealthWorldAsync(database);

        await using var dbContext = TestDb.Create(database);
        var service = new ResidentFinancialHealthService(
            dbContext,
            new FakeCompoundAccessService([seed.CompoundId]));

        var result = await service.GetAdminResidentFinancialHealthAsync(
            seed.AdminUserId,
            seed.ResidentProfileId);

        result.Status.Should().Be(ServiceResultStatus.Success);
        var health = result.Value!;
        health.ResidentProfileId.Should().Be(seed.ResidentProfileId);
        health.TotalOutstandingAmount.Should().Be(1_700_000m);
        health.OverdueAmount.Should().Be(1_700_000m);
        health.OverdueBillsCount.Should().Be(2);
        health.LongestOverdueDays.Should().BeGreaterThanOrEqualTo(35);
        health.AveragePaymentDelayDays.Should().BeGreaterThan(0m);
        health.OnTimePaymentRate.Should().BeLessThan(100m);
        health.RecentDisputesCount.Should().Be(1);
        health.FailedPaymentsCount.Should().Be(1);
        health.Status.Should().Be(ResidentFinancialHealthStatus.Critical);
        health.RiskReasons.Should().Contain(reason => reason.Contains("overdue", StringComparison.OrdinalIgnoreCase));
        health.RiskReasons.Should().Contain(reason => reason.Contains("billing dispute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FinancialHealth_AdminOutsideCompoundScope_ReturnsNotFound()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedFinancialHealthWorldAsync(database);

        await using var dbContext = TestDb.Create(database);
        var service = new ResidentFinancialHealthService(
            dbContext,
            new FakeCompoundAccessService([Guid.NewGuid()]));

        var result = await service.GetAdminResidentFinancialHealthAsync(
            seed.AdminUserId,
            seed.ResidentProfileId);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ConversationDetails_ResidentContextPanel_IncludesFinancialHealthAndActivity()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedFinancialHealthWorldAsync(database);

        var openResult = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentConversationAsync(
                seed.ResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    InitialMessage = "My bill looks risky."
                }));

        openResult.Status.Should().Be(ServiceResultStatus.Success);

        var details = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.GetAdminConversationDetailsAsync(
                seed.AdminUserId,
                openResult.Value!.Id));

        details.Status.Should().Be(ServiceResultStatus.Success);
        var context = details.Value!.ResidentContext;
        context.ResidentProfileId.Should().Be(seed.ResidentProfileId);
        context.OutstandingAmount.Should().Be(1_700_000m);
        context.OverdueAmount.Should().Be(1_700_000m);
        context.OpenConversationsCount.Should().BeGreaterThanOrEqualTo(1);
        context.FinancialHealthStatus.Should().Be(ResidentFinancialHealthStatus.Critical);
        context.FinancialHealthRiskReasons.Should().NotBeEmpty();
        context.RecentActivityEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FinancialHealthDashboard_SummarizesScopedResidentRisk()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedFinancialHealthWorldAsync(database);

        await using var dbContext = TestDb.Create(database);
        var service = new ResidentFinancialHealthService(
            dbContext,
            new FakeCompoundAccessService([seed.CompoundId]));

        var result = await service.GetDashboardSummaryAsync(
            seed.AdminUserId,
            new FinancialHealthDashboardQuery { CompoundId = seed.CompoundId });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var dashboard = result.Value!;
        dashboard.ResidentsCount.Should().Be(1);
        dashboard.CriticalResidentsCount.Should().Be(1);
        dashboard.TotalOutstandingAmount.Should().Be(1_700_000m);
        dashboard.HighestRiskResidents.Should().ContainSingle(item => item.ResidentProfileId == seed.ResidentProfileId);
    }



    [Fact]
    public async Task FinancialHealthDashboard_BatchedPath_SummarizesMultipleScopedResidents()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedFinancialHealthWorldAsync(database);

        await using (var seedContext = TestDb.Create(database))
        {
            var healthyUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"healthy-{Guid.NewGuid():N}@test.local",
                Email = $"healthy-{Guid.NewGuid():N}@test.local",
                FullName = "Healthy Resident"
            };
            var outsideUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"outside-{Guid.NewGuid():N}@test.local",
                Email = $"outside-{Guid.NewGuid():N}@test.local",
                FullName = "Outside Resident"
            };
            var outsideCompound = new Compound
            {
                Id = Guid.NewGuid(),
                Name = "Outside Compound",
                Code = Guid.NewGuid().ToString("N")[..8],
                City = "Baghdad",
                Area = "Mansour"
            };

            seedContext.Users.AddRange(healthyUser, outsideUser);
            seedContext.Compounds.Add(outsideCompound);
            seedContext.ResidentProfiles.AddRange(
                new ResidentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = healthyUser.Id,
                    CompoundId = seed.CompoundId,
                    FullName = "Healthy Resident"
                },
                new ResidentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = outsideUser.Id,
                    CompoundId = outsideCompound.Id,
                    FullName = "Outside Resident"
                });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = TestDb.Create(database);
        var service = new ResidentFinancialHealthService(
            dbContext,
            new FakeCompoundAccessService([seed.CompoundId]));

        var result = await service.GetDashboardSummaryAsync(
            seed.AdminUserId,
            new FinancialHealthDashboardQuery { CompoundId = seed.CompoundId, HighRiskLimit = 10 });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var dashboard = result.Value!;
        dashboard.ResidentsCount.Should().Be(2);
        dashboard.HealthyResidentsCount.Should().Be(1);
        dashboard.CriticalResidentsCount.Should().Be(1);
        dashboard.HighestRiskResidents.Should().ContainSingle(item => item.ResidentProfileId == seed.ResidentProfileId);
    }

    private static async Task<TResult> ExecuteConversationAsync<TResult>(
        TestDb.TestDatabase database,
        Guid allowedCompoundId,
        Func<ConversationService, Task<TResult>> action)
    {
        await using var dbContext = TestDb.Create(database);
        var access = new FakeCompoundAccessService([allowedCompoundId]);
        var financialHealthService = new ResidentFinancialHealthService(dbContext, access);
        var service = new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext, access),
            access,
            financialHealthService);

        return await action(service);
    }

    private static async Task<FinancialHealthSeed> SeedFinancialHealthWorldAsync(TestDb.TestDatabase database)
    {
        await using var dbContext = TestDb.Create(database);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);

        var residentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"resident-{Guid.NewGuid():N}@test.local",
            Email = $"resident-{Guid.NewGuid():N}@test.local",
            FullName = "Financial Resident"
        };

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"admin-{Guid.NewGuid():N}@test.local",
            Email = $"admin-{Guid.NewGuid():N}@test.local",
            FullName = "Finance Admin"
        };

        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = "Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        var unit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            UnitNumber = "FH-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 1
        };

        var resident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = residentUser.Id,
            CompoundId = compound.Id,
            FullName = "Financial Resident"
        };

        var occupancy = new OccupancyRecord
        {
            Id = Guid.NewGuid(),
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-6)
        };

        var billingCycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(-5),
            IsClosed = false
        };

        var overdueBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = $"BILL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Overdue,
            IssueDate = today.AddDays(-50),
            DueDate = today.AddDays(-40),
            SubtotalAmount = 1_200_000m,
            TotalAmount = 1_200_000m,
            PaidAmount = 0m
        };

        var partialBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = $"BILL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.PartiallyPaid,
            IssueDate = today.AddDays(-20),
            DueDate = today.AddDays(-10),
            SubtotalAmount = 800_000m,
            TotalAmount = 800_000m,
            PaidAmount = 300_000m
        };

        var paidBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = $"BILL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Paid,
            IssueDate = today.AddDays(-70),
            DueDate = today.AddDays(-60),
            SubtotalAmount = 100_000m,
            TotalAmount = 100_000m,
            PaidAmount = 100_000m
        };

        var succeededLatePayment = new Payment
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = paidBill.Id,
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 100_000m,
            PaymentReference = $"PAY-{Guid.NewGuid():N}",
            CreatedAt = now.AddDays(-30),
            CompletedAt = paidBill.DueDate.ToDateTime(TimeOnly.MinValue).AddDays(20)
        };

        var failedPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = overdueBill.Id,
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Failed,
            Amount = 50_000m,
            PaymentReference = $"PAY-{Guid.NewGuid():N}",
            CreatedAt = now.AddDays(-3),
            FailureReason = "Provider rejected the attempt."
        };

        var dispute = new Conversation
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            Status = ConversationStatus.PendingAdminReply,
            Priority = ConversationPriority.High,
            Topic = ConversationTopic.Billing,
            IssueType = ConversationIssueType.BillingHighAmount,
            LinkedEntityType = ConversationLinkedEntityType.UtilityBill,
            LinkedEntityId = overdueBill.Id,
            CreatedAtUtc = now.AddDays(-5),
            LastMessageAtUtc = now.AddDays(-5)
        };

        dbContext.Users.AddRange(residentUser, adminUser);
        dbContext.Compounds.Add(compound);
        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.OccupancyRecords.Add(occupancy);
        dbContext.BillingCycles.Add(billingCycle);
        dbContext.UtilityBills.AddRange(overdueBill, partialBill, paidBill);
        dbContext.Payments.AddRange(succeededLatePayment, failedPayment);
        dbContext.Conversations.Add(dispute);
        await dbContext.SaveChangesAsync();

        return new FinancialHealthSeed(
            compound.Id,
            unit.Id,
            resident.Id,
            residentUser.Id,
            adminUser.Id);
    }

    private sealed record FinancialHealthSeed(
        Guid CompoundId,
        Guid UnitId,
        Guid ResidentProfileId,
        Guid ResidentUserId,
        Guid AdminUserId);
}
