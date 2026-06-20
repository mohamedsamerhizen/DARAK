using DARAK.Api.Services.Notifications;

namespace DARAK.Api.Interfaces;

public interface ISmsSender
{
    Task<NotificationDeliveryResult> SendAsync(
        SmsNotificationMessage message,
        CancellationToken cancellationToken = default);
}
