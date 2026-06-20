using DARAK.Api.DTOs.Commercial;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface ICommercialEngineService
{
    Task<ServiceResult<CommercialEngineDashboardResponse>> GetDashboardAsync(
        CommercialDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<BillingRuleResponse>>> SearchBillingRulesAsync(
        BillingRuleSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingRuleResponse>> GetBillingRuleAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingRuleResponse>> CreateBillingRuleAsync(
        Guid? currentUserId,
        CreateBillingRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingRuleResponse>> AddBillingRuleTierAsync(
        Guid? currentUserId,
        Guid id,
        AddBillingRuleTierRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<MeterReadingCorrectionResponse>>> SearchMeterCorrectionsAsync(
        MeterReadingCorrectionSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingCorrectionResponse>> CreateMeterCorrectionAsync(
        Guid? currentUserId,
        CreateMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingCorrectionResponse>> ApproveMeterCorrectionAsync(
        Guid? currentUserId,
        Guid id,
        DecideMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingCorrectionResponse>> RejectMeterCorrectionAsync(
        Guid? currentUserId,
        Guid id,
        DecideMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractLifecycleEventResponse>> CreateContractLifecycleEventAsync(
        Guid? currentUserId,
        CreateContractLifecycleEventRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ContractLifecycleEventResponse>>> GetContractTimelineAsync(
        CommercialContractType contractType,
        Guid contractId,
        ContractLifecycleTimelineQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitHandoverChecklistResponse>> CreateUnitHandoverChecklistAsync(
        Guid? currentUserId,
        CreateUnitHandoverChecklistRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitHandoverChecklistResponse>> CompleteUnitHandoverChecklistAsync(
        Guid? currentUserId,
        Guid id,
        CompleteUnitHandoverChecklistRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OwnershipTransferRequestResponse>> CreateOwnershipTransferAsync(
        Guid? currentUserId,
        CreateOwnershipTransferRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OwnershipTransferRequestResponse>> ApproveOwnershipTransferAsync(
        Guid? currentUserId,
        Guid id,
        DecideOwnershipTransferRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OwnershipTransferRequestResponse>> RejectOwnershipTransferAsync(
        Guid? currentUserId,
        Guid id,
        DecideOwnershipTransferRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InstallmentRescheduleRequestResponse>> CreateInstallmentRescheduleAsync(
        Guid? currentUserId,
        CreateInstallmentRescheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InstallmentRescheduleRequestResponse>> ApproveInstallmentRescheduleAsync(
        Guid? currentUserId,
        Guid id,
        DecideInstallmentRescheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InstallmentRescheduleRequestResponse>> RejectInstallmentRescheduleAsync(
        Guid? currentUserId,
        Guid id,
        DecideInstallmentRescheduleRequest request,
        CancellationToken cancellationToken = default);
}
