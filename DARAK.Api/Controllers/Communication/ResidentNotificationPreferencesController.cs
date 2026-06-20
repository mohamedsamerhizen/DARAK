using DARAK.Api.DTOs.Communication;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/communication/notification-preferences")]
public sealed class ResidentNotificationPreferencesController(
    ICommercialCommunicationService commercialCommunicationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ResidentNotificationPreferenceResponse>> GetPreferences(
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.GetPreferencesAsync(
            currentUserService.UserId,
            null,
            cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<ResidentNotificationPreferenceResponse>> UpdatePreferences(
        UpdateResidentNotificationPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialCommunicationService.UpdatePreferencesAsync(
            currentUserService.UserId,
            null,
            request,
            cancellationToken));
    }
}
