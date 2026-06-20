using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Notifications;

namespace DARAK.Api.Interfaces;

public interface INotificationOutboxService
{
    Task<ServiceResult<NotificationOutboxResponse>> EnqueueAsync(
        Guid? currentUserId,
        EnqueueNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<NotificationOutboxResponse>> EnqueueManualAsync(
        Guid? currentUserId,
        ManualNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<NotificationOutboxResponse>>> SearchAsync(
        Guid? currentUserId,
        NotificationSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<NotificationOutboxResponse>> GetAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<NotificationOutboxResponse>> MarkForRetryAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<NotificationDashboardSummaryResponse>> GetDashboardSummaryAsync(
        Guid? currentUserId,
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<int> ProcessDueNotificationsAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
