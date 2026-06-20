using DARAK.Api.Data;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class CommercialPresentationServiceTests
{
    [Fact]
    public async Task GetDemoSeedBlueprintAsync_ReturnsSeedGapsForMinimalCompound()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DEMO-MIN");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetDemoSeedBlueprintAsync(new CommercialPresentationQuery
        {
            CompoundId = compound.Id
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.SeedStatus.Should().Be("NeedsDemoData");
        result.Value.EntityPlans.Should().Contain(item => item.EntityKey == "unit" && item.SuggestedToCreate > 0);
        result.Value.SafeSeedRules.Should().Contain(item => item.Contains("demo database", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCommercialDemoModeAsync_UsesOnlyAllowedCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await SeedPresentationScenarioAsync(dbContext, "DEMO-ALLOWED");
        var other = await AddCompoundAsync(dbContext, "DEMO-OTHER");
        dbContext.PropertyUnits.Add(new PropertyUnit
        {
            CompoundId = other.Id,
            UnitNumber = "OTHER-999",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        });
        dbContext.ResidentProfiles.Add(new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = other.Id,
            FullName = "Other Resident"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.GetCommercialDemoModeAsync(new CommercialPresentationQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        var executive = result.Value!.Sections.Single(item => item.SectionKey == "executive");
        executive.Signals.Should().Contain(item => item.Label == "Units" && item.Count == 1);
        executive.Signals.Should().Contain(item => item.Label == "Residents" && item.Count == 1);
    }

    [Fact]
    public async Task GetBuyerPresentationPackAsync_ReturnsSalesReadyNarrative()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedPresentationScenarioAsync(dbContext, "DEMO-BUYER");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetBuyerPresentationPackAsync(new CommercialPresentationQuery
        {
            CompoundId = compound.Id
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ProductOneLiner.ToLowerInvariant().Should().Contain("commercial backend");
        result.Value.FeatureBuckets.Should().Contain(item => item.Bucket == "Financial governance");
        result.Value.ObjectionHandling.Should().Contain(item => item.Objection.Contains("backend", StringComparison.OrdinalIgnoreCase));
    }

    private static CommercialPresentationService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new CommercialPresentationService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<Compound> SeedPresentationScenarioAsync(ApplicationDbContext dbContext, string code)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var compound = await AddCompoundAsync(dbContext, code);
        var unit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            UnitNumber = $"{code}-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var resident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = $"Resident {code}",
            PhoneNumber = "07700000000"
        };
        var occupancy = new OccupancyRecord
        {
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-3),
            ContractNumber = $"CNT-{code}"
        };
        var cycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(7)
        };
        var service = new CompoundService
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ServiceType = UtilityServiceType.Electricity,
            Name = "Demo electricity",
            DefaultMonthlyFee = 75000,
            IsMeterBased = true
        };
        var bill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = cycle.Id,
            BillNumber = $"BILL-{code}",
            BillStatus = BillStatus.PartiallyPaid,
            IssueDate = today.AddDays(-10),
            DueDate = today.AddDays(-1),
            SubtotalAmount = 125000,
            PreviousBalanceAmount = 20000,
            TotalAmount = 145000,
            PaidAmount = 50000
        };

        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.OccupancyRecords.Add(occupancy);
        dbContext.BillingCycles.Add(cycle);
        dbContext.CompoundServices.Add(service);
        dbContext.UtilityBills.Add(bill);
        dbContext.UtilityBillLines.Add(new UtilityBillLine
        {
            UtilityBillId = bill.Id,
            CompoundServiceId = service.Id,
            Description = "Demo electricity charge",
            Quantity = 1,
            UnitPrice = 125000,
            LineTotal = 125000
        });
        dbContext.Payments.Add(new Payment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = bill.Id,
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 50000,
            PaymentReference = $"PAY-{code}",
            CompletedAt = now.AddDays(-2)
        });
        dbContext.MaintenanceRequests.Add(new MaintenanceRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            Title = "Demo AC issue",
            Description = "Demo maintenance story",
            Priority = MaintenancePriority.High,
            Status = MaintenanceStatus.InProgress,
            CreatedAt = now.AddDays(-1)
        });
        dbContext.WorkOrders.Add(new WorkOrder
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            Title = "Demo work order",
            Description = "Buyer demo work order",
            Priority = WorkOrderPriority.High,
            Status = WorkOrderStatus.InProgress
        });
        dbContext.VisitorPasses.Add(new VisitorPass
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            VisitorName = "Demo Visitor",
            VisitorPhoneNumber = "07700000001",
            VisitReason = "Buyer demo",
            AccessCode = "DEMO01",
            Status = VisitorPassStatus.Approved,
            ValidFrom = now.AddHours(-1),
            ValidUntil = now.AddHours(3)
        });
        dbContext.Announcements.Add(new Announcement
        {
            CompoundId = compound.Id,
            Title = "Demo outage update",
            Body = "Water outage update for buyer demo.",
            Status = AnnouncementStatus.Published,
            Priority = AnnouncementPriority.High,
            PublishedAt = now.AddHours(-2)
        });
        dbContext.UtilityOutages.Add(new UtilityOutage
        {
            CompoundId = compound.Id,
            ServiceType = UtilityOutageServiceType.Water,
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.High,
            Title = "Demo water outage",
            Description = "Buyer demo outage",
            EstimatedStartAtUtc = now.AddHours(-3),
            EstimatedEndAtUtc = now.AddHours(2),
            RecipientCount = 25
        });
        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Stage = CollectionStage.FirstNotice,
            Status = CollectionCaseStatus.Open,
            AmountDue = 95000,
            DueDate = today.AddDays(-1),
            Reason = "Demo overdue balance"
        });
        dbContext.LicenseProfiles.Add(new LicenseProfile
        {
            LicensedTo = "DARAK Demo Buyer",
            LicenseKeyFingerprint = $"DEMO-{code}",
            Plan = LicensePlan.Professional,
            Status = LicenseStatus.Trial,
            MaxCompounds = 3,
            MaxUnits = 300,
            ExpiresAtUtc = now.AddDays(30)
        });

        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Demo District"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }
}
