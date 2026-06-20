using DARAK.Api.Data;
using DARAK.Api.DTOs.Commercial;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertyUnits;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase5AOwnershipOccupancyLifecycleTests
{
    [Fact]
    public async Task CreateOwnershipTransferAsync_RejectsResidentWhoIsNotCurrentOwner()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedSoldUnitAsync(dbContext, "5A-OT-NOTOWNER");
        var nonOwner = await AddResidentAsync(dbContext, seed.Compound.Id, "Not Owner");
        var newOwner = await AddResidentAsync(dbContext, seed.Compound.Id, "New Owner");
        var service = CreateCommercialService(dbContext, seed.Compound.Id);

        var result = await service.CreateOwnershipTransferAsync(Guid.NewGuid(), new CreateOwnershipTransferRequest
        {
            PropertyUnitId = seed.Unit.Id,
            CurrentOwnerResidentProfileId = nonOwner.Id,
            NewOwnerResidentProfileId = newOwner.Id,
            RequestedTransferDate = new DateOnly(2026, 6, 1),
            Reason = "Attempted transfer by non-owner"
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.OwnershipTransferRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOwnershipTransferAsync_RejectsDuplicatePendingTransferForUnit()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedSoldUnitAsync(dbContext, "5A-OT-DUP");
        var newOwner = await AddResidentAsync(dbContext, seed.Compound.Id, "New Owner");
        var service = CreateCommercialService(dbContext, seed.Compound.Id);

        var first = await service.CreateOwnershipTransferAsync(Guid.NewGuid(), new CreateOwnershipTransferRequest
        {
            PropertyUnitId = seed.Unit.Id,
            CurrentOwnerResidentProfileId = seed.Owner.Id,
            NewOwnerResidentProfileId = newOwner.Id,
            RequestedTransferDate = new DateOnly(2026, 6, 1),
            Reason = "First transfer"
        });

        var second = await service.CreateOwnershipTransferAsync(Guid.NewGuid(), new CreateOwnershipTransferRequest
        {
            PropertyUnitId = seed.Unit.Id,
            CurrentOwnerResidentProfileId = seed.Owner.Id,
            NewOwnerResidentProfileId = newOwner.Id,
            RequestedTransferDate = new DateOnly(2026, 7, 1),
            Reason = "Duplicate transfer"
        });

        first.IsSuccess.Should().BeTrue(first.Message);
        second.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.OwnershipTransferRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task ApproveOwnershipTransferAsync_UpdatesOwnerObligationsOccupancyAndLifecycleContractLink()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedSoldUnitAsync(dbContext, "5A-OT-APPLY");
        var newOwner = await AddResidentAsync(dbContext, seed.Compound.Id, "New Owner");
        var service = CreateCommercialService(dbContext, seed.Compound.Id);

        var created = await service.CreateOwnershipTransferAsync(Guid.NewGuid(), new CreateOwnershipTransferRequest
        {
            PropertyUnitId = seed.Unit.Id,
            CurrentOwnerResidentProfileId = seed.Owner.Id,
            NewOwnerResidentProfileId = newOwner.Id,
            RequestedTransferDate = new DateOnly(2026, 6, 1),
            Reason = "Legal sale transfer"
        });

        var result = await service.ApproveOwnershipTransferAsync(Guid.NewGuid(), created.Value!.Id, new DecideOwnershipTransferRequest
        {
            Reason = "Documents verified"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(OwnershipTransferStatus.Completed);

        var saleContract = await dbContext.PropertySaleContracts.FindAsync(seed.SaleContract.Id);
        saleContract!.ResidentProfileId.Should().Be(newOwner.Id);

        var unpaidInstallment = await dbContext.InstallmentScheduleItems.FindAsync(seed.UnpaidInstallment.Id);
        unpaidInstallment!.ResidentProfileId.Should().Be(newOwner.Id);

        var oldOccupancy = await dbContext.OccupancyRecords.FirstAsync(record => record.ResidentProfileId == seed.Owner.Id);
        oldOccupancy.OccupancyStatus.Should().Be(OccupancyStatus.Ended);

        dbContext.OccupancyRecords.Should().ContainSingle(record =>
            record.PropertyUnitId == seed.Unit.Id
            && record.ResidentProfileId == newOwner.Id
            && record.OccupancyStatus == OccupancyStatus.Active);

        dbContext.ContractLifecycleEvents.Should().ContainSingle(item =>
            item.EventType == ContractLifecycleEventType.OwnershipTransferred
            && item.ContractId == seed.SaleContract.Id);
    }

    [Fact]
    public async Task CreateRentContractAsync_CreatesTenantOccupancyRecord()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAvailableUnitAsync(dbContext, "5A-RC-CREATE");
        var service = new RentContractService(dbContext, new FakeCompoundAccessService([seed.Compound.Id]));

        var result = await service.CreateRentContractAsync(new CreateRentContractRequest
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = "RC-5A-CREATE",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500000m,
            DepositAmount = 100000m
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        dbContext.OccupancyRecords.Should().ContainSingle(record =>
            record.PropertyUnitId == seed.Unit.Id
            && record.ResidentProfileId == seed.Resident.Id
            && record.OccupancyType == OccupancyType.Tenant
            && record.OccupancyStatus == OccupancyStatus.Active);
    }

    [Fact]
    public async Task TerminateRentContractAsync_EndsLinkedTenantOccupancy()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAvailableUnitAsync(dbContext, "5A-RC-END");
        var service = new RentContractService(dbContext, new FakeCompoundAccessService([seed.Compound.Id]));
        var created = await service.CreateRentContractAsync(new CreateRentContractRequest
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = "RC-5A-END",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500000m,
            DepositAmount = 100000m
        });

        var result = await service.TerminateRentContractAsync(created.Value!.Id, new TerminateRentContractRequest
        {
            TerminationDate = new DateOnly(2026, 6, 30),
            Reason = "Resident moved out"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        var occupancy = await dbContext.OccupancyRecords.SingleAsync(record => record.PropertyUnitId == seed.Unit.Id);
        occupancy.OccupancyStatus.Should().Be(OccupancyStatus.Ended);
        occupancy.EndDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task DeactivatePropertyUnitAsync_RejectsActiveOwnershipContract()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedSoldUnitAsync(dbContext, "5A-DEACT");
        var service = new CompoundStructureService(dbContext, new FakeCompoundAccessService([seed.Compound.Id]));

        var result = await service.DeactivatePropertyUnitAsync(seed.Unit.Id);

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        var unit = await dbContext.PropertyUnits.FindAsync(seed.Unit.Id);
        unit!.IsActive.Should().BeTrue();
    }

    private static CommercialEngineService CreateCommercialService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new CommercialEngineService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
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

    private static async Task<PropertyUnit> AddUnitAsync(ApplicationDbContext dbContext, Guid compoundId, string unitNumber, UnitStatus unitStatus = UnitStatus.Available)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = unitStatus,
            AreaSquareMeters = 100m,
            Bedrooms = 2,
            Bathrooms = 1
        };
        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = Guid.NewGuid(),
            FullName = fullName,
            PhoneNumber = "07700000000"
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, ResidentProfile Resident)> SeedAvailableUnitAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = await AddCompoundAsync(dbContext, code);
        var unit = await AddUnitAsync(dbContext, compound.Id, $"U-{code}");
        var resident = await AddResidentAsync(dbContext, compound.Id, $"Resident {code}");
        return (compound, unit, resident);
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, ResidentProfile Owner, PropertySaleContract SaleContract, InstallmentScheduleItem UnpaidInstallment)> SeedSoldUnitAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = await AddCompoundAsync(dbContext, code);
        var unit = await AddUnitAsync(dbContext, compound.Id, $"U-{code}", UnitStatus.SoldInstallment);
        var owner = await AddResidentAsync(dbContext, compound.Id, $"Owner {code}");
        var contract = new PropertySaleContract
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = owner.Id,
            ContractNumber = $"SC-{code}",
            SaleType = SaleType.Installment,
            ContractStatus = SaleContractStatus.Active,
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 100000000m,
            DownPaymentAmount = 10000000m,
            InstallmentCount = 12,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 1)
        };
        dbContext.PropertySaleContracts.Add(contract);
        await dbContext.SaveChangesAsync();

        var installment = new InstallmentScheduleItem
        {
            CompoundId = compound.Id,
            PropertySaleContractId = contract.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = owner.Id,
            InstallmentNumber = 1,
            DueDate = new DateOnly(2026, 2, 1),
            Amount = 1000m,
            InstallmentStatus = InstallmentStatus.Pending
        };
        dbContext.InstallmentScheduleItems.Add(installment);

        dbContext.OccupancyRecords.Add(new OccupancyRecord
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = owner.Id,
            OccupancyType = OccupancyType.OwnerInstallment,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            ContractNumber = contract.ContractNumber
        });

        await dbContext.SaveChangesAsync();
        return (compound, unit, owner, contract, installment);
    }
}
