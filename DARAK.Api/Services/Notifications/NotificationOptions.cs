namespace DARAK.Api.Services.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool WorkerEnabled { get; set; }

    public int WorkerIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 25;

    public int RetryDelayMinutes { get; set; } = 10;

    public int RetryBackoffMultiplier { get; set; } = 1;

    public int MaxRetryDelayMinutes { get; set; } = 120;

    public int ProcessingTimeoutMinutes { get; set; } = 15;

    public EmailNotificationOptions Email { get; set; } = new();

    public SmsNotificationOptions Sms { get; set; } = new();
}

public sealed class EmailNotificationOptions
{
    public bool Enabled { get; set; }

    public string ProviderName { get; set; } = "SMTP";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "DARAK";
}

public sealed class SmsNotificationOptions
{
    public bool Enabled { get; set; }

    public string ProviderName { get; set; } = "HTTP-SMS";

    public string EndpointUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string SenderId { get; set; } = "DARAK";
}
