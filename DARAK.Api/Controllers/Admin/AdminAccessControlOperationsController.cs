using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.OperationsManagers)]
[Route("api/admin/access-control-operations")]
public sealed class AdminAccessControlOperationsController(
    ICurrentUserService currentUserService,
    IAccessControlOperationsService accessControlOperationsService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<AccessControlOperationsSummaryResponse>> Summary(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GetSummaryAsync(compoundId, cancellationToken));
    }


    [HttpGet("pro-dashboard")]
    public async Task<ActionResult<AccessControlProDashboardResponse>> ProDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GetProDashboardAsync(compoundId, cancellationToken));
    }

    [HttpGet("security-command-queue")]
    public async Task<ActionResult<PagedResult<AccessSecurityCommandQueueItemResponse>>> SecurityCommandQueue(
        [FromQuery] AccessSecurityCommandQueueQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetSecurityCommandQueueAsync(query, cancellationToken));
    }

    [HttpGet("credentials/risk-queue")]
    public async Task<ActionResult<PagedResult<AccessCredentialRiskQueueItemResponse>>> CredentialRiskQueue(
        [FromQuery] AccessCredentialRiskQueueQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetCredentialRiskQueueAsync(query, cancellationToken));
    }

    [HttpGet("contractor-work-permits/escort-queue")]
    public async Task<ActionResult<PagedResult<ContractorEscortQueueItemResponse>>> ContractorEscortQueue(
        [FromQuery] ContractorEscortQueueQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetContractorEscortQueueAsync(query, cancellationToken));
    }

    [HttpGet("access-audit-trail")]
    public async Task<ActionResult<PagedResult<AccessAuditTrailItemResponse>>> AccessAuditTrail(
        [FromQuery] AccessAuditTrailQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetAccessAuditTrailAsync(query, cancellationToken));
    }


    [HttpGet("gate-situation-report")]
    public async Task<ActionResult<AccessGateSituationReportResponse>> GateSituationReport(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GetGateSituationReportAsync(compoundId, cancellationToken));
    }

    [HttpGet("visitors/verification-board")]
    public async Task<ActionResult<PagedResult<VisitorVerificationBoardItemResponse>>> VisitorVerificationBoard(
        [FromQuery] VisitorVerificationBoardQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetVisitorVerificationBoardAsync(query, cancellationToken));
    }

    [HttpGet("contractors/compliance-board")]
    public async Task<ActionResult<PagedResult<ContractorAccessComplianceBoardItemResponse>>> ContractorAccessComplianceBoard(
        [FromQuery] ContractorAccessComplianceBoardQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetContractorAccessComplianceBoardAsync(query, cancellationToken));
    }

    [HttpGet("credentials/control-board")]
    public async Task<ActionResult<PagedResult<AccessCredentialControlBoardItemResponse>>> CredentialControlBoard(
        [FromQuery] AccessCredentialControlBoardQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.GetCredentialControlBoardAsync(query, cancellationToken));
    }

    [HttpGet("guard-shift-handover")]
    public async Task<ActionResult<GuardShiftHandoverReportResponse>> GuardShiftHandover(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GetGuardShiftHandoverReportAsync(compoundId, cancellationToken));
    }

    [HttpGet("contractor-work-permits")]
    public async Task<ActionResult<PagedResult<ContractorWorkPermitResponse>>> SearchContractorWorkPermits(
        [FromQuery] ContractorWorkPermitQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.SearchContractorWorkPermitsAsync(query, cancellationToken));
    }

    [HttpGet("contractor-work-permits/{id:guid}")]
    public async Task<ActionResult<ContractorWorkPermitResponse>> GetContractorWorkPermit(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GetContractorWorkPermitAsync(id, cancellationToken));
    }

    [HttpPost("contractor-work-permits")]
    public async Task<ActionResult<ContractorWorkPermitResponse>> CreateContractorWorkPermit(
        CreateContractorWorkPermitRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.CreateContractorWorkPermitAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("contractor-work-permits/{id:guid}/approve")]
    public async Task<ActionResult<ContractorWorkPermitResponse>> ApproveContractorWorkPermit(
        Guid id,
        ContractorPermitDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.ApproveContractorWorkPermitAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("contractor-work-permits/{id:guid}/deny")]
    public async Task<ActionResult<ContractorWorkPermitResponse>> DenyContractorWorkPermit(
        Guid id,
        DenyContractorWorkPermitRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.DenyContractorWorkPermitAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("credentials")]
    public async Task<ActionResult<PagedResult<AccessCredentialResponse>>> SearchCredentials(
        [FromQuery] AccessCredentialQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.SearchAccessCredentialsAsync(query, cancellationToken));
    }

    [HttpPost("credentials")]
    public async Task<ActionResult<AccessCredentialResponse>> CreateCredential(
        CreateAccessCredentialRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.CreateAccessCredentialAsync(request, cancellationToken));
    }

    [HttpPatch("credentials/{id:guid}/revoke")]
    public async Task<ActionResult<AccessCredentialResponse>> RevokeCredential(
        Guid id,
        RevokeAccessCredentialRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.RevokeAccessCredentialAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }
}
