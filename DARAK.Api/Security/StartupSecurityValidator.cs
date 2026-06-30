using System.ComponentModel.DataAnnotations;
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
        ValidateJwt(configuration, environment);
        ValidateRegistration(configuration, environment);
        ValidateBootstrapAdmin(configuration);
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

        if (environment.IsProduction() && IsPlaceholderSecret(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' still contains a placeholder value. Configure it through environment variables, user secrets, or an ignored .env file before running the API in Production.");
        }
    }

    private static void ValidateJwt(IConfiguration configuration, IHostEnvironment environment)
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

        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT secret key is missing. Configure Jwt__SecretKey with a private value.");
        }

        if (!environment.IsEnvironment("Testing") && IsPlaceholderSecret(jwtOptions.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT secret key is missing or still contains a placeholder value. Configure Jwt__SecretKey with a private value of at least 32 bytes.");
        }

        if (!environment.IsEnvironment("Testing") && Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
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

    private static void ValidateRegistration(IConfiguration configuration, IHostEnvironment environment)
    {
        var registrationOptions = configuration
            .GetSection(RegistrationOptions.SectionName)
            .Get<RegistrationOptions>()
            ?? new RegistrationOptions();

        if (!registrationOptions.EnablePublicRegistration && registrationOptions.AutoConfirmRegisteredUsers)
        {
            throw new InvalidOperationException(
                "Registration:AutoConfirmRegisteredUsers cannot be enabled when Registration:EnablePublicRegistration is false.");
        }

        if (environment.IsProduction() && registrationOptions.AutoConfirmRegisteredUsers)
        {
            throw new InvalidOperationException(
                "Registration:AutoConfirmRegisteredUsers cannot be enabled in Production. Use administrator provisioning or a real confirmation workflow.");
        }
    }

    private static void ValidateBootstrapAdmin(IConfiguration configuration)
    {
        var bootstrapOptions = configuration
            .GetSection(BootstrapAdminOptions.SectionName)
            .Get<BootstrapAdminOptions>()
            ?? new BootstrapAdminOptions();

        if (!bootstrapOptions.Enabled)
        {
            return;
        }

        if (IsPlaceholderSecret(bootstrapOptions.Email))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin email is missing or still contains a placeholder value.");
        }

        if (!new EmailAddressAttribute().IsValid(bootstrapOptions.Email))
        {
            throw new InvalidOperationException("BootstrapAdmin email is not a valid email address.");
        }

        if (IsPlaceholderSecret(bootstrapOptions.Password))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin password is missing or still contains a placeholder value.");
        }

        if (!IsStrongBootstrapPassword(bootstrapOptions.Password))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin password must be at least 12 characters and include uppercase, lowercase, digit, and non-alphanumeric characters.");
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

    private static bool IsStrongBootstrapPassword(string password)
    {
        return password.Length >= 12
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(character => !char.IsLetterOrDigit(character));
    }

    private static void ValidateConfiguredValue(string? value, string errorMessage)
    {
        if (IsPlaceholderSecret(value))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
