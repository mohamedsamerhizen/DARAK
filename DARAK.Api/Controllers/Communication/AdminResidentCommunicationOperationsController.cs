using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.CommunicationManagers)]
[Route("api/admin/communication-operations")]
public sealed class AdminResidentCommunicationOperationsController(
    IResidentCommunicationOperationsService residentCommunicationOperationsService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{


    [HttpGet("command-center")]
    public async Task<ActionResult<CommunicationCommandCenterResponse>> GetCommandCenter(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetCommunicationCommandCenterAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("announcements/acknowledgement-board")]
    public async Task<ActionResult<AnnouncementAcknowledgementBoardResponse>> GetAnnouncementAcknowledgementBoard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetAnnouncementAcknowledgementBoardAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("outages/operations-board")]
    public async Task<ActionResult<UtilityOutageOperationsBoardResponse>> GetOutageOperationsBoard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetUtilityOutageOperationsBoardAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("impact-report")]
    public async Task<ActionResult<ResidentCommunicationImpactReportResponse>> GetImpactReport(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetResidentCommunicationImpactReportAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("response-intelligence")]
    public async Task<ActionResult<CommunicationResponseIntelligenceResponse>> GetResponseIntelligence(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetCommunicationResponseIntelligenceAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("risk-dashboard")]
    public async Task<ActionResult<CommunicationRiskDashboardResponse>> GetRiskDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetCommunicationRiskDashboardAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ResidentCommunicationOperationsSummaryResponse>> GetSummary(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetAdminSummaryAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("outages")]
    public async Task<ActionResult<PagedResult<UtilityOutageResponse>>> SearchOutages(
        [FromQuery] UtilityOutageQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await residentCommunicationOperationsService.SearchUtilityOutagesAsync(query, cancellationToken));
    }

    [HttpGet("outages/{id:guid}")]
    public async Task<ActionResult<UtilityOutageDetailsResponse>> GetOutage(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetUtilityOutageAsync(
            id,
            cancellationToken));
    }

    [HttpPost("outages")]
    public async Task<ActionResult<UtilityOutageDetailsResponse>> CreateOutage(
        CreateUtilityOutageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentCommunicationOperationsService.CreateUtilityOutageAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetOutage), new { id = result.Value!.Outage.Id }, result.Value);
    }

    [HttpPost("outages/{id:guid}/updates")]
    public async Task<ActionResult<UtilityOutageDetailsResponse>> PublishUpdate(
        Guid id,
        PublishUtilityOutageUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.PublishUtilityOutageUpdateAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("outages/{id:guid}/resolve")]
    public async Task<ActionResult<UtilityOutageDetailsResponse>> ResolveOutage(
        Guid id,
        ResolveUtilityOutageRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.ResolveUtilityOutageAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("outages/{id:guid}/cancel")]
    public async Task<ActionResult<UtilityOutageDetailsResponse>> CancelOutage(
        Guid id,
        CancelUtilityOutageRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.CancelUtilityOutageAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }
}
