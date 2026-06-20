using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;

namespace DARAK.Api.Interfaces;

public interface IOperationsService
{
    Task<ServiceResult<WorkOrderResponse>> CreateWorkOrderAsync(
        Guid? currentUserId,
        CreateWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> UpdateWorkOrderAsync(
        Guid id,
        UpdateWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> GetWorkOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkOrderResponse>> SearchWorkOrdersAsync(
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<WorkOrderResponse>>> SearchWorkOrdersForStaffMemberAsync(
        Guid staffMemberId,
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<WorkOrderResponse>>> SearchWorkOrdersForVendorAsync(
        Guid vendorId,
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> AssignWorkOrderToStaffAsync(
        Guid id,
        Guid? currentUserId,
        AssignWorkOrderToStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> AssignWorkOrderToVendorAsync(
        Guid id,
        Guid? currentUserId,
        AssignWorkOrderToVendorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> ScheduleWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        ScheduleWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> StartWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        StartWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> CompleteWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        CompleteWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderResponse>> CancelWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        CancelWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderCostItemResponse>> AddCostItemAsync(
        Guid id,
        AddWorkOrderCostItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<WorkOrderStatusHistoryResponse>>> GetStatusHistoryAsync(
        Guid id,
        WorkOrderStatusHistoryQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderRatingResponse>> RateWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CreateWorkOrderRatingRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkOrderResponse>> SearchOverdueWorkOrdersAsync(
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default);
}
