using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.ResidentLifecycle;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.OperationsManagers)]
[Route("api/admin/resident-lifecycle")]
public sealed class AdminResidentLifecycleController(
    ICurrentUserService currentUserService,
    IResidentLifecycleService residentLifecycleService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ResidentLifecycleSummaryResponse>> Summary(
        [FromQuery] ResidentLifecycleSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.GetSummaryAsync(query, cancellationToken));
    }

    [HttpGet("move-out-readiness")]
    public async Task<ActionResult<MoveOutReadinessResponse>> MoveOutReadiness(
        [FromQuery] MoveOutReadinessQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.GetMoveOutReadinessAsync(query, cancellationToken));
    }

    [HttpGet("processes")]
    public async Task<ActionResult<PagedResult<ResidentLifecycleProcessResponse>>> SearchProcesses(
        [FromQuery] ResidentLifecycleQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await residentLifecycleService.SearchProcessesAsync(query, cancellationToken));
    }

    [HttpGet("processes/{id:guid}")]
    public async Task<ActionResult<ResidentLifecycleProcessResponse>> GetProcess(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.GetProcessAsync(id, cancellationToken));
    }

    [HttpGet("processes/{id:guid}/move-out-operational-settlement")]
    public async Task<ActionResult<MoveOutOperationalSettlementResponse>> GetMoveOutOperationalSettlement(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.GetMoveOutOperationalSettlementAsync(id, cancellationToken));
    }

    [HttpGet("processes/{id:guid}/exit-certificate")]
    public async Task<ActionResult<MoveOutExitCertificateResponse>> GetMoveOutExitCertificate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.GetMoveOutExitCertificateAsync(id, cancellationToken));
    }

    [HttpPost("processes/{id:guid}/unit-turnover")]
    public async Task<ActionResult<UnitReadinessRecordResponse>> PrepareMoveOutUnitTurnover(
        Guid id,
        PrepareMoveOutUnitTurnoverRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.PrepareMoveOutUnitTurnoverAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("processes/{id:guid}/unit-turnover-timeline")]
    public async Task<ActionResult<MoveOutUnitTurnoverTimelineResponse>> GetMoveOutUnitTurnoverTimeline(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.GetMoveOutUnitTurnoverTimelineAsync(id, cancellationToken));
    }

    [HttpPost("processes/{id:guid}/final-meter-readings")]
    public async Task<ActionResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>> RecordMoveOutFinalMeterReadings(
        Guid id,
        RecordMoveOutFinalMeterReadingsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.RecordMoveOutFinalMeterReadingsAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("processes")]
    public async Task<ActionResult<ResidentLifecycleProcessResponse>> CreateProcess(
        CreateResidentLifecycleProcessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentLifecycleService.CreateProcessAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetProcess), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPatch("processes/{id:guid}/financial-clearance")]
    public async Task<ActionResult<ResidentLifecycleProcessResponse>> ConfirmFinancialClearance(
        Guid id,
        ConfirmLifecycleFinancialClearanceRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.ConfirmFinancialClearanceAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("processes/{id:guid}/complete")]
    public async Task<ActionResult<ResidentLifecycleProcessResponse>> CompleteProcess(
        Guid id,
        CompleteResidentLifecycleProcessRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.CompleteProcessAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("custody-items")]
    public async Task<ActionResult<PagedResult<ResidentCustodyItemResponse>>> SearchCustodyItems(
        [FromQuery] CustodyItemQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await residentLifecycleService.SearchCustodyItemsAsync(query, cancellationToken));
    }

    [HttpPost("custody-items")]
    public async Task<ActionResult<ResidentCustodyItemResponse>> IssueCustodyItem(
        IssueCustodyItemRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.IssueCustodyItemAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("custody-items/{id:guid}/return")]
    public async Task<ActionResult<ResidentCustodyItemResponse>> ReturnCustodyItem(
        Guid id,
        ReturnCustodyItemRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.ReturnCustodyItemAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("custody-items/{id:guid}/settlement-status")]
    public async Task<ActionResult<ResidentCustodyItemResponse>> UpdateCustodySettlementStatus(
        Guid id,
        UpdateCustodySettlementStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.UpdateCustodyItemSettlementStatusAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("move-permits")]
    public async Task<ActionResult<PagedResult<MoveLogisticsPermitResponse>>> SearchMovePermits(
        [FromQuery] MoveLogisticsPermitQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await residentLifecycleService.SearchMovePermitsAsync(query, cancellationToken));
    }

    [HttpPost("move-permits")]
    public async Task<ActionResult<MoveLogisticsPermitResponse>> CreateMovePermit(
        CreateMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.CreateMovePermitAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("move-permits/{id:guid}/decision")]
    public async Task<ActionResult<MoveLogisticsPermitResponse>> DecideMovePermit(
        Guid id,
        DecideMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.DecideMovePermitAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("move-permits/{id:guid}/complete")]
    public async Task<ActionResult<MoveLogisticsPermitResponse>> CompleteMovePermit(
        Guid id,
        CompleteMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.CompleteMovePermitAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("unit-readiness")]
    public async Task<ActionResult<PagedResult<UnitReadinessRecordResponse>>> SearchUnitReadiness(
        [FromQuery] UnitReadinessQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await residentLifecycleService.SearchUnitReadinessRecordsAsync(query, cancellationToken));
    }

    [HttpPost("unit-readiness")]
    public async Task<ActionResult<UnitReadinessRecordResponse>> CreateUnitReadiness(
        CreateUnitReadinessRecordRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.CreateUnitReadinessRecordAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("unit-readiness/{id:guid}/status")]
    public async Task<ActionResult<UnitReadinessRecordResponse>> UpdateUnitReadiness(
        Guid id,
        UpdateUnitReadinessStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.UpdateUnitReadinessStatusAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("damage-liabilities")]
    public async Task<ActionResult<UnitDamageLiabilityResponse>> CreateDamageLiability(
        CreateUnitDamageLiabilityRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.CreateDamageLiabilityAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("damage-liabilities/{id:guid}/status")]
    public async Task<ActionResult<UnitDamageLiabilityResponse>> UpdateDamageLiabilityStatus(
        Guid id,
        UpdateDamageLiabilityStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentLifecycleService.UpdateDamageLiabilityStatusAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }
}
