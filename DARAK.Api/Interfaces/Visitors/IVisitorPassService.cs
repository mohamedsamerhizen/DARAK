using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;

namespace DARAK.Api.Interfaces;

public interface IVisitorPassService
{
    Task<PagedResult<VisitorPassResponse>> SearchAdminAsync(
        VisitorPassSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> GetAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> DenyAsync(
        Guid id,
        DenyVisitorPassRequest request,
        Guid? guardUserId = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> CancelAdminAsync(
        Guid id,
        CancelVisitorPassRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<VisitorPassResponse>>> SearchResidentAsync(
        Guid userId,
        VisitorPassSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> GetResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> CreateResidentAsync(
        Guid userId,
        CreateVisitorPassRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> CancelResidentAsync(
        Guid userId,
        Guid id,
        CancelVisitorPassRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<VisitorPassResponse>> SearchTodayForGuardAsync(
        Guid? guardUserId,
        VisitorPassSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> VerifyAccessCodeAsync(
        Guid? guardUserId,
        VerifyVisitorPassAccessCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> GetGuardAsync(
        Guid? guardUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> CheckInAsync(
        Guid id,
        Guid? guardUserId,
        VisitorPassAccessRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<VisitorPassResponse>> CheckOutAsync(
        Guid id,
        Guid? guardUserId,
        VisitorPassAccessRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<VisitorAccessLogResponse>>> GetAccessLogsAsync(
        Guid visitorPassId,
        VisitorAccessLogSearchQuery query,
        CancellationToken cancellationToken = default);
}
