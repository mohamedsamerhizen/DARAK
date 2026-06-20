using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.FinanceReaders)]
[Route("api/admin/finance")]
public sealed class AdminFinanceController(
    IFinancialControlService financialControlService,
    IFinancialGovernanceService financialGovernanceService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<FinancialControlDashboardResponse>> GetDashboard(
        [FromQuery] FinancialDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetDashboardAsync(query, cancellationToken));
    }

    [HttpGet("financial-governance/summary")]
    public async Task<ActionResult<AdminFinancialGovernanceSummaryResponse>> GetFinancialGovernanceSummary(
        [FromQuery] FinancialGovernanceSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.GetAdminFinancialGovernanceSummaryAsync(query, cancellationToken));
    }

    [HttpGet("residents/{residentProfileId:guid}/financial-governance")]
    public async Task<ActionResult<AdminResidentFinancialGovernanceSnapshotResponse>> GetResidentFinancialGovernanceSnapshot(
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.GetAdminResidentFinancialGovernanceSnapshotAsync(
            residentProfileId,
            cancellationToken));
    }

    [HttpGet("residents/{residentProfileId:guid}/statement")]
    public async Task<ActionResult<ResidentStatementResponse>> GetResidentStatement(
        Guid residentProfileId,
        [FromQuery] ResidentStatementQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetResidentStatementAsync(
            residentProfileId,
            query,
            cancellationToken));
    }

    [HttpGet("aging-report")]
    public async Task<ActionResult<FinancialAgingReportResponse>> GetAgingReport(
        [FromQuery] FinancialAgingReportQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetAgingReportAsync(query, cancellationToken));
    }

    [HttpGet("closure-summary")]
    public async Task<ActionResult<FinancialClosureSummaryResponse>> GetClosureSummary(
        [FromQuery] FinancialClosureSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetFinancialClosureSummaryAsync(query, cancellationToken));
    }

    [HttpGet("revenue-summary")]
    public async Task<ActionResult<RevenueSummaryResponse>> GetRevenueSummary(
        [FromQuery] RevenueSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetRevenueSummaryAsync(query, cancellationToken));
    }

    [HttpGet("aging-risk-report")]
    public async Task<ActionResult<FinancialAgingRiskReportResponse>> GetAgingRiskReport(
        [FromQuery] FinancialAgingRiskReportQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetAgingRiskReportAsync(query, cancellationToken));
    }


    [HttpGet("adjustments")]
    public async Task<ActionResult<PagedResult<FinancialAdjustmentResponse>>> SearchAdjustments(
        [FromQuery] FinancialAdjustmentSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.SearchAdjustmentsAsync(query, cancellationToken));
    }

    [HttpGet("adjustments/{id:guid}")]
    public async Task<ActionResult<FinancialAdjustmentResponse>> GetAdjustment(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.GetAdjustmentAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("adjustments")]
    public async Task<ActionResult<FinancialAdjustmentResponse>> CreateAdjustment(
        CreateFinancialAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await financialControlService.CreateAdjustmentAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetAdjustment), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.FinancialAdjustmentManagers)]
    [HttpPost("adjustments/{id:guid}/apply")]
    public async Task<ActionResult<FinancialAdjustmentResponse>> ApplyAdjustment(
        Guid id,
        ApplyFinancialAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.ApplyAdjustmentAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinancialAdjustmentManagers)]
    [HttpPost("adjustments/{id:guid}/cancel")]
    public async Task<ActionResult<FinancialAdjustmentResponse>> CancelAdjustment(
        Guid id,
        CancelFinancialAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialControlService.CancelAdjustmentAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpGet("disputes")]
    public async Task<ActionResult<PagedResult<FinancialDisputeResponse>>> SearchFinancialDisputes(
        [FromQuery] FinancialDisputeSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.SearchFinancialDisputesAsync(query, cancellationToken));
    }

    [HttpGet("disputes/{id:guid}")]
    public async Task<ActionResult<FinancialDisputeResponse>> GetFinancialDispute(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.GetFinancialDisputeAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("disputes")]
    public async Task<ActionResult<FinancialDisputeResponse>> CreateFinancialDispute(
        CreateFinancialDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await financialGovernanceService.CreateFinancialDisputeAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetFinancialDispute), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("disputes/{id:guid}/transition")]
    public async Task<ActionResult<FinancialDisputeResponse>> TransitionFinancialDispute(
        Guid id,
        TransitionFinancialDisputeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.TransitionFinancialDisputeAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("disputes/{id:guid}/adjustment")]
    public async Task<ActionResult<FinancialDisputeResponse>> CreateFinancialDisputeAdjustment(
        Guid id,
        CreateGovernanceFinancialAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.CreateAdjustmentForFinancialDisputeAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpGet("violation-appeals")]
    public async Task<ActionResult<PagedResult<ViolationAppealResponse>>> SearchViolationAppeals(
        [FromQuery] ViolationAppealSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.SearchViolationAppealsAsync(query, cancellationToken));
    }

    [HttpGet("violation-appeals/{id:guid}")]
    public async Task<ActionResult<ViolationAppealResponse>> GetViolationAppeal(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.GetViolationAppealAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("violation-appeals")]
    public async Task<ActionResult<ViolationAppealResponse>> CreateViolationAppeal(
        CreateViolationAppealRequest request,
        CancellationToken cancellationToken)
    {
        var result = await financialGovernanceService.CreateViolationAppealAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetViolationAppeal), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("violation-appeals/{id:guid}/transition")]
    public async Task<ActionResult<ViolationAppealResponse>> TransitionViolationAppeal(
        Guid id,
        TransitionViolationAppealRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.TransitionViolationAppealAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("violation-appeals/{id:guid}/adjustment")]
    public async Task<ActionResult<ViolationAppealResponse>> CreateViolationAppealAdjustment(
        Guid id,
        CreateGovernanceFinancialAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await financialGovernanceService.CreateAdjustmentForViolationAppealAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

}
