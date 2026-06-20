using DARAK.Api.Services.Notifications;

namespace DARAK.Api.Interfaces;

public interface IEmailSender
{
    Task<NotificationDeliveryResult> SendAsync(
        EmailNotificationMessage message,
        CancellationToken cancellationToken = default);
}
