using System.Net;
using DARAK.Api.Data;
using DARAK.Api.Extensions;
using DARAK.Api.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DARAK.Tests;

public sealed class StartupConfigurationHardeningTests
{
    [Fact]
    public void Unit02_AppSettingsDevelopment_ShouldUseRuntimeConfigurationKeys()
    {
        var root = FindRepositoryRoot();
        var developmentSettings = File.ReadAllText(Path.Combine(root, "DARAK.Api", "appsettings.Development.json"));

        developmentSettings.Should().Contain("\"SecretKey\"");
        developmentSettings.Should().NotContain("\"Key\"");
        developmentSettings.Should().Contain("\"DevelopmentSuperAdmin\"");
        developmentSettings.Should().NotContain("\"SuperAdmin\":");
    }

    [Fact]
    public void Unit02_DesignTimeDbContextFactory_ShouldRejectPlaceholderConnectionString()
    {
        var root = FindRepositoryRoot();
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var originalAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            Directory.SetCurrentDirectory(Path.Combine(root, "DARAK.Api"));

            var act = () => new ApplicationDbContextFactory().CreateDbContext([]);

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*Design-time connection string*placeholder*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalConnection);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNetCoreEnvironment);
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public async Task Unit02_HealthLive_ShouldReturnPublicLivenessWithoutAuthentication()
    {
        using var factory = new DarakApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Unit02_HealthRoot_ShouldRemainBackwardCompatibleLivenessWithoutDatabaseReadinessLeak()
    {
        using var factory = new DarakApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Healthy");
        body.Should().NotContain("sqlserver");
    }

    [Fact]
    public void Unit02_ServiceRegistration_ShouldCoverEveryApplicationInterface()
    {
        var services = new ServiceCollection();
        services.AddDarakApplicationServices();

        var serviceTypes = typeof(IAdminUserService).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && type.Namespace == "DARAK.Api.Interfaces")
            .OrderBy(type => type.FullName)
            .ToList();

        serviceTypes.Should().NotBeEmpty();

        foreach (var serviceType in serviceTypes)
        {
            services.Should().Contain(
                descriptor => descriptor.ServiceType == serviceType,
                $"{serviceType.FullName} should be registered through AddDarakApplicationServices");
        }
    }

    [Fact]
    public void Unit02_ProductionCompose_ShouldDefaultToProductionAndRequireSecretsFromEnvironment()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.production.yml"));
        var envExample = File.ReadAllText(Path.Combine(root, ".env.production.example"));

        compose.Should().Contain("ASPNETCORE_ENVIRONMENT: \"Production\"");
        compose.Should().Contain("${SQLSERVER_SA_PASSWORD:?");
        compose.Should().Contain("${JWT_SECRET_KEY:?");
        compose.Should().NotContain("DevelopmentSuperAdmin__Password");

        envExample.Should().Contain("ASPNETCORE_ENVIRONMENT=Production");
        envExample.Should().Contain("JWT_SECRET_KEY=YOUR_64_PLUS_CHARACTER_PRODUCTION_JWT_SECRET_HERE");
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
}

