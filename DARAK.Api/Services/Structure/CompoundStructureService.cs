using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Buildings;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Compounds;
using DARAK.Api.DTOs.Floors;
using DARAK.Api.DTOs.ParkingSpots;
using DARAK.Api.DTOs.PropertyUnits;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CompoundStructureService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : ICompoundStructureService
{
    private static readonly HashSet<UnitStatus> PhaseTwoUnitStatuses =
    [
        UnitStatus.Available,
        UnitStatus.UnderMaintenance,
        UnitStatus.Blocked
    ];

    public async Task<PagedResult<CompoundResponse>> SearchCompoundsAsync(
        CompoundSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var compounds = await ApplyCurrentCompoundScopeAsync(
            dbContext.Compounds.AsNoTracking(),
            compound => compound.Id,
            cancellationToken);

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            compounds = compounds.Where(compound =>
                compound.Name.Contains(searchTerm) || compound.Code.Contains(searchTerm));
        }

        if (HasText(query.City))
        {
            var city = query.City!.Trim();
            compounds = compounds.Where(compound => compound.City == city);
        }

        if (HasText(query.Area))
        {
            var area = query.Area!.Trim();
            compounds = compounds.Where(compound => compound.Area == area);
        }

        if (query.IsActive.HasValue)
        {
            compounds = compounds.Where(compound => compound.IsActive == query.IsActive.Value);
        }

        return await ToPagedResultAsync(
            compounds.OrderBy(compound => compound.Name),
            query,
            compound => new CompoundResponse(
                compound.Id,
                compound.Name,
                compound.Code,
                compound.Description,
                compound.City,
                compound.Area,
                compound.Address,
                compound.IsActive,
                compound.CreatedAt,
                compound.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<CompoundResponse>> GetCompoundAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var compounds = await ApplyCurrentCompoundScopeAsync(
            dbContext.Compounds.AsNoTracking(),
            compound => compound.Id,
            cancellationToken);

        var compound = await compounds
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return compound is null
            ? ServiceResult<CompoundResponse>.NotFound("Compound was not found.")
            : ServiceResult<CompoundResponse>.Success(ToCompoundResponse(compound));
    }

    public async Task<ServiceResult<CompoundResponse>> CreateCompoundAsync(
        CreateCompoundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanCreateCompoundAsync(cancellationToken))
        {
            return ServiceResult<CompoundResponse>.Forbidden("Only SuperAdmin can create compounds.");
        }

        var code = NormalizeCode(request.Code);
        if (await dbContext.Compounds.AnyAsync(compound => compound.Code == code, cancellationToken))
        {
            return ServiceResult<CompoundResponse>.Conflict("Compound code already exists.");
        }

        var compound = new Compound
        {
            Name = request.Name.Trim(),
            Code = code,
            Description = TrimOrNull(request.Description),
            City = request.City.Trim(),
            Area = request.Area.Trim(),
            Address = TrimOrNull(request.Address)
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CompoundResponse>.Success(ToCompoundResponse(compound));
    }

    public async Task<ServiceResult<CompoundResponse>> UpdateCompoundAsync(
        Guid id,
        UpdateCompoundRequest request,
        CancellationToken cancellationToken = default)
    {
        var compounds = await ApplyCurrentCompoundScopeAsync(
            dbContext.Compounds,
            compound => compound.Id,
            cancellationToken);

        var compound = await compounds
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (compound is null)
        {
            return ServiceResult<CompoundResponse>.NotFound("Compound was not found.");
        }

        var code = NormalizeCode(request.Code);
        var codeExists = await dbContext.Compounds.AnyAsync(
            item => item.Id != id && item.Code == code,
            cancellationToken);

        if (codeExists)
        {
            return ServiceResult<CompoundResponse>.Conflict("Compound code already exists.");
        }

        compound.Name = request.Name.Trim();
        compound.Code = code;
        compound.Description = TrimOrNull(request.Description);
        compound.City = request.City.Trim();
        compound.Area = request.Area.Trim();
        compound.Address = TrimOrNull(request.Address);
        compound.IsActive = request.IsActive;
        compound.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CompoundResponse>.Success(ToCompoundResponse(compound));
    }

    public async Task<ServiceResult<object?>> DeactivateCompoundAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var compounds = await ApplyCurrentCompoundScopeAsync(
            dbContext.Compounds,
            compound => compound.Id,
            cancellationToken);

        var compound = await compounds
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (compound is null)
        {
            return ServiceResult<object?>.NotFound("Compound was not found.");
        }

        compound.IsActive = false;
        compound.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<PagedResult<BuildingResponse>> SearchBuildingsAsync(
        BuildingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var buildings = await ApplyCurrentCompoundScopeAsync(
            dbContext.Buildings.AsNoTracking(),
            building => building.CompoundId,
            cancellationToken);

        if (query.CompoundId.HasValue)
        {
            buildings = buildings.Where(building => building.CompoundId == query.CompoundId.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            buildings = buildings.Where(building =>
                building.Name.Contains(searchTerm) || building.Code.Contains(searchTerm));
        }

        if (query.IsActive.HasValue)
        {
            buildings = buildings.Where(building => building.IsActive == query.IsActive.Value);
        }

        return await ToPagedResultAsync(
            buildings.OrderBy(building => building.Name),
            query,
            building => new BuildingResponse(
                building.Id,
                building.CompoundId,
                building.Name,
                building.Code,
                building.NumberOfFloors,
                building.IsActive,
                building.CreatedAt,
                building.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<BuildingResponse>> GetBuildingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var buildings = await ApplyCurrentCompoundScopeAsync(
            dbContext.Buildings.AsNoTracking(),
            building => building.CompoundId,
            cancellationToken);

        var building = await buildings
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return building is null
            ? ServiceResult<BuildingResponse>.NotFound("Building was not found.")
            : ServiceResult<BuildingResponse>.Success(ToBuildingResponse(building));
    }

    public async Task<ServiceResult<BuildingResponse>> CreateBuildingAsync(
        CreateBuildingRequest request,
        CancellationToken cancellationToken = default)
    {
        var compoundValidation = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (compoundValidation is not null)
        {
            return ToResult<BuildingResponse>(compoundValidation);
        }

        if (request.NumberOfFloors < 0)
        {
            return ServiceResult<BuildingResponse>.BadRequest("Number of floors cannot be negative.");
        }

        var code = NormalizeCode(request.Code);
        var duplicateExists = await dbContext.Buildings.AnyAsync(
            building => building.CompoundId == request.CompoundId && building.Code == code,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<BuildingResponse>.Conflict("Building code already exists in this compound.");
        }

        var building = new Building
        {
            CompoundId = request.CompoundId,
            Name = request.Name.Trim(),
            Code = code,
            NumberOfFloors = request.NumberOfFloors
        };

        dbContext.Buildings.Add(building);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BuildingResponse>.Success(ToBuildingResponse(building));
    }

    public async Task<ServiceResult<BuildingResponse>> UpdateBuildingAsync(
        Guid id,
        UpdateBuildingRequest request,
        CancellationToken cancellationToken = default)
    {
        var buildings = await ApplyCurrentCompoundScopeAsync(
            dbContext.Buildings,
            building => building.CompoundId,
            cancellationToken);

        var building = await buildings
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (building is null)
        {
            return ServiceResult<BuildingResponse>.NotFound("Building was not found.");
        }

        if (building.CompoundId != request.CompoundId)
        {
            return ServiceResult<BuildingResponse>.BadRequest("Building compound cannot be changed.");
        }

        var compoundValidation = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (compoundValidation is not null)
        {
            return ToResult<BuildingResponse>(compoundValidation);
        }

        if (request.NumberOfFloors < 0)
        {
            return ServiceResult<BuildingResponse>.BadRequest("Number of floors cannot be negative.");
        }

        var code = NormalizeCode(request.Code);
        var duplicateExists = await dbContext.Buildings.AnyAsync(
            item => item.Id != id && item.CompoundId == request.CompoundId && item.Code == code,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<BuildingResponse>.Conflict("Building code already exists in this compound.");
        }

        building.Name = request.Name.Trim();
        building.Code = code;
        building.NumberOfFloors = request.NumberOfFloors;
        building.IsActive = request.IsActive;
        building.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BuildingResponse>.Success(ToBuildingResponse(building));
    }

    public async Task<ServiceResult<object?>> DeactivateBuildingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var buildings = await ApplyCurrentCompoundScopeAsync(
            dbContext.Buildings,
            building => building.CompoundId,
            cancellationToken);

        var building = await buildings
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (building is null)
        {
            return ServiceResult<object?>.NotFound("Building was not found.");
        }

        building.IsActive = false;
        building.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<PagedResult<FloorResponse>> SearchFloorsAsync(
        FloorSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var floors = await ApplyCurrentCompoundScopeAsync(
            dbContext.Floors.AsNoTracking(),
            floor => floor.CompoundId,
            cancellationToken);

        if (query.CompoundId.HasValue)
        {
            floors = floors.Where(floor => floor.CompoundId == query.CompoundId.Value);
        }

        if (query.BuildingId.HasValue)
        {
            floors = floors.Where(floor => floor.BuildingId == query.BuildingId.Value);
        }

        if (query.IsActive.HasValue)
        {
            floors = floors.Where(floor => floor.IsActive == query.IsActive.Value);
        }

        return await ToPagedResultAsync(
            floors.OrderBy(floor => floor.BuildingId).ThenBy(floor => floor.FloorNumber),
            query,
            floor => new FloorResponse(
                floor.Id,
                floor.CompoundId,
                floor.BuildingId,
                floor.FloorNumber,
                floor.Name,
                floor.IsActive,
                floor.CreatedAt,
                floor.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<FloorResponse>> GetFloorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var floors = await ApplyCurrentCompoundScopeAsync(
            dbContext.Floors.AsNoTracking(),
            floor => floor.CompoundId,
            cancellationToken);

        var floor = await floors
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return floor is null
            ? ServiceResult<FloorResponse>.NotFound("Floor was not found.")
            : ServiceResult<FloorResponse>.Success(ToFloorResponse(floor));
    }

    public async Task<ServiceResult<FloorResponse>> CreateFloorAsync(
        CreateFloorRequest request,
        CancellationToken cancellationToken = default)
    {
        var buildingValidation = await ValidateActiveBuildingAsync(
            request.CompoundId,
            request.BuildingId,
            cancellationToken);

        if (buildingValidation is not null)
        {
            return ToResult<FloorResponse>(buildingValidation);
        }

        var duplicateExists = await dbContext.Floors.AnyAsync(
            floor => floor.BuildingId == request.BuildingId && floor.FloorNumber == request.FloorNumber,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<FloorResponse>.Conflict("Floor number already exists in this building.");
        }

        var floor = new Floor
        {
            CompoundId = request.CompoundId,
            BuildingId = request.BuildingId,
            FloorNumber = request.FloorNumber,
            Name = TrimOrNull(request.Name)
        };

        dbContext.Floors.Add(floor);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FloorResponse>.Success(ToFloorResponse(floor));
    }

    public async Task<ServiceResult<FloorResponse>> UpdateFloorAsync(
        Guid id,
        UpdateFloorRequest request,
        CancellationToken cancellationToken = default)
    {
        var floors = await ApplyCurrentCompoundScopeAsync(
            dbContext.Floors,
            floor => floor.CompoundId,
            cancellationToken);

        var floor = await floors
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (floor is null)
        {
            return ServiceResult<FloorResponse>.NotFound("Floor was not found.");
        }

        if (floor.CompoundId != request.CompoundId || floor.BuildingId != request.BuildingId)
        {
            return ServiceResult<FloorResponse>.BadRequest("Floor compound and building cannot be changed.");
        }

        var buildingValidation = await ValidateActiveBuildingAsync(
            request.CompoundId,
            request.BuildingId,
            cancellationToken);

        if (buildingValidation is not null)
        {
            return ToResult<FloorResponse>(buildingValidation);
        }

        var duplicateExists = await dbContext.Floors.AnyAsync(
            item => item.Id != id
                && item.BuildingId == request.BuildingId
                && item.FloorNumber == request.FloorNumber,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<FloorResponse>.Conflict("Floor number already exists in this building.");
        }

        floor.FloorNumber = request.FloorNumber;
        floor.Name = TrimOrNull(request.Name);
        floor.IsActive = request.IsActive;
        floor.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FloorResponse>.Success(ToFloorResponse(floor));
    }

    public async Task<ServiceResult<object?>> DeactivateFloorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var floors = await ApplyCurrentCompoundScopeAsync(
            dbContext.Floors,
            floor => floor.CompoundId,
            cancellationToken);

        var floor = await floors
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (floor is null)
        {
            return ServiceResult<object?>.NotFound("Floor was not found.");
        }

        floor.IsActive = false;
        floor.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<PagedResult<PropertyUnitResponse>> SearchPropertyUnitsAsync(
        PropertyUnitSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var propertyUnits = await ApplyCurrentCompoundScopeAsync(
            dbContext.PropertyUnits.AsNoTracking(),
            unit => unit.CompoundId,
            cancellationToken);

        if (query.CompoundId.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.CompoundId == query.CompoundId.Value);
        }

        if (query.BuildingId.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.BuildingId == query.BuildingId.Value);
        }

        if (query.FloorId.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.FloorId == query.FloorId.Value);
        }

        if (query.PropertyType.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.PropertyType == query.PropertyType.Value);
        }

        if (query.UnitStatus.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.UnitStatus == query.UnitStatus.Value);
        }

        if (query.MinArea.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.AreaSquareMeters >= query.MinArea.Value);
        }

        if (query.MaxArea.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.AreaSquareMeters <= query.MaxArea.Value);
        }

        if (query.Bedrooms.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.Bedrooms == query.Bedrooms.Value);
        }

        if (query.Bathrooms.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.Bathrooms == query.Bathrooms.Value);
        }

        if (query.HasParking.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.HasParking == query.HasParking.Value);
        }

        if (query.IsActive.HasValue)
        {
            propertyUnits = propertyUnits.Where(unit => unit.IsActive == query.IsActive.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            propertyUnits = propertyUnits.Where(unit => unit.UnitNumber.Contains(searchTerm));
        }

        return await ToPagedResultAsync(
            propertyUnits.OrderBy(unit => unit.UnitNumber),
            query,
            unit => new PropertyUnitResponse(
                unit.Id,
                unit.CompoundId,
                unit.BuildingId,
                unit.FloorId,
                unit.UnitNumber,
                unit.PropertyType,
                unit.UnitStatus,
                unit.AreaSquareMeters,
                unit.Bedrooms,
                unit.Bathrooms,
                unit.HasParking,
                unit.Notes,
                unit.IsActive,
                unit.CreatedAt,
                unit.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<PropertyUnitResponse>> GetPropertyUnitAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var propertyUnits = await ApplyCurrentCompoundScopeAsync(
            dbContext.PropertyUnits.AsNoTracking(),
            unit => unit.CompoundId,
            cancellationToken);

        var unit = await propertyUnits
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return unit is null
            ? ServiceResult<PropertyUnitResponse>.NotFound("Property unit was not found.")
            : ServiceResult<PropertyUnitResponse>.Success(ToPropertyUnitResponse(unit));
    }

    public async Task<ServiceResult<PropertyUnitResponse>> CreatePropertyUnitAsync(
        CreatePropertyUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePropertyUnitRequestAsync(
            request.CompoundId,
            request.BuildingId,
            request.FloorId,
            request.PropertyType,
            request.UnitStatus,
            request.AreaSquareMeters,
            request.Bedrooms,
            request.Bathrooms,
            cancellationToken);

        if (validation is not null)
        {
            return ToResult<PropertyUnitResponse>(validation);
        }

        var unitNumber = request.UnitNumber.Trim();
        var duplicateExists = await PropertyUnitDuplicateExistsAsync(
            request.CompoundId,
            request.BuildingId,
            unitNumber,
            excludedId: null,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<PropertyUnitResponse>.Conflict(
                "Property unit number already exists in this compound and building context.");
        }

        var unit = new PropertyUnit
        {
            CompoundId = request.CompoundId,
            BuildingId = request.BuildingId,
            FloorId = request.FloorId,
            UnitNumber = unitNumber,
            PropertyType = request.PropertyType,
            UnitStatus = request.UnitStatus,
            AreaSquareMeters = request.AreaSquareMeters,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            HasParking = request.HasParking,
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PropertyUnitResponse>.Success(ToPropertyUnitResponse(unit));
    }

    public async Task<ServiceResult<PropertyUnitResponse>> UpdatePropertyUnitAsync(
        Guid id,
        UpdatePropertyUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var propertyUnits = await ApplyCurrentCompoundScopeAsync(
            dbContext.PropertyUnits,
            unit => unit.CompoundId,
            cancellationToken);

        var unit = await propertyUnits
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (unit is null)
        {
            return ServiceResult<PropertyUnitResponse>.NotFound("Property unit was not found.");
        }

        var validation = await ValidatePropertyUnitRequestAsync(
            request.CompoundId,
            request.BuildingId,
            request.FloorId,
            request.PropertyType,
            request.UnitStatus,
            request.AreaSquareMeters,
            request.Bedrooms,
            request.Bathrooms,
            cancellationToken);

        if (validation is not null)
        {
            return ToResult<PropertyUnitResponse>(validation);
        }

        var unitNumber = request.UnitNumber.Trim();
        var duplicateExists = await PropertyUnitDuplicateExistsAsync(
            request.CompoundId,
            request.BuildingId,
            unitNumber,
            id,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<PropertyUnitResponse>.Conflict(
                "Property unit number already exists in this compound and building context.");
        }

        if (unit.IsActive && !request.IsActive)
        {
            var deactivationValidation = await ValidatePropertyUnitCanBeDeactivatedAsync(id, cancellationToken);
            if (deactivationValidation is not null)
            {
                return ToResult<PropertyUnitResponse>(deactivationValidation);
            }
        }

        unit.CompoundId = request.CompoundId;
        unit.BuildingId = request.BuildingId;
        unit.FloorId = request.FloorId;
        unit.UnitNumber = unitNumber;
        unit.PropertyType = request.PropertyType;
        unit.UnitStatus = request.UnitStatus;
        unit.AreaSquareMeters = request.AreaSquareMeters;
        unit.Bedrooms = request.Bedrooms;
        unit.Bathrooms = request.Bathrooms;
        unit.HasParking = request.HasParking;
        unit.Notes = TrimOrNull(request.Notes);
        unit.IsActive = request.IsActive;
        unit.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PropertyUnitResponse>.Success(ToPropertyUnitResponse(unit));
    }

    public async Task<ServiceResult<PropertyUnitResponse>> UpdatePropertyUnitStatusAsync(
        Guid id,
        UpdatePropertyUnitStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsPhaseTwoUnitStatus(request.UnitStatus))
        {
            return ServiceResult<PropertyUnitResponse>.BadRequest(
                "Property unit status is not allowed in Phase 2.");
        }

        var propertyUnits = await ApplyCurrentCompoundScopeAsync(
            dbContext.PropertyUnits,
            unit => unit.CompoundId,
            cancellationToken);

        var unit = await propertyUnits
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (unit is null)
        {
            return ServiceResult<PropertyUnitResponse>.NotFound("Property unit was not found.");
        }

        unit.UnitStatus = request.UnitStatus;
        unit.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PropertyUnitResponse>.Success(ToPropertyUnitResponse(unit));
    }

    public async Task<ServiceResult<object?>> DeactivatePropertyUnitAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var propertyUnits = await ApplyCurrentCompoundScopeAsync(
            dbContext.PropertyUnits,
            unit => unit.CompoundId,
            cancellationToken);

        var unit = await propertyUnits
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (unit is null)
        {
            return ServiceResult<object?>.NotFound("Property unit was not found.");
        }

        var deactivationValidation = await ValidatePropertyUnitCanBeDeactivatedAsync(id, cancellationToken);
        if (deactivationValidation is not null)
        {
            return ToResult<object?>(deactivationValidation);
        }

        unit.IsActive = false;
        unit.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<PagedResult<ParkingSpotResponse>> SearchParkingSpotsAsync(
        ParkingSpotSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var parkingSpots = await ApplyCurrentCompoundScopeAsync(
            dbContext.ParkingSpots.AsNoTracking(),
            spot => spot.CompoundId,
            cancellationToken);

        if (query.CompoundId.HasValue)
        {
            parkingSpots = parkingSpots.Where(spot => spot.CompoundId == query.CompoundId.Value);
        }

        if (query.IsCovered.HasValue)
        {
            parkingSpots = parkingSpots.Where(spot => spot.IsCovered == query.IsCovered.Value);
        }

        if (query.IsReserved.HasValue)
        {
            parkingSpots = parkingSpots.Where(spot => spot.IsReserved == query.IsReserved.Value);
        }

        if (query.IsActive.HasValue)
        {
            parkingSpots = parkingSpots.Where(spot => spot.IsActive == query.IsActive.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            parkingSpots = parkingSpots.Where(spot => spot.SpotNumber.Contains(searchTerm));
        }

        return await ToPagedResultAsync(
            parkingSpots.OrderBy(spot => spot.SpotNumber),
            query,
            spot => new ParkingSpotResponse(
                spot.Id,
                spot.CompoundId,
                spot.SpotNumber,
                spot.IsCovered,
                spot.IsReserved,
                spot.IsActive,
                spot.Notes,
                spot.CreatedAt,
                spot.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<ParkingSpotResponse>> GetParkingSpotAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var parkingSpots = await ApplyCurrentCompoundScopeAsync(
            dbContext.ParkingSpots.AsNoTracking(),
            spot => spot.CompoundId,
            cancellationToken);

        var parkingSpot = await parkingSpots
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return parkingSpot is null
            ? ServiceResult<ParkingSpotResponse>.NotFound("Parking spot was not found.")
            : ServiceResult<ParkingSpotResponse>.Success(ToParkingSpotResponse(parkingSpot));
    }

    public async Task<ServiceResult<ParkingSpotResponse>> CreateParkingSpotAsync(
        CreateParkingSpotRequest request,
        CancellationToken cancellationToken = default)
    {
        var compoundValidation = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (compoundValidation is not null)
        {
            return ToResult<ParkingSpotResponse>(compoundValidation);
        }

        var spotNumber = request.SpotNumber.Trim();
        var duplicateExists = await dbContext.ParkingSpots.AnyAsync(
            spot => spot.CompoundId == request.CompoundId && spot.SpotNumber == spotNumber,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<ParkingSpotResponse>.Conflict(
                "Parking spot number already exists in this compound.");
        }

        var parkingSpot = new ParkingSpot
        {
            CompoundId = request.CompoundId,
            SpotNumber = spotNumber,
            IsCovered = request.IsCovered,
            IsReserved = request.IsReserved,
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.ParkingSpots.Add(parkingSpot);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParkingSpotResponse>.Success(ToParkingSpotResponse(parkingSpot));
    }

    public async Task<ServiceResult<ParkingSpotResponse>> UpdateParkingSpotAsync(
        Guid id,
        UpdateParkingSpotRequest request,
        CancellationToken cancellationToken = default)
    {
        var parkingSpots = await ApplyCurrentCompoundScopeAsync(
            dbContext.ParkingSpots,
            spot => spot.CompoundId,
            cancellationToken);

        var parkingSpot = await parkingSpots
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (parkingSpot is null)
        {
            return ServiceResult<ParkingSpotResponse>.NotFound("Parking spot was not found.");
        }

        var compoundValidation = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (compoundValidation is not null)
        {
            return ToResult<ParkingSpotResponse>(compoundValidation);
        }

        var spotNumber = request.SpotNumber.Trim();
        var duplicateExists = await dbContext.ParkingSpots.AnyAsync(
            spot => spot.Id != id
                && spot.CompoundId == request.CompoundId
                && spot.SpotNumber == spotNumber,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<ParkingSpotResponse>.Conflict(
                "Parking spot number already exists in this compound.");
        }

        parkingSpot.CompoundId = request.CompoundId;
        parkingSpot.SpotNumber = spotNumber;
        parkingSpot.IsCovered = request.IsCovered;
        parkingSpot.IsReserved = request.IsReserved;
        parkingSpot.IsActive = request.IsActive;
        parkingSpot.Notes = TrimOrNull(request.Notes);
        parkingSpot.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParkingSpotResponse>.Success(ToParkingSpotResponse(parkingSpot));
    }

    public async Task<ServiceResult<object?>> DeactivateParkingSpotAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var parkingSpots = await ApplyCurrentCompoundScopeAsync(
            dbContext.ParkingSpots,
            spot => spot.CompoundId,
            cancellationToken);

        var parkingSpot = await parkingSpots
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (parkingSpot is null)
        {
            return ServiceResult<object?>.NotFound("Parking spot was not found.");
        }

        parkingSpot.IsActive = false;
        parkingSpot.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }


    private async Task<IQueryable<T>> ApplyCurrentCompoundScopeAsync<T>(
        IQueryable<T> query,
        Expression<Func<T, Guid>> compoundIdSelector,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return query.ApplyCompoundAccess(scope, compoundIdSelector);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<bool> CanCreateCompoundAsync(CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return true;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return scope.IsAuthenticated && scope.IsSuperAdmin;
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TSource, TResponse>(
        IQueryable<TSource> query,
        PaginationQuery pagination,
        Expression<Func<TSource, TResponse>> selector,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(selector)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<TResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<ValidationFailure?> ValidateActiveCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var compound = await dbContext.Compounds
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == compoundId, cancellationToken);

        if (compound is null)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        if (!compound.IsActive)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound is inactive.");
        }

        return await CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken)
            ? null
            : new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.");
    }

    private async Task<ValidationFailure?> ValidateActiveBuildingAsync(
        Guid compoundId,
        Guid buildingId,
        CancellationToken cancellationToken)
    {
        var building = await dbContext.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == buildingId, cancellationToken);

        if (building is null)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Building was not found.");
        }

        if (!building.IsActive)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Building is inactive.");
        }

        if (building.CompoundId != compoundId)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Building does not belong to the compound.");
        }

        return await CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken)
            ? null
            : new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.");
    }

    private async Task<ValidationFailure?> ValidatePropertyUnitRequestAsync(
        Guid compoundId,
        Guid? buildingId,
        Guid? floorId,
        PropertyType propertyType,
        UnitStatus unitStatus,
        decimal areaSquareMeters,
        int bedrooms,
        int bathrooms,
        CancellationToken cancellationToken)
    {
        if (!IsPhaseTwoUnitStatus(unitStatus))
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Property unit status is not allowed in Phase 2.");
        }

        if (areaSquareMeters <= 0)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Property unit area must be greater than zero.");
        }

        if (bedrooms < 0 || bathrooms < 0)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Bedrooms and bathrooms cannot be negative.");
        }

        var compoundValidation = await ValidateActiveCompoundAsync(compoundId, cancellationToken);
        if (compoundValidation is not null)
        {
            return compoundValidation;
        }

        if (propertyType == PropertyType.Apartment && !buildingId.HasValue)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Apartment units require a building.");
        }

        if (floorId.HasValue && !buildingId.HasValue)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Building is required when a floor is provided.");
        }

        if (buildingId.HasValue)
        {
            var buildingValidation = await ValidateActiveBuildingAsync(
                compoundId,
                buildingId.Value,
                cancellationToken);

            if (buildingValidation is not null)
            {
                return buildingValidation;
            }
        }

        if (floorId.HasValue)
        {
            var floor = await dbContext.Floors
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == floorId.Value, cancellationToken);

            if (floor is null)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Floor was not found.");
            }

            if (!floor.IsActive)
            {
                return new ValidationFailure(ServiceResultStatus.BadRequest, "Floor is inactive.");
            }

            if (floor.CompoundId != compoundId || floor.BuildingId != buildingId)
            {
                return new ValidationFailure(
                    ServiceResultStatus.BadRequest,
                    "Floor must belong to the same compound and building.");
            }
        }

        return null;
    }


    private async Task<bool> PropertyUnitDuplicateExistsAsync(
        Guid compoundId,
        Guid? buildingId,
        string unitNumber,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        return await dbContext.PropertyUnits.AnyAsync(
            unit => unit.CompoundId == compoundId
                && unit.BuildingId == buildingId
                && unit.UnitNumber == unitNumber
                && (!excludedId.HasValue || unit.Id != excludedId.Value),
            cancellationToken);
    }

    private async Task<ValidationFailure?> ValidatePropertyUnitCanBeDeactivatedAsync(
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        var hasActiveOccupancy = await dbContext.OccupancyRecords.AnyAsync(record =>
            record.PropertyUnitId == propertyUnitId
            && record.OccupancyStatus == OccupancyStatus.Active,
            cancellationToken);
        if (hasActiveOccupancy)
        {
            return new ValidationFailure(ServiceResultStatus.Conflict,
                "Cannot deactivate a property unit with an active occupancy record.");
        }

        var hasActiveRentContract = await dbContext.RentContracts.AnyAsync(contract =>
            contract.PropertyUnitId == propertyUnitId
            && contract.ContractStatus == RentContractStatus.Active,
            cancellationToken);
        if (hasActiveRentContract)
        {
            return new ValidationFailure(ServiceResultStatus.Conflict,
                "Cannot deactivate a property unit with an active rent contract.");
        }

        var hasSaleOwnershipContract = await dbContext.PropertySaleContracts.AnyAsync(contract =>
            contract.PropertyUnitId == propertyUnitId
            && contract.ContractStatus != SaleContractStatus.Cancelled,
            cancellationToken);
        if (hasSaleOwnershipContract)
        {
            return new ValidationFailure(ServiceResultStatus.Conflict,
                "Cannot deactivate a property unit with an active ownership contract.");
        }

        var hasPendingOwnershipTransfer = await dbContext.OwnershipTransferRequests.AnyAsync(transfer =>
            transfer.PropertyUnitId == propertyUnitId
            && transfer.Status == OwnershipTransferStatus.PendingApproval,
            cancellationToken);
        if (hasPendingOwnershipTransfer)
        {
            return new ValidationFailure(ServiceResultStatus.Conflict,
                "Cannot deactivate a property unit with a pending ownership transfer.");
        }

        return null;
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private static bool IsPhaseTwoUnitStatus(UnitStatus unitStatus)
    {
        return PhaseTwoUnitStatuses.Contains(unitStatus);
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static CompoundResponse ToCompoundResponse(Compound compound)
    {
        return new CompoundResponse(
            compound.Id,
            compound.Name,
            compound.Code,
            compound.Description,
            compound.City,
            compound.Area,
            compound.Address,
            compound.IsActive,
            compound.CreatedAt,
            compound.UpdatedAt);
    }

    private static BuildingResponse ToBuildingResponse(Building building)
    {
        return new BuildingResponse(
            building.Id,
            building.CompoundId,
            building.Name,
            building.Code,
            building.NumberOfFloors,
            building.IsActive,
            building.CreatedAt,
            building.UpdatedAt);
    }

    private static FloorResponse ToFloorResponse(Floor floor)
    {
        return new FloorResponse(
            floor.Id,
            floor.CompoundId,
            floor.BuildingId,
            floor.FloorNumber,
            floor.Name,
            floor.IsActive,
            floor.CreatedAt,
            floor.UpdatedAt);
    }

    private static PropertyUnitResponse ToPropertyUnitResponse(PropertyUnit unit)
    {
        return new PropertyUnitResponse(
            unit.Id,
            unit.CompoundId,
            unit.BuildingId,
            unit.FloorId,
            unit.UnitNumber,
            unit.PropertyType,
            unit.UnitStatus,
            unit.AreaSquareMeters,
            unit.Bedrooms,
            unit.Bathrooms,
            unit.HasParking,
            unit.Notes,
            unit.IsActive,
            unit.CreatedAt,
            unit.UpdatedAt);
    }

    private static ParkingSpotResponse ToParkingSpotResponse(ParkingSpot parkingSpot)
    {
        return new ParkingSpotResponse(
            parkingSpot.Id,
            parkingSpot.CompoundId,
            parkingSpot.SpotNumber,
            parkingSpot.IsCovered,
            parkingSpot.IsReserved,
            parkingSpot.IsActive,
            parkingSpot.Notes,
            parkingSpot.CreatedAt,
            parkingSpot.UpdatedAt);
    }


    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
