using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.CommunicationManagers)]
[Route("api/admin/communication-automation")]
public sealed class AdminCommunicationAutomationController(
    ICommercialCommunicationService commercialCommunicationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("preferences/{userId:guid}")]
    public async Task<ActionResult<ResidentNotificationPreferenceResponse>> GetPreferences(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.GetPreferencesAsync(
            currentUserService.UserId,
            userId,
            cancellationToken));
    }

    [HttpPut("preferences/{userId:guid}")]
    public async Task<ActionResult<ResidentNotificationPreferenceResponse>> UpdatePreferences(
        Guid userId,
        UpdateResidentNotificationPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.UpdatePreferencesAsync(
            currentUserService.UserId,
            userId,
            request,
            cancellationToken));
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<PagedResult<CommunicationCampaignResponse>>> SearchCampaigns(
        [FromQuery] CommunicationCampaignSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.SearchCampaignsAsync(query, cancellationToken));
    }

    [HttpGet("campaigns/{id:guid}")]
    public async Task<ActionResult<CommunicationCampaignDetailsResponse>> GetCampaign(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.GetCampaignAsync(id, cancellationToken));
    }

    [HttpPost("campaigns")]
    public async Task<ActionResult<CommunicationCampaignResponse>> CreateCampaign(
        CreateCommunicationCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commercialCommunicationService.CreateCampaignAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetCampaign), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("campaigns/{id:guid}/send")]
    public async Task<ActionResult<CommunicationCampaignDetailsResponse>> SendCampaign(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.SendCampaignAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [HttpGet("delivery-analytics")]
    public async Task<ActionResult<CommunicationDeliveryAnalyticsResponse>> GetDeliveryAnalytics(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.GetDeliveryAnalyticsAsync(
            compoundId,
            cancellationToken));
    }
}
