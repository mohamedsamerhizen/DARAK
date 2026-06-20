using DARAK.Api.DTOs.Buildings;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Floors;
using DARAK.Api.DTOs.ParkingSpots;
using DARAK.Api.DTOs.PropertyUnits;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.StructureReaders)]
[Route("api/admin/property-structure")]
public sealed class AdminPropertyStructureController(
    ICompoundStructureService compoundStructureService,
    IActivityTimelineService activityTimelineService)
    : ApiControllerBase
{
    [HttpGet("buildings")]
    public async Task<ActionResult<PagedResult<BuildingResponse>>> SearchBuildings(
        [FromQuery] BuildingSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await compoundStructureService.SearchBuildingsAsync(query, cancellationToken));
    }

    [HttpGet("buildings/{id:guid}")]
    public async Task<ActionResult<BuildingResponse>> GetBuilding(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.GetBuildingAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPost("buildings")]
    public async Task<ActionResult<BuildingResponse>> CreateBuilding(
        CreateBuildingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await compoundStructureService.CreateBuildingAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetBuilding), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPut("buildings/{id:guid}")]
    public async Task<ActionResult<BuildingResponse>> UpdateBuilding(
        Guid id,
        UpdateBuildingRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.UpdateBuildingAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpDelete("buildings/{id:guid}")]
    public async Task<IActionResult> DeactivateBuilding(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await compoundStructureService.DeactivateBuildingAsync(id, cancellationToken));
    }

    [HttpGet("floors")]
    public async Task<ActionResult<PagedResult<FloorResponse>>> SearchFloors(
        [FromQuery] FloorSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await compoundStructureService.SearchFloorsAsync(query, cancellationToken));
    }

    [HttpGet("floors/{id:guid}")]
    public async Task<ActionResult<FloorResponse>> GetFloor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.GetFloorAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPost("floors")]
    public async Task<ActionResult<FloorResponse>> CreateFloor(
        CreateFloorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await compoundStructureService.CreateFloorAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetFloor), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPut("floors/{id:guid}")]
    public async Task<ActionResult<FloorResponse>> UpdateFloor(
        Guid id,
        UpdateFloorRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.UpdateFloorAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpDelete("floors/{id:guid}")]
    public async Task<IActionResult> DeactivateFloor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await compoundStructureService.DeactivateFloorAsync(id, cancellationToken));
    }

    [HttpGet("units")]
    public async Task<ActionResult<PagedResult<PropertyUnitResponse>>> SearchUnits(
        [FromQuery] PropertyUnitSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await compoundStructureService.SearchPropertyUnitsAsync(query, cancellationToken));
    }

    [HttpGet("units/{id:guid}")]
    public async Task<ActionResult<PropertyUnitResponse>> GetUnit(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.GetPropertyUnitAsync(id, cancellationToken));
    }


    [HttpGet("units/{id:guid}/timeline")]
    public async Task<ActionResult<PagedResult<ActivityEventResponse>>> GetUnitTimeline(
        Guid id,
        [FromQuery] ActivityTimelineQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await activityTimelineService.GetUnitTimelineAsync(
            id,
            query,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPost("units")]
    public async Task<ActionResult<PropertyUnitResponse>> CreateUnit(
        CreatePropertyUnitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await compoundStructureService.CreatePropertyUnitAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetUnit), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPut("units/{id:guid}")]
    public async Task<ActionResult<PropertyUnitResponse>> UpdateUnit(
        Guid id,
        UpdatePropertyUnitRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.UpdatePropertyUnitAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPatch("units/{id:guid}/status")]
    public async Task<ActionResult<PropertyUnitResponse>> UpdateUnitStatus(
        Guid id,
        UpdatePropertyUnitStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.UpdatePropertyUnitStatusAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpDelete("units/{id:guid}")]
    public async Task<IActionResult> DeactivateUnit(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await compoundStructureService.DeactivatePropertyUnitAsync(id, cancellationToken));
    }

    [HttpGet("parking-spots")]
    public async Task<ActionResult<PagedResult<ParkingSpotResponse>>> SearchParkingSpots(
        [FromQuery] ParkingSpotSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await compoundStructureService.SearchParkingSpotsAsync(query, cancellationToken));
    }

    [HttpGet("parking-spots/{id:guid}")]
    public async Task<ActionResult<ParkingSpotResponse>> GetParkingSpot(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.GetParkingSpotAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPost("parking-spots")]
    public async Task<ActionResult<ParkingSpotResponse>> CreateParkingSpot(
        CreateParkingSpotRequest request,
        CancellationToken cancellationToken)
    {
        var result = await compoundStructureService.CreateParkingSpotAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetParkingSpot), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPut("parking-spots/{id:guid}")]
    public async Task<ActionResult<ParkingSpotResponse>> UpdateParkingSpot(
        Guid id,
        UpdateParkingSpotRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.UpdateParkingSpotAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpDelete("parking-spots/{id:guid}")]
    public async Task<IActionResult> DeactivateParkingSpot(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await compoundStructureService.DeactivateParkingSpotAsync(id, cancellationToken));
    }
}
