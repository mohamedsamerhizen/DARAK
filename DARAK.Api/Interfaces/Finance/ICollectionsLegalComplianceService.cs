using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;

namespace DARAK.Api.Interfaces;

public interface ICollectionsLegalComplianceService
{
    Task<ServiceResult<CollectionsLegalComplianceSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PenaltyRuleResponse>> CreatePenaltyRuleAsync(
        Guid? currentUserId,
        CreatePenaltyRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PenaltyRuleResponse>> SearchPenaltyRulesAsync(
        PenaltyRuleQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CollectionCaseResponse>> CreateCollectionCaseAsync(
        Guid? currentUserId,
        CreateCollectionCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<CollectionCaseResponse>> SearchCollectionCasesAsync(
        CollectionCaseQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CollectionCaseResponse>> AdvanceCollectionCaseAsync(
        Guid id,
        Guid? currentUserId,
        AdvanceCollectionCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LegalNoticeResponse>> CreateLegalNoticeAsync(
        Guid? currentUserId,
        CreateLegalNoticeRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LegalNoticeResponse>> SearchLegalNoticesAsync(
        LegalNoticeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LegalNoticeResponse>> IssueLegalNoticeAsync(
        Guid id,
        Guid? currentUserId,
        IssueLegalNoticeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentPlanResponse>> CreatePaymentPlanAsync(
        Guid? currentUserId,
        CreatePaymentPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentPlanResponse>> PayPaymentPlanInstallmentAsync(
        Guid paymentPlanId,
        Guid installmentId,
        PayPaymentPlanInstallmentRequest request,
        CancellationToken cancellationToken = default);


    Task<PagedResult<CollectionFollowUpQueueItemResponse>> GetCollectionFollowUpQueueAsync(
        CollectionFollowUpQueueQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentComplianceProfileResponse>> GetResidentComplianceProfileAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LegalCaseManagementDashboardResponse>> GetLegalCaseManagementDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LegalCaseEscalationQueueItemResponse>> GetLegalCaseEscalationQueueAsync(
        LegalCaseEscalationQueueQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LegalNoticeServiceQueueItemResponse>> GetLegalNoticeServiceQueueAsync(
        LegalNoticeServiceQueueQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LegalCaseFileResponse>> GetLegalCaseFileAsync(
        Guid collectionCaseId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<LegalCaseTimelineEventResponse>>> GetLegalCaseTimelineAsync(
        Guid collectionCaseId,
        CancellationToken cancellationToken = default);

}
