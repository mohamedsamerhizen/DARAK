using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.VisitorPassAdministrators)]
[Route("api/admin/visitor-passes")]
public sealed class AdminVisitorPassesController(IVisitorPassService visitorPassService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<VisitorPassResponse>>> Search(
        [FromQuery] VisitorPassSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await visitorPassService.SearchAdminAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VisitorPassResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.GetAdminAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<VisitorPassResponse>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.ApproveAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/deny")]
    public async Task<ActionResult<VisitorPassResponse>> Deny(
        Guid id,
        DenyVisitorPassRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.DenyAsync(id, request, null, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<VisitorPassResponse>> Cancel(
        Guid id,
        CancelVisitorPassRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.CancelAdminAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/access-logs")]
    public async Task<ActionResult<PagedResult<VisitorAccessLogResponse>>> AccessLogs(
        Guid id,
        [FromQuery] VisitorAccessLogSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await visitorPassService.GetAccessLogsAsync(id, query, cancellationToken));
    }
}

