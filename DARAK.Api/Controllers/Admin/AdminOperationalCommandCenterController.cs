using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operational;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.OperationalCommandCenterReaders)]
[Route("api/admin/operations")]
public sealed class AdminOperationalCommandCenterController(
    IOperationalCommandCenterService operationalCommandCenterService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("intelligence")]
    public async Task<ActionResult<AdminCommandCenterIntelligenceResponse>> GetIntelligence(
        [FromQuery] AdminCommandCenterIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetIntelligenceAsync(query, cancellationToken));
    }

    [HttpGet("executive-daily-summary")]
    public async Task<ActionResult<ExecutiveDailySummaryResponse>> GetExecutiveDailySummary(
        [FromQuery] ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetExecutiveDailySummaryAsync(query, cancellationToken));
    }

    [HttpGet("domain-signal-board")]
    public async Task<ActionResult<DomainSignalBoardResponse>> GetDomainSignalBoard(
        [FromQuery] ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetDomainSignalBoardAsync(query, cancellationToken));
    }

    [HttpGet("critical-action-queue")]
    public async Task<ActionResult<CriticalActionQueueResponse>> GetCriticalActionQueue(
        [FromQuery] ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetCriticalActionQueueAsync(query, cancellationToken));
    }

    [HttpGet("command-center")]
    public async Task<ActionResult<OperationalCommandCenterResponse>> GetCommandCenter(
        [FromQuery] OperationalCommandCenterQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetCommandCenterAsync(query, cancellationToken));
    }

    [HttpGet("sla-breaches")]
    public async Task<ActionResult<PagedResult<SlaBreachResponse>>> GetSlaBreaches(
        [FromQuery] SlaBreachQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetSlaBreachesAsync(query, cancellationToken));
    }

    [HttpGet("staff-performance")]
    public async Task<ActionResult<StaffPerformanceResponse>> GetStaffPerformance(
        [FromQuery] StaffPerformanceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetStaffPerformanceAsync(query, cancellationToken));
    }

    [HttpGet("compound-health")]
    public async Task<ActionResult<CompoundHealthResponse>> GetCompoundHealth(
        [FromQuery] CompoundHealthQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetCompoundHealthAsync(query, cancellationToken));
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<PagedResult<OperationalTaskResponse>>> SearchTasks(
        [FromQuery] OperationalTaskSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.SearchTasksAsync(query, cancellationToken));
    }

    [HttpGet("tasks/{id:guid}")]
    public async Task<ActionResult<OperationalTaskResponse>> GetTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.GetTaskAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationalCommandCenterManagers)]
    [HttpPost("tasks")]
    public async Task<ActionResult<OperationalTaskResponse>> CreateTask(
        CreateOperationalTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await operationalCommandCenterService.CreateTaskAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetTask), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.OperationalCommandCenterManagers)]
    [HttpPost("tasks/{id:guid}/complete")]
    public async Task<ActionResult<OperationalTaskResponse>> CompleteTask(
        Guid id,
        CompleteOperationalTaskRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.CompleteTaskAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationalCommandCenterManagers)]
    [HttpPost("tasks/{id:guid}/cancel")]
    public async Task<ActionResult<OperationalTaskResponse>> CancelTask(
        Guid id,
        CancelOperationalTaskRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationalCommandCenterService.CancelTaskAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }
}
