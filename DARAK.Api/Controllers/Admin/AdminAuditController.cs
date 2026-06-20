using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.AuditReaders)]
[Route("api/admin/audit")]
public sealed class AdminAuditController(IAuditLogService auditLogService) : ApiControllerBase
{
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> SearchLogs(
        [FromQuery] AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await auditLogService.SearchAsync(query, cancellationToken));
    }

    [HttpGet("logs/{id:guid}")]
    public async Task<ActionResult<AuditLogDetailsResponse>> GetLogDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await auditLogService.GetDetailsAsync(id, cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AuditDashboardResponse>> GetDashboard(
        [FromQuery] AuditDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await auditLogService.GetDashboardAsync(query, cancellationToken));
    }

    [HttpGet("entities/{entityType}/{entityId:guid}")]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> GetEntityTrail(
        AuditEntityType entityType,
        Guid entityId,
        [FromQuery] AuditEntityTrailQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await auditLogService.GetEntityTrailAsync(entityType, entityId, query, cancellationToken));
    }

    [HttpGet("residents/{residentProfileId:guid}")]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> GetResidentTrail(
        Guid residentProfileId,
        [FromQuery] AuditEntityTrailQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await auditLogService.GetResidentTrailAsync(residentProfileId, query, cancellationToken));
    }
}
