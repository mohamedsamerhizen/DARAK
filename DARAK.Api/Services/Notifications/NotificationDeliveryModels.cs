namespace DARAK.Api.Services.Notifications;

public sealed record EmailNotificationMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string Body);

public sealed record SmsNotificationMessage(
    string ToPhoneNumber,
    string Body);

public sealed record NotificationDeliveryResult(
    bool Succeeded,
    bool WasSkipped,
    string ProviderName,
    string? ProviderMessageId,
    string? ErrorMessage)
{
    public static NotificationDeliveryResult Success(
        string providerName,
        string? providerMessageId = null)
    {
        return new NotificationDeliveryResult(true, false, providerName, providerMessageId, null);
    }

    public static NotificationDeliveryResult Skipped(
        string providerName,
        string reason)
    {
        return new NotificationDeliveryResult(false, true, providerName, null, reason);
    }

    public static NotificationDeliveryResult Failed(
        string providerName,
        string errorMessage)
    {
        return new NotificationDeliveryResult(false, false, providerName, null, errorMessage);
    }
}
