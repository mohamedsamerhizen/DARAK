using DARAK.Api.Entities;
using DARAK.Api.Enums;

namespace DARAK.Api.Services;

internal static class ResidentNotificationPreferencePolicy
{
    public static bool IsMandatory(
        ResidentNotificationType type,
        ResidentNotificationSeverity severity,
        NotificationPriority priority)
    {
        return severity == ResidentNotificationSeverity.Critical
            || priority == NotificationPriority.Urgent
            || type == ResidentNotificationType.Payment && severity >= ResidentNotificationSeverity.Warning;
    }

    public static string? GetSuppressionReason(
        ResidentNotificationPreference? preference,
        ResidentNotificationType type,
        ResidentNotificationSeverity severity,
        NotificationPriority priority,
        DateTime scheduledAtUtc,
        bool requireCampaignEnabled = false)
    {
        if (preference is null || IsMandatory(type, severity, priority))
        {
            return null;
        }

        if (!preference.InAppEnabled)
        {
            return "In-app notifications are disabled.";
        }

        if (requireCampaignEnabled && !preference.CampaignNotificationsEnabled)
        {
            return "Campaign notifications are disabled.";
        }

        var categoryDisabled = type switch
        {
            ResidentNotificationType.Payment => !preference.PaymentNotificationsEnabled,
            ResidentNotificationType.Maintenance => !preference.MaintenanceNotificationsEnabled,
            ResidentNotificationType.Complaint => !preference.ComplaintNotificationsEnabled,
            ResidentNotificationType.Violation => !preference.ViolationNotificationsEnabled,
            ResidentNotificationType.Visitor => !preference.VisitorNotificationsEnabled,
            ResidentNotificationType.Announcement => !preference.AnnouncementNotificationsEnabled,
            _ => false
        };
        if (categoryDisabled)
        {
            return $"{type} notifications are disabled.";
        }

        if (!preference.DoNotDisturbEnabled
            || !preference.DoNotDisturbStartLocalTime.HasValue
            || !preference.DoNotDisturbEndLocalTime.HasValue)
        {
            return null;
        }

        var localTime = scheduledAtUtc.TimeOfDay;
        var start = preference.DoNotDisturbStartLocalTime.Value;
        var end = preference.DoNotDisturbEndLocalTime.Value;
        var isInsideWindow = start <= end
            ? localTime >= start && localTime <= end
            : localTime >= start || localTime <= end;

        return isInsideWindow ? "Do-not-disturb window is active." : null;
    }
}
