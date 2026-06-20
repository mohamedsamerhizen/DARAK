using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;

namespace DARAK.Api.Interfaces;

public interface IMaintenanceReliabilityService
{
    Task<ServiceResult<MaintenanceAssetResponse>> CreateAssetAsync(
        CreateMaintenanceAssetRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceAssetResponse>> GetAssetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MaintenanceAssetResponse>> SearchAssetsAsync(
        MaintenanceAssetQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceSlaPolicyResponse>> CreateSlaPolicyAsync(
        CreateMaintenanceSlaPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MaintenanceSlaPolicyResponse>> SearchSlaPoliciesAsync(
        MaintenanceSlaPolicyQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderSlaSnapshotResponse>> ApplySlaToWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PreventiveMaintenancePlanResponse>> CreatePreventivePlanAsync(
        CreatePreventiveMaintenancePlanRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PreventiveMaintenancePlanResponse>> SearchPreventivePlansAsync(
        PreventiveMaintenancePlanQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderSlaSnapshotResponse>> GeneratePreventiveWorkOrderAsync(
        Guid planId,
        Guid? currentUserId,
        GeneratePreventiveWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalChecklistTemplateResponse>> CreateChecklistTemplateAsync(
        CreateOperationalChecklistTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OperationalChecklistTemplateResponse>> SearchChecklistTemplatesAsync(
        OperationalChecklistTemplateQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalChecklistRunResponse>> StartChecklistRunAsync(
        Guid? currentUserId,
        StartOperationalChecklistRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalChecklistRunResponse>> CompleteChecklistRunAsync(
        Guid id,
        Guid? currentUserId,
        CompleteOperationalChecklistRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalChecklistRunResponse>> GetChecklistRunAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceSlaRefreshResponse>> RefreshSlaBreachesAsync(
        MaintenanceReliabilitySummaryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceReliabilitySummaryResponse>> GetSummaryAsync(
        MaintenanceReliabilitySummaryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceReliabilityDashboardResponse>> GetProDashboardAsync(
        MaintenanceReliabilityDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceAssetReliabilityProfileResponse>> GetAssetReliabilityProfileAsync(
        Guid assetId,
        MaintenanceAssetReliabilityQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PreventiveMaintenanceDueQueueItemResponse>> GetPreventiveMaintenanceDueQueueAsync(
        PreventiveMaintenanceDueQueueQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MaintenanceSlaEscalationQueueItemResponse>> GetSlaEscalationQueueAsync(
        MaintenanceSlaEscalationQueueQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<VendorPerformanceItemResponse>> GetVendorPerformanceAsync(
        VendorPerformanceQuery query,
        CancellationToken cancellationToken = default);

}
