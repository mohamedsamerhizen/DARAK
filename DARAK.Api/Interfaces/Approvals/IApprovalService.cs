using DARAK.Api.DTOs.Approvals;
using DARAK.Api.DTOs.Common;

namespace DARAK.Api.Interfaces;

public interface IApprovalService
{
    Task<ServiceResult<ApprovalRequestResponse>> CreateRequestAsync(
        Guid? currentUserId,
        CreateApprovalRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ApprovalRequestResponse>>> SearchRequestsAsync(
        Guid? currentUserId,
        ApprovalSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovalRequestDetailsResponse>> GetDetailsAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovalRequestDetailsResponse>> ApproveAsync(
        Guid? currentUserId,
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovalRequestDetailsResponse>> RejectAsync(
        Guid? currentUserId,
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovalRequestDetailsResponse>> CancelAsync(
        Guid? currentUserId,
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovalRequestDetailsResponse>> MarkExecutedAsync(
        Guid? currentUserId,
        Guid id,
        MarkApprovalExecutedRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovalDashboardResponse>> GetDashboardAsync(
        Guid? currentUserId,
        Guid? compoundId,
        CancellationToken cancellationToken = default);
}
