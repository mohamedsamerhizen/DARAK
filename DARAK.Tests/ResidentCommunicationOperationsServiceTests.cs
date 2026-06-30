using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ResidentCommunicationOperationsServiceTests
{
    [Fact]
    public async Task CreateUtilityOutageAsync_CreatesAnnouncementAndNotificationsForAffectedResidents()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OUT-1");
        var building = await AddBuildingAsync(dbContext, compound.Id, "B1");
        var floor = await AddFloorAsync(dbContext, compound.Id, building.Id, 1);
        var unit = await AddUnitAsync(dbContext, compound.Id, building.Id, floor.Id, "101");
        var resident = await AddResidentWithOccupancyAsync(dbContext, compound.Id, unit.Id, "Outage Resident");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateUtilityOutageAsync(Guid.NewGuid(), new CreateUtilityOutageRequest
        {
            CompoundId = compound.Id,
            BuildingId = building.Id,
            FloorId = floor.Id,
            PropertyUnitId = unit.Id,
            AffectedScope = UtilityOutageAffectedScope.Unit,
            ServiceType = UtilityOutageServiceType.Water,
            Severity = UtilityOutageSeverity.High,
            Title = "Water interruption",
            Description = "Water will be interrupted for pump maintenance.",
            NotifyResidents = true,
            PublishAnnouncement = true
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Outage.AnnouncementId.Should().NotBeNull();
        result.Value.Outage.RecipientCount.Should().Be(1);
        dbContext.Announcements.Should().ContainSingle(item => item.Title == "Water interruption");
        dbContext.ResidentNotifications.Should().ContainSingle(item => item.UserId == resident.UserId);
        dbContext.NotificationOutboxes.Should().ContainSingle(item => item.RelatedEntityId == result.Value.Outage.Id);
    }

    [Fact]
    public async Task CreateUtilityOutageAsync_RespectsOptionalOptOutButCriticalBypassesIt()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OUT-PREF");
        var building = await AddBuildingAsync(dbContext, compound.Id, "B1");
        var floor = await AddFloorAsync(dbContext, compound.Id, building.Id, 1);
        var unit = await AddUnitAsync(dbContext, compound.Id, building.Id, floor.Id, "101");
        var resident = await AddResidentWithOccupancyAsync(dbContext, compound.Id, unit.Id, "Muted Outage Resident");
        dbContext.ResidentNotificationPreferences.Add(new ResidentNotificationPreference
        {
            UserId = resident.UserId,
            InAppEnabled = false,
            AnnouncementNotificationsEnabled = false
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var optional = await service.CreateUtilityOutageAsync(Guid.NewGuid(), new CreateUtilityOutageRequest
        {
            CompoundId = compound.Id,
            BuildingId = building.Id,
            FloorId = floor.Id,
            PropertyUnitId = unit.Id,
            AffectedScope = UtilityOutageAffectedScope.Unit,
            ServiceType = UtilityOutageServiceType.Water,
            Severity = UtilityOutageSeverity.Medium,
            Title = "Optional water notice",
            Description = "A medium severity water interruption.",
            NotifyResidents = true
        });
        var critical = await service.CreateUtilityOutageAsync(Guid.NewGuid(), new CreateUtilityOutageRequest
        {
            CompoundId = compound.Id,
            BuildingId = building.Id,
            FloorId = floor.Id,
            PropertyUnitId = unit.Id,
            AffectedScope = UtilityOutageAffectedScope.Unit,
            ServiceType = UtilityOutageServiceType.Water,
            Severity = UtilityOutageSeverity.Critical,
            Title = "Critical water outage",
            Description = "A critical water interruption.",
            NotifyResidents = true
        });

        optional.IsSuccess.Should().BeTrue(optional.Message);
        optional.Value!.Outage.OutboxItemCount.Should().Be(0);
        critical.IsSuccess.Should().BeTrue(critical.Message);
        critical.Value!.Outage.OutboxItemCount.Should().Be(1);
        dbContext.ResidentNotifications.Should().ContainSingle(item => item.Title == "Critical water outage");
        dbContext.ResidentNotifications.Should().NotContain(item => item.Title == "Optional water notice");
    }

    [Fact]
    public async Task SearchResidentUtilityOutagesAsync_ReturnsOnlyResidentAffectedOutages()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OUT-2");
        var buildingA = await AddBuildingAsync(dbContext, compound.Id, "A");
        var buildingB = await AddBuildingAsync(dbContext, compound.Id, "B");
        var floorA = await AddFloorAsync(dbContext, compound.Id, buildingA.Id, 1);
        var floorB = await AddFloorAsync(dbContext, compound.Id, buildingB.Id, 1);
        var unitA = await AddUnitAsync(dbContext, compound.Id, buildingA.Id, floorA.Id, "A-101");
        var unitB = await AddUnitAsync(dbContext, compound.Id, buildingB.Id, floorB.Id, "B-101");
        var resident = await AddResidentWithOccupancyAsync(dbContext, compound.Id, unitA.Id, "Resident A");
        var service = CreateService(dbContext, compound.Id);

        dbContext.UtilityOutages.AddRange(
            new UtilityOutage
            {
                CompoundId = compound.Id,
                BuildingId = buildingA.Id,
                ServiceType = UtilityOutageServiceType.Electricity,
                AffectedScope = UtilityOutageAffectedScope.Building,
                Status = UtilityOutageStatus.Active,
                Severity = UtilityOutageSeverity.Medium,
                Title = "Building A outage",
                Description = "Affected building A."
            },
            new UtilityOutage
            {
                CompoundId = compound.Id,
                BuildingId = buildingB.Id,
                ServiceType = UtilityOutageServiceType.Electricity,
                AffectedScope = UtilityOutageAffectedScope.Building,
                Status = UtilityOutageStatus.Active,
                Severity = UtilityOutageSeverity.Medium,
                Title = "Building B outage",
                Description = "Affected building B."
            });
        await dbContext.SaveChangesAsync();

        var result = await service.SearchResidentUtilityOutagesAsync(resident.UserId, new UtilityOutageQueryRequest());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.Single().Title.Should().Be("Building A outage");
    }

    [Fact]
    public async Task ResolveUtilityOutageAsync_ClosesOutageAndCreatesUpdate()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OUT-3");
        var outage = new UtilityOutage
        {
            CompoundId = compound.Id,
            ServiceType = UtilityOutageServiceType.Internet,
            AffectedScope = UtilityOutageAffectedScope.Compound,
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.Critical,
            Title = "Internet outage",
            Description = "Internet provider outage."
        };
        dbContext.UtilityOutages.Add(outage);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.ResolveUtilityOutageAsync(outage.Id, Guid.NewGuid(), new ResolveUtilityOutageRequest
        {
            ResolutionNotes = "Provider confirmed restoration.",
            NotifyResidents = false
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Outage.Status.Should().Be(UtilityOutageStatus.Resolved);
        result.Value.Updates.Should().ContainSingle(update => update.UpdateType == UtilityOutageUpdateType.Resolved);
        dbContext.UtilityOutages.Single().ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAdminSummaryAsync_ReturnsScopedOutageCounts()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "OUT-4A");
        var denied = await AddCompoundAsync(dbContext, "OUT-4B");
        dbContext.UtilityOutages.AddRange(
            new UtilityOutage
            {
                CompoundId = allowed.Id,
                ServiceType = UtilityOutageServiceType.Water,
                AffectedScope = UtilityOutageAffectedScope.Compound,
                Status = UtilityOutageStatus.Active,
                Severity = UtilityOutageSeverity.Critical,
                Title = "Allowed outage",
                Description = "Allowed outage body."
            },
            new UtilityOutage
            {
                CompoundId = denied.Id,
                ServiceType = UtilityOutageServiceType.Water,
                AffectedScope = UtilityOutageAffectedScope.Compound,
                Status = UtilityOutageStatus.Active,
                Severity = UtilityOutageSeverity.Critical,
                Title = "Denied outage",
                Description = "Denied outage body."
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var summary = await service.GetAdminSummaryAsync(null);

        summary.IsSuccess.Should().BeTrue(summary.Message);
        summary.Value!.ActiveOutageCount.Should().Be(1);
        summary.Value.CriticalOutageCount.Should().Be(1);
    }

    private static ResidentCommunicationOperationsService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new ResidentCommunicationOperationsService(dbContext, new FakeCompoundAccessService(allowedCompoundIds));
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
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 1
        };
        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<ResidentProfile> AddResidentWithOccupancyAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid unitId,
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
        dbContext.OccupancyRecords.Add(new OccupancyRecord
        {
            CompoundId = compoundId,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unitId,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        });
        await dbContext.SaveChangesAsync();
        return resident;
    }
}
