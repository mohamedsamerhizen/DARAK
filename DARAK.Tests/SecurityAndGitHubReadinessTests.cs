using DARAK.Api.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DARAK.Tests;

public sealed class SecurityAndGitHubReadinessTests
{
    [Fact]
    public void Phase14_StartupSecurityValidator_ShouldRejectPlaceholderJwtSecret()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "YOUR_JWT_SECRET_KEY_HERE",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment("Production"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*JWT secret key*placeholder*");
    }

    [Fact]
    public void Phase14_StartupSecurityValidator_ShouldRejectEnabledBootstrapAdminPlaceholders()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "DARAK_TESTS_SECRET_KEY_1234567890",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:Email"] = "YOUR_SUPERADMIN_EMAIL_HERE",
            ["BootstrapAdmin:Password"] = "YOUR_SUPERADMIN_PASSWORD_HERE"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment(Environments.Development));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*BootstrapAdmin email*placeholder*");
    }

    [Fact]
    public void Phase02_StartupSecurityValidator_ShouldRejectProductionRegistrationAutoConfirm()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "DARAK_TESTS_SECRET_KEY_1234567890",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Registration:EnablePublicRegistration"] = "true",
            ["Registration:AutoConfirmRegisteredUsers"] = "true"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment("Production"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*AutoConfirmRegisteredUsers*Production*");
    }

    [Fact]
    public void Phase02_StartupSecurityValidator_ShouldRejectUnsafeBootstrapPassword()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "DARAK_TESTS_SECRET_KEY_1234567890",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:Email"] = "superadmin@darak.test",
            ["BootstrapAdmin:Password"] = "weak"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment("Production"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*BootstrapAdmin password*at least 12 characters*");
    }

    [Fact]
    public void Phase02_StartupSecurityValidator_ShouldAllowTestingWithTestSafeConfig()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=DARAK_TESTS;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "TEST_SECRET",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Registration:EnablePublicRegistration"] = "true",
            ["Registration:AutoConfirmRegisteredUsers"] = "true",
            ["BootstrapAdmin:Enabled"] = "false"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment("Testing"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Phase02_StartupSecurityValidator_ShouldAllowDevelopmentWithSafeLocalConfig()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "DARAK_TESTS_SECRET_KEY_1234567890",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Registration:EnablePublicRegistration"] = "true",
            ["Registration:AutoConfirmRegisteredUsers"] = "true",
            ["BootstrapAdmin:Enabled"] = "false"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment(Environments.Development));

        act.Should().NotThrow();
    }

    [Fact]
    public void Phase02_StartupSecurityValidator_ShouldRejectAutoConfirmWhenPublicRegistrationDisabled()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "DARAK_TESTS_SECRET_KEY_1234567890",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Registration:EnablePublicRegistration"] = "false",
            ["Registration:AutoConfirmRegisteredUsers"] = "true"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment(Environments.Development));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*AutoConfirmRegisteredUsers*EnablePublicRegistration*false*");
    }

    [Fact]
    public void Phase14_StartupSecurityValidator_ShouldRejectEnabledSmsWithoutRealApiKey()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=DARAKDb;TrustServerCertificate=True;",
            ["Jwt:Issuer"] = "DARAK.Tests",
            ["Jwt:Audience"] = "DARAK.Tests",
            ["Jwt:SecretKey"] = "DARAK_TESTS_SECRET_KEY_1234567890",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Notifications:Sms:Enabled"] = "true",
            ["Notifications:Sms:EndpointUrl"] = "https://sms-provider.example/send",
            ["Notifications:Sms:ApiKey"] = "YOUR_SMS_PROVIDER_API_KEY_HERE"
        });

        var act = () => StartupSecurityValidator.Validate(configuration, new TestHostEnvironment("Production"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Notifications:Sms:ApiKey*");
    }

    [Fact]
    public void Phase14_GitIgnore_ShouldBlockLocalSecretsAndGeneratedArtifacts()
    {
        var root = FindRepositoryRoot();
        var gitIgnore = File.ReadAllText(Path.Combine(root, ".gitignore"));

        gitIgnore.Should().Contain(".env");
        gitIgnore.Should().Contain("*.zip");
        gitIgnore.Should().Contain("**/*.zip");
        gitIgnore.Should().Contain("**/bin/");
        gitIgnore.Should().Contain("**/obj/");
        gitIgnore.Should().Contain("logs/");
        gitIgnore.Should().Contain("DARAK.Api/App_Data/Uploads/");
    }


    [Fact]
    public void Phase14_DockerIgnore_ShouldExcludeLocalArtifactsFromBuildContext()
    {
        var root = FindRepositoryRoot();
        var dockerIgnore = File.ReadAllText(Path.Combine(root, ".dockerignore"));

        dockerIgnore.Should().Contain("**/bin");
        dockerIgnore.Should().Contain("**/obj");
        dockerIgnore.Should().Contain("**/*.zip");
        dockerIgnore.Should().Contain(".env.*");
        dockerIgnore.Should().Contain("DARAK.Api/App_Data/Uploads");
    }

    [Fact]
    public void Phase14_DockerCompose_ShouldReadSensitiveValuesOnlyFromEnvironmentVariables()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));

        compose.Should().Contain("${SQLSERVER_SA_PASSWORD}");
        compose.Should().Contain("${JWT_SECRET_KEY}");
        compose.Should().Contain("${REGISTRATION_ENABLE_PUBLIC_REGISTRATION:-true}");
        compose.Should().Contain("${REGISTRATION_AUTO_CONFIRM_REGISTERED_USERS:-true}");
        compose.Should().Contain("${BOOTSTRAP_ADMIN_PASSWORD:-}");
        compose.Should().Contain("${NOTIFICATIONS_EMAIL_PASSWORD:-}");
        compose.Should().Contain("${NOTIFICATIONS_SMS_API_KEY:-}");
    }

    [Fact]
    public void Phase14_CleanBeforeGitHubScript_ShouldRemoveAndValidateRiskyArtifacts()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "tools", "Clean-BeforeGitHub.ps1"));

        script.Should().Contain("DARAK.sln");
        script.Should().Contain(".env.local.backup");
        script.Should().Contain("*.zip");
        script.Should().Contain("bin,obj,TestResults,coverage");
        script.Should().Contain("DARAK.Api\\App_Data\\Uploads");
        script.Should().Contain("blockedFiles");
    }

    [Fact]
    public void Phase14_AppSettingsTemplates_ShouldUsePlaceholdersForSensitiveValues()
    {
        var root = FindRepositoryRoot();
        var appSettings = File.ReadAllText(Path.Combine(root, "DARAK.Api", "appsettings.json"));
        var developmentSettings = File.ReadAllText(Path.Combine(root, "DARAK.Api", "appsettings.Development.json"));
        var envExample = File.ReadAllText(Path.Combine(root, ".env.example"));

        appSettings.Should().Contain("YOUR_SQLSERVER_PASSWORD_HERE");
        appSettings.Should().Contain("YOUR_JWT_SECRET_KEY_HERE");
        developmentSettings.Should().Contain("\"BootstrapAdmin\"");
        developmentSettings.Should().NotContain("YOUR_SUPERADMIN_PASSWORD_HERE");
        envExample.Should().Contain("YOUR_SQLSERVER_PASSWORD_HERE");
        envExample.Should().Contain("YOUR_JWT_SECRET_KEY_HERE");
        envExample.Should().Contain("YOUR_SUPERADMIN_PASSWORD_HERE");
        envExample.Should().Contain("YOUR_SMS_PROVIDER_API_KEY_HERE");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DARAK.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("DARAK repository root could not be located.");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DARAK.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
