using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.EmergencyContacts;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.DTOs.FamilyMembers;
using DARAK.Api.DTOs.Occupancy;
using DARAK.Api.DTOs.Residents;
using DARAK.Api.DTOs.RiskFlags;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ResidentManagementReaders)]
[Route("api/admin/residents")]
public sealed class AdminResidentsController(
    IResidentService residentService,
    IOccupancyService occupancyService,
    IActivityTimelineService activityTimelineService,
    IResidentFinancialHealthService residentFinancialHealthService,
    IResidentRiskFlagService residentRiskFlagService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ResidentProfileResponse>>> Search(
        [FromQuery] ResidentProfileSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await residentService.SearchResidentProfilesAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResidentProfileResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentService.GetResidentProfileAsync(id, cancellationToken));
    }




    [HttpGet("{id:guid}/financial-health")]
    public async Task<ActionResult<ResidentFinancialHealthResponse>> GetFinancialHealth(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentFinancialHealthService.GetAdminResidentFinancialHealthAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }



    [HttpGet("{id:guid}/risk-flags")]
    public async Task<ActionResult<PagedResult<ResidentRiskFlagResponse>>> GetRiskFlags(
        Guid id,
        [FromQuery] ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.GetResidentFlagsAsync(
            currentUserService.UserId,
            id,
            query,
            cancellationToken));
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<PagedResult<ActivityEventResponse>>> GetResidentTimeline(
        Guid id,
        [FromQuery] ActivityTimelineQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await activityTimelineService.GetResidentTimelineAsync(
            id,
            query,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPost]
    public async Task<ActionResult<ResidentProfileResponse>> Create(
        CreateResidentProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentService.CreateResidentProfileAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ResidentProfileResponse>> Update(
        Guid id,
        UpdateResidentProfileRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentService.UpdateResidentProfileAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await residentService.DeactivateResidentProfileAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/family-members")]
    public async Task<ActionResult<IReadOnlyCollection<FamilyMemberResponse>>> GetFamilyMembers(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentService.GetFamilyMembersAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPost("{id:guid}/family-members")]
    public async Task<ActionResult<FamilyMemberResponse>> AddFamilyMember(
        Guid id,
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentService.AddFamilyMemberAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPut("{id:guid}/family-members/{familyMemberId:guid}")]
    public async Task<ActionResult<FamilyMemberResponse>> UpdateFamilyMember(
        Guid id,
        Guid familyMemberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(
            await residentService.UpdateFamilyMemberAsync(id, familyMemberId, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpDelete("{id:guid}/family-members/{familyMemberId:guid}")]
    public async Task<IActionResult> DeactivateFamilyMember(
        Guid id,
        Guid familyMemberId,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(
            await residentService.DeactivateFamilyMemberAsync(id, familyMemberId, cancellationToken));
    }

    [HttpGet("{id:guid}/emergency-contacts")]
    public async Task<ActionResult<IReadOnlyCollection<EmergencyContactResponse>>> GetEmergencyContacts(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentService.GetEmergencyContactsAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPost("{id:guid}/emergency-contacts")]
    public async Task<ActionResult<EmergencyContactResponse>> AddEmergencyContact(
        Guid id,
        CreateEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentService.AddEmergencyContactAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPut("{id:guid}/emergency-contacts/{contactId:guid}")]
    public async Task<ActionResult<EmergencyContactResponse>> UpdateEmergencyContact(
        Guid id,
        Guid contactId,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(
            await residentService.UpdateEmergencyContactAsync(id, contactId, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpDelete("{id:guid}/emergency-contacts/{contactId:guid}")]
    public async Task<IActionResult> DeactivateEmergencyContact(
        Guid id,
        Guid contactId,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(
            await residentService.DeactivateEmergencyContactAsync(id, contactId, cancellationToken));
    }

    [HttpGet("occupancies")]
    public async Task<ActionResult<PagedResult<OccupancyRecordResponse>>> SearchOccupancies(
        [FromQuery] OccupancySearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await occupancyService.SearchOccupanciesAsync(query, cancellationToken));
    }

    [HttpGet("occupancies/{occupancyId:guid}")]
    public async Task<ActionResult<OccupancyRecordResponse>> GetOccupancy(
        Guid occupancyId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await occupancyService.GetOccupancyAsync(occupancyId, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPost("occupancies")]
    public async Task<ActionResult<OccupancyRecordResponse>> CreateOccupancy(
        CreateOccupancyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await occupancyService.CreateOccupancyAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetOccupancy), new { occupancyId = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
    [HttpPost("occupancies/{occupancyId:guid}/end")]
    public async Task<ActionResult<OccupancyRecordResponse>> EndOccupancy(
        Guid occupancyId,
        EndOccupancyRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await occupancyService.EndOccupancyAsync(occupancyId, request, cancellationToken));
    }
}
