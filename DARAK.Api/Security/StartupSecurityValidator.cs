using System.Text;
using DARAK.Api.Authentication;
using DARAK.Api.Services.Notifications;
using Microsoft.Extensions.Hosting;

namespace DARAK.Api.Security;

public static class StartupSecurityValidator
{
    private static readonly string[] PlaceholderMarkers =
    [
        "YOUR_",
        "CHANGE_ME",
        "REPLACE_ME",
        "TODO_",
        "EXAMPLE_",
        "PLACEHOLDER"
    ];

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        ValidateConnectionString(configuration, environment);
        ValidateJwt(configuration);
        ValidateDevelopmentSuperAdmin(configuration, environment);
        ValidateNotificationProviders(configuration);
    }

    public static bool IsPlaceholderSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return PlaceholderMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        if (!environment.IsEnvironment("Testing") && IsPlaceholderSecret(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' still contains a placeholder value. Configure it through environment variables, user secrets, or an ignored .env file before running the API.");
        }
    }

    private static void ValidateJwt(IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
        {
            throw new InvalidOperationException("JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
        {
            throw new InvalidOperationException("JWT audience is not configured.");
        }

        if (IsPlaceholderSecret(jwtOptions.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT secret key is missing or still contains a placeholder value. Configure Jwt__SecretKey with a private value of at least 32 bytes.");
        }

        if (Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
        {
            throw new InvalidOperationException("JWT secret key must be at least 32 bytes.");
        }

        if (jwtOptions.AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("JWT access token lifetime must be positive.");
        }

        if (jwtOptions.RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("JWT refresh token lifetime must be positive.");
        }
    }

    private static void ValidateDevelopmentSuperAdmin(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var email = configuration["DevelopmentSuperAdmin:Email"];
        var password = configuration["DevelopmentSuperAdmin:Password"];

        if (IsPlaceholderSecret(email))
        {
            throw new InvalidOperationException(
                "Development SuperAdmin email is missing or still contains a placeholder value.");
        }

        if (IsPlaceholderSecret(password))
        {
            throw new InvalidOperationException(
                "Development SuperAdmin password is missing or still contains a placeholder value.");
        }
    }

    private static void ValidateNotificationProviders(IConfiguration configuration)
    {
        var notificationOptions = configuration
            .GetSection(NotificationOptions.SectionName)
            .Get<NotificationOptions>()
            ?? new NotificationOptions();

        if (notificationOptions.Email.Enabled)
        {
            ValidateConfiguredValue(
                notificationOptions.Email.Host,
                "Notifications:Email:Host must be configured when SMTP email delivery is enabled.");

            ValidateConfiguredValue(
                notificationOptions.Email.FromEmail,
                "Notifications:Email:FromEmail must be configured when SMTP email delivery is enabled.");

            if (!string.IsNullOrWhiteSpace(notificationOptions.Email.Username))
            {
                ValidateConfiguredValue(
                    notificationOptions.Email.Password,
                    "Notifications:Email:Password must be configured when SMTP username is configured.");
            }
        }

        if (notificationOptions.Sms.Enabled)
        {
            ValidateConfiguredValue(
                notificationOptions.Sms.EndpointUrl,
                "Notifications:Sms:EndpointUrl must be configured when SMS delivery is enabled.");

            ValidateConfiguredValue(
                notificationOptions.Sms.ApiKey,
                "Notifications:Sms:ApiKey must be configured when SMS delivery is enabled.");
        }
    }

    private static void ValidateConfiguredValue(string? value, string errorMessage)
    {
        if (IsPlaceholderSecret(value))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
