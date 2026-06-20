using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Guard)]
[Route("api/guard/access")]
public sealed class GuardAccessController(
    ICurrentUserService currentUserService,
    IVisitorPassService visitorPassService)
    : ApiControllerBase
{
    [HttpGet("visitors/today")]
    public async Task<ActionResult<PagedResult<VisitorPassResponse>>> TodayVisitors(
        [FromQuery] VisitorPassSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await visitorPassService.SearchTodayForGuardAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpPost("visitors/verify-code")]
    public async Task<ActionResult<VisitorPassResponse>> VerifyAccessCode(
        VerifyVisitorPassAccessCodeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.VerifyAccessCodeAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("visitors/{id:guid}")]
    public async Task<ActionResult<VisitorPassResponse>> GetVisitor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.GetGuardAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [HttpPost("visitors/{id:guid}/check-in")]
    public async Task<ActionResult<VisitorPassResponse>> CheckIn(
        Guid id,
        VisitorPassAccessRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.CheckInAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("visitors/{id:guid}/check-out")]
    public async Task<ActionResult<VisitorPassResponse>> CheckOut(
        Guid id,
        VisitorPassAccessRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.CheckOutAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("visitors/{id:guid}/deny")]
    public async Task<ActionResult<VisitorPassResponse>> DenyEntry(
        Guid id,
        DenyVisitorPassRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.DenyAsync(
            id,
            request,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("visitors/{id:guid}/logs")]
    public async Task<ActionResult<PagedResult<VisitorAccessLogResponse>>> AccessLogs(
        Guid id,
        [FromQuery] VisitorAccessLogSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.GetAccessLogsAsync(id, query, cancellationToken));
    }
}
