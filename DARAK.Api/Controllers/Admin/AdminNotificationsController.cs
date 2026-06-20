using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Notifications;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.CommunicationManagers)]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController(
    INotificationOutboxService notificationOutboxService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("outbox")]
    public async Task<ActionResult<PagedResult<NotificationOutboxResponse>>> SearchOutbox(
        [FromQuery] NotificationSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await notificationOutboxService.SearchAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("outbox/{id:guid}")]
    public async Task<ActionResult<NotificationOutboxResponse>> GetOutboxItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await notificationOutboxService.GetAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [HttpPost("manual")]
    public async Task<ActionResult<NotificationOutboxResponse>> EnqueueManual(
        ManualNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await notificationOutboxService.EnqueueManualAsync(
            currentUserService.UserId,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetOutboxItem), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("outbox/{id:guid}/retry")]
    public async Task<ActionResult<NotificationOutboxResponse>> Retry(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await notificationOutboxService.MarkForRetryAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<NotificationDashboardSummaryResponse>> GetDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await notificationOutboxService.GetDashboardSummaryAsync(
            currentUserService.UserId,
            compoundId,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPost("process-due")]
    public async Task<ActionResult<object>> ProcessDue(
        [FromQuery] int batchSize,
        CancellationToken cancellationToken)
    {
        var processed = await notificationOutboxService.ProcessDueNotificationsAsync(
            batchSize <= 0 ? 25 : batchSize,
            cancellationToken);

        return Ok(new { processed });
    }
}
