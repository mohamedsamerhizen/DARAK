using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace DARAK.Tests;

public sealed class DemoDataSeederTests
{
    private const string StrongDemoPassword = "DemoSeedStrong1!";

    [Fact]
    public async Task SeedAsync_WhenDisabled_DoesNotCreateDemoData()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["DemoSeed:Enabled"] = "false"
        });

        await DemoDataSeeder.SeedAsync(provider);

        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        dbContext.Compounds.Should().BeEmpty();
        dbContext.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_WhenProductionWithoutOverride_FailsClosed()
    {
        using var provider = CreateProvider(BuildEnabledConfig(), environmentName: "Production");

        var act = () => DemoDataSeeder.SeedAsync(provider);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development*Demo*Testing*AllowProduction*");
    }

    [Fact]
    public async Task SeedAsync_WhenNonDemoEnvironmentWithoutOverride_FailsClosed()
    {
        using var provider = CreateProvider(BuildEnabledConfig(), environmentName: "Staging");

        var act = () => DemoDataSeeder.SeedAsync(provider);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development*Demo*Testing*AllowProduction*");
    }

    [Fact]
    public async Task SeedAsync_WhenUserSeedingEnabledRequiresStrongPassword()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["DemoSeed:Enabled"] = "true",
            ["DemoSeed:SeedUsers"] = "true",
            ["DemoSeed:DemoPassword"] = "password"
        });

        var act = () => DemoDataSeeder.SeedAsync(provider);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*DemoPassword*");
    }

    [Fact]
    public async Task SeedAsync_WhenEnabled_CreatesBroadIdempotentHashedDemoDataset()
    {
        using var provider = CreateProvider(BuildEnabledConfig());
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        await DemoDataSeeder.SeedAsync(provider);
        var firstCounts = CaptureCounts(dbContext);
        await DemoDataSeeder.SeedAsync(provider);
        var secondCounts = CaptureCounts(dbContext);

        secondCounts.Should().Be(firstCounts);
        dbContext.Compounds.Select(compound => compound.Code).Should().BeEquivalentTo("DEMO-RIVER", "DEMO-GARDEN");
        dbContext.PropertyUnits.Should().HaveCount(72);
        dbContext.ResidentProfiles.Should().HaveCount(30);
        dbContext.UtilityBills.Should().NotBeEmpty();
        dbContext.Payments.Should().NotBeEmpty();
        dbContext.RentContracts.Should().NotBeEmpty();
        dbContext.PropertySaleContracts.Should().NotBeEmpty();
        dbContext.VisitorPasses.Should().NotBeEmpty();
        dbContext.WorkOrders.Should().NotBeEmpty();
        dbContext.DocumentFiles.Should().NotBeEmpty();
        dbContext.ReportExportJobs.Should().NotBeEmpty();
        dbContext.AuditLogEntries.Should().NotBeEmpty();
        dbContext.NotificationOutboxes.Should().NotBeEmpty();
        dbContext.VisitorPasses.Select(pass => pass.AccessCode).ToArray().Should().OnlyContain(value => IsHashedAccessValue(value));
        dbContext.AccessCredentials.Select(credential => credential.CredentialCode).ToArray().Should().OnlyContain(value => IsHashedAccessValue(value));
        dbContext.Payments.Should().OnlyContain(payment => payment.Amount > 0);
        dbContext.Payments.Where(payment => payment.TargetType == PaymentTargetType.UtilityBill)
            .Should()
            .OnlyContain(payment => dbContext.UtilityBills.Any(bill => bill.Id == payment.TargetId));
    }

    private static Dictionary<string, string?> BuildEnabledConfig()
    {
        return new Dictionary<string, string?>
        {
            ["DemoSeed:Enabled"] = "true",
            ["DemoSeed:SeedUsers"] = "true",
            ["DemoSeed:DemoPassword"] = StrongDemoPassword,
            ["DemoSeed:AllowProduction"] = "false"
        };
    }

    private static ServiceProvider CreateProvider(
        Dictionary<string, string?> configurationValues,
        string environmentName = "Testing")
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(environmentName));
        services.AddSingleton<IAccessCodeHasher, AccessCodeHasher>();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"demo-seed-{Guid.NewGuid():N}"));
        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    private static DemoCounts CaptureCounts(ApplicationDbContext dbContext)
    {
        return new DemoCounts(
            dbContext.Compounds.Count(),
            dbContext.PropertyUnits.Count(),
            dbContext.ResidentProfiles.Count(),
            dbContext.UtilityBills.Count(),
            dbContext.Payments.Count(),
            dbContext.VisitorPasses.Count(),
            dbContext.AccessCredentials.Count(),
            dbContext.WorkOrders.Count(),
            dbContext.NotificationOutboxes.Count(),
            dbContext.AuditLogEntries.Count());
    }

    private static bool IsHashedAccessValue(string value)
    {
        return value.StartsWith("AC2$", StringComparison.Ordinal)
            || value.StartsWith("SHA256HEX$", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DARAK.Tests";

        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record DemoCounts(
        int Compounds,
        int Units,
        int Residents,
        int Bills,
        int Payments,
        int VisitorPasses,
        int AccessCredentials,
        int WorkOrders,
        int NotificationOutboxes,
        int AuditEntries);
}
