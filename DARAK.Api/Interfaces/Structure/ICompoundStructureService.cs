using DARAK.Api.DTOs.Buildings;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Compounds;
using DARAK.Api.DTOs.Floors;
using DARAK.Api.DTOs.ParkingSpots;
using DARAK.Api.DTOs.PropertyUnits;

namespace DARAK.Api.Interfaces;

public interface ICompoundStructureService
{
    Task<PagedResult<CompoundResponse>> SearchCompoundsAsync(
        CompoundSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundResponse>> GetCompoundAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundResponse>> CreateCompoundAsync(
        CreateCompoundRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundResponse>> UpdateCompoundAsync(
        Guid id,
        UpdateCompoundRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateCompoundAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<BuildingResponse>> SearchBuildingsAsync(
        BuildingSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BuildingResponse>> GetBuildingAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BuildingResponse>> CreateBuildingAsync(
        CreateBuildingRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BuildingResponse>> UpdateBuildingAsync(
        Guid id,
        UpdateBuildingRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateBuildingAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FloorResponse>> SearchFloorsAsync(
        FloorSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FloorResponse>> GetFloorAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FloorResponse>> CreateFloorAsync(
        CreateFloorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FloorResponse>> UpdateFloorAsync(
        Guid id,
        UpdateFloorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateFloorAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PropertyUnitResponse>> SearchPropertyUnitsAsync(
        PropertyUnitSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertyUnitResponse>> GetPropertyUnitAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertyUnitResponse>> CreatePropertyUnitAsync(
        CreatePropertyUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertyUnitResponse>> UpdatePropertyUnitAsync(
        Guid id,
        UpdatePropertyUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertyUnitResponse>> UpdatePropertyUnitStatusAsync(
        Guid id,
        UpdatePropertyUnitStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivatePropertyUnitAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ParkingSpotResponse>> SearchParkingSpotsAsync(
        ParkingSpotSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ParkingSpotResponse>> GetParkingSpotAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ParkingSpotResponse>> CreateParkingSpotAsync(
        CreateParkingSpotRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ParkingSpotResponse>> UpdateParkingSpotAsync(
        Guid id,
        UpdateParkingSpotRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateParkingSpotAsync(
        Guid id,
        CancellationToken cancellationToken = default);

}
