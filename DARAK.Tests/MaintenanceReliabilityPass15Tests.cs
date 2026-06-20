using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class MaintenanceReliabilityPass15Tests
{
    [Fact]
    public async Task Pass15_CreatePreventivePlanAsync_RejectsBothStaffAndVendor()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P15-BOTH");
        var asset = await AddAssetAsync(dbContext, compound.Id, "P15-ASSET-BOTH");
        var staff = new StaffMember
        {
            FullName = "Preventive Technician",
            PhoneNumber = "07700000001",
            StaffType = StaffType.MaintenanceTechnician
        };
        var vendor = new ServiceVendor
        {
            Name = "Preventive Vendor",
            PhoneNumber = "07700000002",
            ServiceType = VendorServiceType.Maintenance
        };
        dbContext.StaffMembers.Add(staff);
        dbContext.ServiceVendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreatePreventivePlanAsync(new CreatePreventiveMaintenancePlanRequest
        {
            MaintenanceAssetId = asset.Id,
            Title = "Monthly mixed assignment check",
            Description = "Should not allow both staff and vendor.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.Normal,
            AssignedStaffMemberId = staff.Id,
            AssignedVendorId = vendor.Id,
            NextDueAtUtc = DateTime.UtcNow.AddDays(3)
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("cannot assign both staff and vendor");
        dbContext.PreventiveMaintenancePlans.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass15_CreatePreventivePlanAsync_RejectsDefaultNextDueDate()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P15-DUE");
        var asset = await AddAssetAsync(dbContext, compound.Id, "P15-ASSET-DUE");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreatePreventivePlanAsync(new CreatePreventiveMaintenancePlanRequest
        {
            MaintenanceAssetId = asset.Id,
            Title = "Missing due date check",
            Description = "Default DateTime should be rejected.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.High,
            NextDueAtUtc = default
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("next due date");
        dbContext.PreventiveMaintenancePlans.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass15_GeneratePreventiveWorkOrderAsync_RejectsDueBeforeSchedule()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P15-SCHED");
        var asset = await AddAssetAsync(dbContext, compound.Id, "P15-ASSET-SCHED");
        var service = CreateService(dbContext, compound.Id);
        var plan = await service.CreatePreventivePlanAsync(new CreatePreventiveMaintenancePlanRequest
        {
            MaintenanceAssetId = asset.Id,
            Title = "Pump scheduled inspection",
            Description = "Generated due date must not be before scheduled date.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.High,
            NextDueAtUtc = DateTime.UtcNow.AddDays(1)
        });
        var scheduledAtUtc = DateTime.UtcNow.AddDays(2);

        var result = await service.GeneratePreventiveWorkOrderAsync(
            plan.Value!.Id,
            Guid.NewGuid(),
            new GeneratePreventiveWorkOrderRequest
            {
                ScheduledAtUtc = scheduledAtUtc,
                DueAtUtc = scheduledAtUtc.AddHours(-1)
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("due date cannot be before the scheduled date");
        dbContext.WorkOrders.Should().BeEmpty();
        dbContext.PreventiveMaintenancePlans.Single().LastGeneratedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Pass15_CreateAssetAsync_RejectsFloorFromDifferentBuilding()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P15-FLOOR");
        var firstBuilding = await AddBuildingAsync(dbContext, compound.Id, "B1");
        var secondBuilding = await AddBuildingAsync(dbContext, compound.Id, "B2");
        var floorOnSecondBuilding = await AddFloorAsync(dbContext, compound.Id, secondBuilding.Id, 2);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            BuildingId = firstBuilding.Id,
            FloorId = floorOnSecondBuilding.Id,
            Name = "Mismatched floor asset",
            Code = "P15-FLOOR-MISMATCH",
            AssetType = MaintenanceAssetType.Other
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("floor does not belong to the selected building");
        dbContext.MaintenanceAssets.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass15_CreateAssetAsync_RejectsUnitFromDifferentFloor()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P15-UNIT");
        var building = await AddBuildingAsync(dbContext, compound.Id, "B1");
        var firstFloor = await AddFloorAsync(dbContext, compound.Id, building.Id, 1);
        var secondFloor = await AddFloorAsync(dbContext, compound.Id, building.Id, 2);
        var unitOnSecondFloor = await AddUnitAsync(dbContext, compound.Id, building.Id, secondFloor.Id, "201");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            BuildingId = building.Id,
            FloorId = firstFloor.Id,
            PropertyUnitId = unitOnSecondFloor.Id,
            Name = "Mismatched unit asset",
            Code = "P15-UNIT-MISMATCH",
            AssetType = MaintenanceAssetType.Other
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("unit does not belong to the selected floor");
        dbContext.MaintenanceAssets.Should().BeEmpty();
    }

    private static MaintenanceReliabilityService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new MaintenanceReliabilityService(dbContext, new FakeCompoundAccessService([compoundId]));
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

    private static async Task<MaintenanceAsset> AddAssetAsync(ApplicationDbContext dbContext, Guid compoundId, string code)
    {
        var asset = new MaintenanceAsset
        {
            CompoundId = compoundId,
            Name = $"Asset {code}",
            Code = code,
            AssetType = MaintenanceAssetType.Pump,
            Status = MaintenanceAssetStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.MaintenanceAssets.Add(asset);
        await dbContext.SaveChangesAsync();
        return asset;
    }

    private static async Task<Building> AddBuildingAsync(ApplicationDbContext dbContext, Guid compoundId, string code)
    {
        var building = new Building
        {
            CompoundId = compoundId,
            Name = $"Building {code}",
            Code = code,
            NumberOfFloors = 10
        };

        dbContext.Buildings.Add(building);
        await dbContext.SaveChangesAsync();
        return building;
    }

    private static async Task<Floor> AddFloorAsync(ApplicationDbContext dbContext, Guid compoundId, Guid buildingId, int floorNumber)
    {
        var floor = new Floor
        {
            CompoundId = compoundId,
            BuildingId = buildingId,
            FloorNumber = floorNumber,
            Name = $"Floor {floorNumber}"
        };

        dbContext.Floors.Add(floor);
        await dbContext.SaveChangesAsync();
        return floor;
    }

    private static async Task<PropertyUnit> AddUnitAsync(ApplicationDbContext dbContext, Guid compoundId, Guid buildingId, Guid floorId, string unitNumber)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            BuildingId = buildingId,
            FloorId = floorId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 2
        };

        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }
}
