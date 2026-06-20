using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.ResidentLifecycle;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ResidentLifecycleContractBlockersPass14Tests
{
    [Fact]
    public async Task CompleteMoveOutAsync_BlocksWhenActiveRentContractExists()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC14-RENT");
        var service = CreateService(dbContext, seed.Compound.Id);

        dbContext.RentContracts.Add(new RentContract
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = "RC-LC14-001",
            ContractStatus = RentContractStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500000,
            DepositAmount = 250000
        });
        await dbContext.SaveChangesAsync();

        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 9, 1),
                FinancialClearanceRequired = false
            });

        var result = await service.CompleteProcessAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("Active rent contracts");
        dbContext.OccupancyRecords.Single(item => item.Id == seed.Occupancy.Id).OccupancyStatus.Should().Be(OccupancyStatus.Active);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.Occupied);
    }

    [Fact]
    public async Task CompleteMoveOutAsync_BlocksWhenActiveSaleContractExists()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC14-SALE");
        var service = CreateService(dbContext, seed.Compound.Id);

        dbContext.PropertySaleContracts.Add(new PropertySaleContract
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = "SC-LC14-001",
            SaleType = SaleType.Cash,
            ContractStatus = SaleContractStatus.Active,
            ContractDate = new DateOnly(2026, 1, 15),
            PropertyPrice = 150000000,
            DownPaymentAmount = 150000000,
            InstallmentCount = 0
        });
        await dbContext.SaveChangesAsync();

        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 9, 2),
                FinancialClearanceRequired = false
            });

        var result = await service.CompleteProcessAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("Active sale contracts");
        dbContext.OccupancyRecords.Single(item => item.Id == seed.Occupancy.Id).OccupancyStatus.Should().Be(OccupancyStatus.Active);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.Occupied);
    }

    [Fact]
    public async Task GetMoveOutReadinessAsync_TreatsActiveSaleContractAsOperationalCompletionBlocker()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC14-READY");
        var service = CreateService(dbContext, seed.Compound.Id);

        dbContext.PropertySaleContracts.Add(new PropertySaleContract
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = "SC-LC14-READY",
            SaleType = SaleType.Installment,
            ContractStatus = SaleContractStatus.Active,
            ContractDate = new DateOnly(2026, 2, 1),
            PropertyPrice = 180000000,
            DownPaymentAmount = 40000000,
            InstallmentCount = 120,
            FirstInstallmentDueDate = new DateOnly(2026, 3, 1)
        });
        await dbContext.SaveChangesAsync();

        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 9, 3),
                FinancialClearanceRequired = false
            });

        var readiness = await service.GetMoveOutReadinessAsync(new MoveOutReadinessQuery
        {
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            AsOfDate = new DateOnly(2026, 8, 20)
        });

        readiness.IsSuccess.Should().BeTrue(readiness.Message);
        readiness.Value!.ActiveMoveOutProcessId.Should().Be(process.Value!.Id);
        readiness.Value.ActiveSaleContractCount.Should().Be(1);
        readiness.Value.HasOperationalBlockers.Should().BeTrue();
        readiness.Value.CanCompleteMoveOutNow.Should().BeFalse();
        readiness.Value.Blockers.Select(item => item.Code).Should().Contain("ACTIVE_SALE_CONTRACT");
    }

    private static ResidentLifecycleService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new ResidentLifecycleService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<SeedData> SeedAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };
        var resident = new ResidentProfile
        {
            CompoundId = compound.Id,
            UserId = Guid.NewGuid(),
            FullName = $"Resident {code}",
            PhoneNumber = "07700000000"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = $"U-{code}",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 1,
            IsActive = true
        };
        var occupancy = new OccupancyRecord
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };

        dbContext.Compounds.Add(compound);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.PropertyUnits.Add(unit);
        dbContext.OccupancyRecords.Add(occupancy);
        await dbContext.SaveChangesAsync();

        return new SeedData(compound, resident, unit, occupancy);
    }

    private sealed record SeedData(Compound Compound, ResidentProfile Resident, PropertyUnit Unit, OccupancyRecord Occupancy);
}
