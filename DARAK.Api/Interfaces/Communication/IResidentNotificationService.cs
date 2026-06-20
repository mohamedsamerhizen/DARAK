using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface IResidentNotificationService
{
    Task<ServiceResult<ResidentNotificationResponse>> CreateNotificationAsync(CreateResidentNotificationRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<ResidentNotificationResponse>>> SearchNotificationsAsync(ResidentNotificationSearchQuery query, Guid? currentUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ResidentNotificationResponse>> MarkNotificationAsReadAsync(Guid id, Guid? currentUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<object?>> MarkAllNotificationsAsReadAsync(Guid? currentUserId, CancellationToken cancellationToken = default);
}
