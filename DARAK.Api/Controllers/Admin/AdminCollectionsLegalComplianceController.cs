using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.FinanceReaders)]
[Route("api/admin/collections-legal-compliance")]
public sealed class AdminCollectionsLegalComplianceController(
    ICurrentUserService currentUserService,
    ICollectionsLegalComplianceService collectionsLegalComplianceService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<CollectionsLegalComplianceSummaryResponse>> Summary(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.GetSummaryAsync(compoundId, cancellationToken));
    }

    [HttpGet("penalty-rules")]
    public async Task<ActionResult<PagedResult<PenaltyRuleResponse>>> SearchPenaltyRules(
        [FromQuery] PenaltyRuleQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await collectionsLegalComplianceService.SearchPenaltyRulesAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("penalty-rules")]
    public async Task<ActionResult<PenaltyRuleResponse>> CreatePenaltyRule(
        CreatePenaltyRuleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.CreatePenaltyRuleAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("collection-cases")]
    public async Task<ActionResult<PagedResult<CollectionCaseResponse>>> SearchCollectionCases(
        [FromQuery] CollectionCaseQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await collectionsLegalComplianceService.SearchCollectionCasesAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("collection-cases")]
    public async Task<ActionResult<CollectionCaseResponse>> CreateCollectionCase(
        CreateCollectionCaseRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.CreateCollectionCaseAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPatch("collection-cases/{id:guid}/advance")]
    public async Task<ActionResult<CollectionCaseResponse>> AdvanceCollectionCase(
        Guid id,
        AdvanceCollectionCaseRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.AdvanceCollectionCaseAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("legal-notices")]
    public async Task<ActionResult<PagedResult<LegalNoticeResponse>>> SearchLegalNotices(
        [FromQuery] LegalNoticeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await collectionsLegalComplianceService.SearchLegalNoticesAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("legal-notices")]
    public async Task<ActionResult<LegalNoticeResponse>> CreateLegalNotice(
        CreateLegalNoticeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.CreateLegalNoticeAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPatch("legal-notices/{id:guid}/issue")]
    public async Task<ActionResult<LegalNoticeResponse>> IssueLegalNotice(
        Guid id,
        IssueLegalNoticeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.IssueLegalNoticeAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPost("payment-plans")]
    public async Task<ActionResult<PaymentPlanResponse>> CreatePaymentPlan(
        CreatePaymentPlanRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.CreatePaymentPlanAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.FinanceManagers)]
    [HttpPatch("payment-plans/{paymentPlanId:guid}/installments/{installmentId:guid}/pay")]
    public async Task<ActionResult<PaymentPlanResponse>> PayPaymentPlanInstallment(
        Guid paymentPlanId,
        Guid installmentId,
        PayPaymentPlanInstallmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.PayPaymentPlanInstallmentAsync(
            paymentPlanId,
            installmentId,
            request,
            cancellationToken));
    }


    [HttpGet("collection-cases/follow-up-queue")]
    public async Task<ActionResult<PagedResult<CollectionFollowUpQueueItemResponse>>> GetCollectionFollowUpQueue(
        [FromQuery] CollectionFollowUpQueueQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await collectionsLegalComplianceService.GetCollectionFollowUpQueueAsync(query, cancellationToken));
    }



    [HttpGet("legal-cases/dashboard")]
    public async Task<ActionResult<LegalCaseManagementDashboardResponse>> GetLegalCaseManagementDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.GetLegalCaseManagementDashboardAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("legal-cases/escalation-queue")]
    public async Task<ActionResult<PagedResult<LegalCaseEscalationQueueItemResponse>>> GetLegalCaseEscalationQueue(
        [FromQuery] LegalCaseEscalationQueueQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await collectionsLegalComplianceService.GetLegalCaseEscalationQueueAsync(query, cancellationToken));
    }

    [HttpGet("legal-notices/service-queue")]
    public async Task<ActionResult<PagedResult<LegalNoticeServiceQueueItemResponse>>> GetLegalNoticeServiceQueue(
        [FromQuery] LegalNoticeServiceQueueQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await collectionsLegalComplianceService.GetLegalNoticeServiceQueueAsync(query, cancellationToken));
    }

    [HttpGet("legal-cases/{collectionCaseId:guid}/case-file")]
    public async Task<ActionResult<LegalCaseFileResponse>> GetLegalCaseFile(
        Guid collectionCaseId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.GetLegalCaseFileAsync(
            collectionCaseId,
            cancellationToken));
    }

    [HttpGet("legal-cases/{collectionCaseId:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyCollection<LegalCaseTimelineEventResponse>>> GetLegalCaseTimeline(
        Guid collectionCaseId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.GetLegalCaseTimelineAsync(
            collectionCaseId,
            cancellationToken));
    }

    [HttpGet("residents/{residentProfileId:guid}/compliance-profile")]
    public async Task<ActionResult<ResidentComplianceProfileResponse>> GetResidentComplianceProfile(
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await collectionsLegalComplianceService.GetResidentComplianceProfileAsync(
            residentProfileId,
            cancellationToken));
    }
}
