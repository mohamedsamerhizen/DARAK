using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.ResidentLifecycle;

namespace DARAK.Api.Interfaces;

public interface IResidentLifecycleService
{
    Task<ServiceResult<ResidentLifecycleProcessResponse>> CreateProcessAsync(
        Guid? currentUserId,
        CreateResidentLifecycleProcessRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentLifecycleProcessResponse>> GetProcessAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ResidentLifecycleProcessResponse>> SearchProcessesAsync(
        ResidentLifecycleQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentLifecycleProcessResponse>> ConfirmFinancialClearanceAsync(
        Guid id,
        Guid? currentUserId,
        ConfirmLifecycleFinancialClearanceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentLifecycleProcessResponse>> CompleteProcessAsync(
        Guid id,
        Guid? currentUserId,
        CompleteResidentLifecycleProcessRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentCustodyItemResponse>> IssueCustodyItemAsync(
        Guid? currentUserId,
        IssueCustodyItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentCustodyItemResponse>> ReturnCustodyItemAsync(
        Guid id,
        Guid? currentUserId,
        ReturnCustodyItemRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ResidentCustodyItemResponse>> SearchCustodyItemsAsync(
        CustodyItemQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MoveLogisticsPermitResponse>> CreateMovePermitAsync(
        Guid? currentUserId,
        CreateMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MoveLogisticsPermitResponse>> DecideMovePermitAsync(
        Guid id,
        Guid? currentUserId,
        DecideMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MoveLogisticsPermitResponse>> CompleteMovePermitAsync(
        Guid id,
        Guid? currentUserId,
        CompleteMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MoveLogisticsPermitResponse>> SearchMovePermitsAsync(
        MoveLogisticsPermitQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitReadinessRecordResponse>> CreateUnitReadinessRecordAsync(
        Guid? currentUserId,
        CreateUnitReadinessRecordRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitReadinessRecordResponse>> UpdateUnitReadinessStatusAsync(
        Guid id,
        Guid? currentUserId,
        UpdateUnitReadinessStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<UnitReadinessRecordResponse>> SearchUnitReadinessRecordsAsync(
        UnitReadinessQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitDamageLiabilityResponse>> CreateDamageLiabilityAsync(
        Guid? currentUserId,
        CreateUnitDamageLiabilityRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitDamageLiabilityResponse>> UpdateDamageLiabilityStatusAsync(
        Guid id,
        Guid? currentUserId,
        UpdateDamageLiabilityStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentCustodyItemResponse>> UpdateCustodyItemSettlementStatusAsync(
        Guid id,
        Guid? currentUserId,
        UpdateCustodySettlementStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MoveOutOperationalSettlementResponse>> GetMoveOutOperationalSettlementAsync(
        Guid residentLifecycleProcessId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>> RecordMoveOutFinalMeterReadingsAsync(
        Guid residentLifecycleProcessId,
        Guid? currentUserId,
        RecordMoveOutFinalMeterReadingsRequest request,
        CancellationToken cancellationToken = default);


    Task<ServiceResult<MoveOutExitCertificateResponse>> GetMoveOutExitCertificateAsync(
        Guid residentLifecycleProcessId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnitReadinessRecordResponse>> PrepareMoveOutUnitTurnoverAsync(
        Guid residentLifecycleProcessId,
        Guid? currentUserId,
        PrepareMoveOutUnitTurnoverRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MoveOutUnitTurnoverTimelineResponse>> GetMoveOutUnitTurnoverTimelineAsync(
        Guid residentLifecycleProcessId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MoveOutReadinessResponse>> GetMoveOutReadinessAsync(
        MoveOutReadinessQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentLifecycleSummaryResponse>> GetSummaryAsync(
        ResidentLifecycleSummaryQuery query,
        CancellationToken cancellationToken = default);
}
