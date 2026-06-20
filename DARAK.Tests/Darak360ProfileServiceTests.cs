using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class Darak360ProfileServiceTests
{
    [Fact]
    public async Task GetResident360ProfileAsync_ReturnsFinancialOperationsAndRiskContext()
    {
        await using var dbContext = TestDb.Create();
        var seeded = await Seed360ScenarioAsync(dbContext, "360-RES");
        var service = CreateService(dbContext, seeded.Compound.Id);

        var result = await service.GetResident360ProfileAsync(seeded.Resident.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ResidentId.Should().Be(seeded.Resident.Id);
        result.Value.CurrentUnit!.UnitId.Should().Be(seeded.Unit.Id);
        result.Value.FinancialSnapshot.OutstandingAmount.Should().Be(120000);
        result.Value.OperationsSnapshot.MaintenanceRequests.Should().Be(1);
        result.Value.LegalRiskSnapshot.ActiveRiskFlags.Should().Be(1);
        result.Value.Signals.Should().Contain(item => item.SignalKey == "resident-outstanding-balance");
        result.Value.RecommendedActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUnit360ProfileAsync_ReturnsCurrentResidentAndTurnoverSignals()
    {
        await using var dbContext = TestDb.Create();
        var seeded = await Seed360ScenarioAsync(dbContext, "360-UNIT");
        var service = CreateService(dbContext, seeded.Compound.Id);

        var result = await service.GetUnit360ProfileAsync(seeded.Unit.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.UnitId.Should().Be(seeded.Unit.Id);
        result.Value.CurrentResident!.ResidentId.Should().Be(seeded.Resident.Id);
        result.Value.LifecycleSnapshot.OpenDamageLiabilities.Should().Be(1);
        result.Value.LifecycleSnapshot.LatestReadinessStatus.Should().Be(UnitReadinessStatus.Blocked.ToString());
        result.Value.Signals.Should().Contain(item => item.SignalKey == "unit-turnover-blocker");
    }

    [Fact]
    public async Task GetCompound360OverviewAsync_AggregatesOnlyAllowedCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await Seed360ScenarioAsync(dbContext, "360-ALLOW");
        var other = await Seed360ScenarioAsync(dbContext, "360-OTHER");
        var service = CreateService(dbContext, allowed.Compound.Id);

        var allowedResult = await service.GetCompound360OverviewAsync(allowed.Compound.Id);
        var forbiddenResult = await service.GetCompound360OverviewAsync(other.Compound.Id);

        allowedResult.IsSuccess.Should().BeTrue(allowedResult.Message);
        allowedResult.Value!.InventorySnapshot.Units.Should().Be(1);
        allowedResult.Value.InventorySnapshot.Residents.Should().Be(1);
        allowedResult.Value.FinancialSnapshot.OutstandingAmount.Should().Be(120000);
        forbiddenResult.IsSuccess.Should().BeFalse();
        forbiddenResult.Message!.ToLowerInvariant().Should().Contain("access");
    }

    private static Darak360ProfileService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new Darak360ProfileService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<Seeded360Scenario> Seed360ScenarioAsync(ApplicationDbContext dbContext, string code)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var occupancy = new OccupancyRecord
        {
            Id = Guid.NewGuid(),
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-6),
            ContractNumber = $"CNT-{code}"
        };
        var billingCycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(5)
        };
        var bill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = $"BILL-{code}",
            BillStatus = BillStatus.PartiallyPaid,
            IssueDate = today.AddDays(-10),
            DueDate = today.AddDays(-1),
            SubtotalAmount = 200000,
            TotalAmount = 200000,
            PaidAmount = 80000
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = bill.Id,
            PaymentMethod = PaymentMethod.ManualAdminPayment,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 80000,
            PaymentReference = $"PAY-{code}",
            CompletedAt = DateTime.UtcNow
        };
        var maintenance = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            Title = "Water leak",
            Description = "Demo leak",
            Priority = MaintenancePriority.High,
            Status = MaintenanceStatus.Open
        };
        var readiness = new UnitReadinessRecord
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            Status = UnitReadinessStatus.Blocked,
            Notes = "Demo blocker"
        };
        var damage = new UnitDamageLiability
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            Status = DamageLiabilityStatus.Charged,
            EstimatedAmount = 45000,
            Description = "Door damage"
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
            Description = "Outstanding balance",
            RecommendedAction = "Finance follow-up"
        };

        dbContext.Compounds.Add(compound);
        dbContext.Buildings.Add(building);
        dbContext.Floors.Add(floor);
        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.OccupancyRecords.Add(occupancy);
        dbContext.BillingCycles.Add(billingCycle);
        dbContext.UtilityBills.Add(bill);
        dbContext.Payments.Add(payment);
        dbContext.MaintenanceRequests.Add(maintenance);
        dbContext.UnitReadinessRecords.Add(readiness);
        dbContext.UnitDamageLiabilities.Add(damage);
        dbContext.ResidentRiskFlags.Add(riskFlag);
        await dbContext.SaveChangesAsync();

        return new Seeded360Scenario(compound, unit, resident);
    }

    private sealed record Seeded360Scenario(
        Compound Compound,
        PropertyUnit Unit,
        ResidentProfile Resident);
}

