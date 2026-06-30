using DARAK.Api.Data;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace DARAK.Tests;

public sealed class BootstrapAdminSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenBootstrapDisabled_DoesNotCreateSuperAdmin()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Enabled"] = "false"
        });

        await IdentitySeeder.SeedAsync(provider);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var roleName in Enum.GetNames<UserRole>())
        {
            (await roleManager.RoleExistsAsync(roleName)).Should().BeTrue();
        }

        var superAdmins = await userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin));
        superAdmins.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_WhenBootstrapEnabled_CreatesFirstSuperAdmin()
    {
        const string password = "BootstrapStrong1!";
        using var provider = CreateProvider(BuildEnabledBootstrapConfig(
            "first-superadmin@darak.test",
            password));

        await IdentitySeeder.SeedAsync(provider);

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("first-superadmin@darak.test");

        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeTrue();
        user.LockoutEnabled.Should().BeTrue();
        user.FullName.Should().Be("Bootstrap SuperAdmin");
        (await userManager.IsInRoleAsync(user, nameof(UserRole.SuperAdmin))).Should().BeTrue();
        (await userManager.CheckPasswordAsync(user, password)).Should().BeTrue();
        user.PasswordHash.Should().NotContain(password);
    }

    [Fact]
    public async Task SeedAsync_WhenExistingSuperAdminExists_DoesNotCreateAnother()
    {
        using var provider = CreateProvider(BuildEnabledBootstrapConfig(
            "new-superadmin@darak.test",
            "BootstrapStrong1!"));
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        await roleManager.CreateAsync(new IdentityRole<Guid>(nameof(UserRole.SuperAdmin)));
        var existing = new ApplicationUser
        {
            Email = "existing-superadmin@darak.test",
            UserName = "existing-superadmin@darak.test",
            FullName = "Existing SuperAdmin",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        (await userManager.CreateAsync(existing, "ExistingStrong1!")).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(existing, nameof(UserRole.SuperAdmin))).Succeeded.Should().BeTrue();

        await IdentitySeeder.SeedAsync(provider);

        var superAdmins = await userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin));
        superAdmins.Should().ContainSingle();
        (await userManager.FindByEmailAsync("new-superadmin@darak.test")).Should().BeNull();
    }

    [Fact]
    public async Task SeedAsync_WhenBootstrapConfigIncomplete_FailsSafely()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:Email"] = "",
            ["BootstrapAdmin:Password"] = "",
            ["BootstrapAdmin:FullName"] = "Bootstrap SuperAdmin"
        });

        var act = () => IdentitySeeder.SeedAsync(provider);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*BootstrapAdmin email*");

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var superAdmins = await userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin));
        superAdmins.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_WhenExistingEmailHasNoRole_AssignsFirstSuperAdminRole()
    {
        using var provider = CreateProvider(BuildEnabledBootstrapConfig(
            "existing-user@darak.test",
            "BootstrapStrong1!"));
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = new ApplicationUser
        {
            Email = "existing-user@darak.test",
            UserName = "existing-user@darak.test",
            FullName = "Unconfirmed Existing User",
            EmailConfirmed = false,
            LockoutEnabled = false
        };
        (await userManager.CreateAsync(existing, "ExistingStrong1!")).Succeeded.Should().BeTrue();

        await IdentitySeeder.SeedAsync(provider);

        var user = await userManager.FindByEmailAsync("existing-user@darak.test");
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeTrue();
        user.LockoutEnabled.Should().BeTrue();
        user.FullName.Should().Be("Bootstrap SuperAdmin");
        (await userManager.IsInRoleAsync(user, nameof(UserRole.SuperAdmin))).Should().BeTrue();
    }

    private static Dictionary<string, string?> BuildEnabledBootstrapConfig(
        string email,
        string password)
    {
        return new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:Email"] = email,
            ["BootstrapAdmin:Password"] = password,
            ["BootstrapAdmin:FullName"] = "Bootstrap SuperAdmin"
        };
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> configurationValues)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"bootstrap-admin-{Guid.NewGuid():N}"));
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

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "DARAK.Tests";

        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
