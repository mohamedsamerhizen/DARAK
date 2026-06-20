using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Maintenance;

namespace DARAK.Api.Interfaces;

public interface IMaintenanceService
{
    Task<PagedResult<MaintenanceRequestResponse>> SearchAdminAsync(
        MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> GetAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> AssignAsync(
        Guid id,
        Guid? changedByUserId,
        AssignMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> RejectAsync(
        Guid id,
        Guid? changedByUserId,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> CloseAdminAsync(
        Guid id,
        Guid? changedByUserId,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<MaintenanceRequestResponse>>> SearchResidentAsync(
        Guid userId,
        MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> GetResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> CreateResidentAsync(
        Guid userId,
        CreateMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> UpdateResidentAsync(
        Guid userId,
        Guid id,
        UpdateMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> CancelResidentAsync(
        Guid userId,
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> CloseResidentAsync(
        Guid userId,
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<MaintenanceRequestResponse>>> SearchAssignedToStaffAsync(
        Guid staffUserId,
        MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> GetAssignedToStaffAsync(
        Guid staffUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> StartAsync(
        Guid staffUserId,
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceRequestResponse>> ResolveAsync(
        Guid staffUserId,
        Guid id,
        ResolveMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<MaintenanceStatusHistoryResponse>>> GetHistoryAsync(
        Guid maintenanceRequestId,
        MaintenanceStatusHistorySearchQuery query,
        CancellationToken cancellationToken = default);
}
