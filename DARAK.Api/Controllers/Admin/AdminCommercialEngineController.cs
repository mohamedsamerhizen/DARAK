using DARAK.Api.DTOs.Commercial;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ContractManagers)]
[Route("api/admin/commercial-engine")]
public sealed class AdminCommercialEngineController(
    ICommercialEngineService commercialEngineService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<CommercialEngineDashboardResponse>> GetDashboard(
        [FromQuery] CommercialDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.GetDashboardAsync(query, cancellationToken));
    }

    [HttpGet("billing-rules")]
    public async Task<ActionResult<PagedResult<BillingRuleResponse>>> SearchBillingRules(
        [FromQuery] BillingRuleSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.SearchBillingRulesAsync(query, cancellationToken));
    }

    [HttpGet("billing-rules/{id:guid}")]
    public async Task<ActionResult<BillingRuleResponse>> GetBillingRule(Guid id, CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.GetBillingRuleAsync(id, cancellationToken));
    }

    [HttpPost("billing-rules")]
    public async Task<ActionResult<BillingRuleResponse>> CreateBillingRule(
        CreateBillingRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commercialEngineService.CreateBillingRuleAsync(currentUserService.UserId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetBillingRule), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("billing-rules/{id:guid}/tiers")]
    public async Task<ActionResult<BillingRuleResponse>> AddBillingRuleTier(
        Guid id,
        AddBillingRuleTierRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.AddBillingRuleTierAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpGet("meter-corrections")]
    public async Task<ActionResult<PagedResult<MeterReadingCorrectionResponse>>> SearchMeterCorrections(
        [FromQuery] MeterReadingCorrectionSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.SearchMeterCorrectionsAsync(query, cancellationToken));
    }

    [HttpPost("meter-corrections")]
    public async Task<ActionResult<MeterReadingCorrectionResponse>> CreateMeterCorrection(
        CreateMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.CreateMeterCorrectionAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpPost("meter-corrections/{id:guid}/approve")]
    public async Task<ActionResult<MeterReadingCorrectionResponse>> ApproveMeterCorrection(
        Guid id,
        DecideMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.ApproveMeterCorrectionAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpPost("meter-corrections/{id:guid}/reject")]
    public async Task<ActionResult<MeterReadingCorrectionResponse>> RejectMeterCorrection(
        Guid id,
        DecideMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.RejectMeterCorrectionAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpPost("contracts/lifecycle-events")]
    public async Task<ActionResult<ContractLifecycleEventResponse>> CreateContractLifecycleEvent(
        CreateContractLifecycleEventRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.CreateContractLifecycleEventAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpGet("contracts/{contractType}/{contractId:guid}/timeline")]
    public async Task<ActionResult<PagedResult<ContractLifecycleEventResponse>>> GetContractTimeline(
        CommercialContractType contractType,
        Guid contractId,
        [FromQuery] ContractLifecycleTimelineQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.GetContractTimelineAsync(contractType, contractId, query, cancellationToken));
    }

    [HttpPost("unit-handovers")]
    public async Task<ActionResult<UnitHandoverChecklistResponse>> CreateUnitHandoverChecklist(
        CreateUnitHandoverChecklistRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.CreateUnitHandoverChecklistAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpPost("unit-handovers/{id:guid}/complete")]
    public async Task<ActionResult<UnitHandoverChecklistResponse>> CompleteUnitHandoverChecklist(
        Guid id,
        CompleteUnitHandoverChecklistRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.CompleteUnitHandoverChecklistAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpPost("ownership-transfers")]
    public async Task<ActionResult<OwnershipTransferRequestResponse>> CreateOwnershipTransfer(
        CreateOwnershipTransferRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.CreateOwnershipTransferAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpPost("ownership-transfers/{id:guid}/approve")]
    public async Task<ActionResult<OwnershipTransferRequestResponse>> ApproveOwnershipTransfer(
        Guid id,
        DecideOwnershipTransferRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.ApproveOwnershipTransferAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpPost("ownership-transfers/{id:guid}/reject")]
    public async Task<ActionResult<OwnershipTransferRequestResponse>> RejectOwnershipTransfer(
        Guid id,
        DecideOwnershipTransferRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.RejectOwnershipTransferAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpPost("installment-reschedules")]
    public async Task<ActionResult<InstallmentRescheduleRequestResponse>> CreateInstallmentReschedule(
        CreateInstallmentRescheduleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.CreateInstallmentRescheduleAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpPost("installment-reschedules/{id:guid}/approve")]
    public async Task<ActionResult<InstallmentRescheduleRequestResponse>> ApproveInstallmentReschedule(
        Guid id,
        DecideInstallmentRescheduleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.ApproveInstallmentRescheduleAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [HttpPost("installment-reschedules/{id:guid}/reject")]
    public async Task<ActionResult<InstallmentRescheduleRequestResponse>> RejectInstallmentReschedule(
        Guid id,
        DecideInstallmentRescheduleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialEngineService.RejectInstallmentRescheduleAsync(currentUserService.UserId, id, request, cancellationToken));
    }
}
