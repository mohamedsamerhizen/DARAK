using System.Net.Http.Headers;
using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DARAK.Tests;

internal sealed class DarakApiFactory : WebApplicationFactory<Program>
{
    private readonly bool enableMockGatewayEndpoints;
    private readonly string databaseName = $"darak-http-{Guid.NewGuid():N}";
    private readonly InMemoryDatabaseRoot databaseRoot = new();
    private readonly ServiceProvider efServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private readonly Dictionary<string, string?> originalEnvironmentValues = new(StringComparer.OrdinalIgnoreCase);
    private bool environmentRestored;

    public DarakApiFactory(bool enableMockGatewayEndpoints = false)
    {
        this.enableMockGatewayEndpoints = enableMockGatewayEndpoints;

        SetTestEnvironment("ASPNETCORE_ENVIRONMENT", "Testing");
        SetTestEnvironment("ConnectionStrings__DefaultConnection", "Server=(local);Database=DARAK_HTTP_TESTS;TrustServerCertificate=True;");
        SetTestEnvironment("Jwt__Issuer", "DARAK.Http.Tests");
        SetTestEnvironment("Jwt__Audience", "DARAK.Http.Tests");
        SetTestEnvironment("Jwt__SecretKey", "DARAK_HTTP_TESTS_SECRET_KEY_1234567890");
        SetTestEnvironment("Jwt__AccessTokenMinutes", "60");
        SetTestEnvironment("Jwt__RefreshTokenDays", "7");
        SetTestEnvironment("Registration__EnablePublicRegistration", "true");
        SetTestEnvironment("Registration__AutoConfirmRegisteredUsers", "true");
        SetTestEnvironment("BootstrapAdmin__Enabled", "false");
        SetTestEnvironment("Payments__EnableMockGatewayEndpoints", enableMockGatewayEndpoints ? "true" : "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            var descriptorsToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(ApplicationDbContext) ||
                    descriptor.ServiceType == typeof(DbContextOptions) ||
                    descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options
                    .UseInMemoryDatabase(databaseName, databaseRoot)
                    .UseInternalServiceProvider(efServiceProvider)
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RestoreEnvironment();
            efServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    public async Task SeedAsync(Func<ApplicationDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    public async Task<AuthenticatedTestClient> CreateAuthenticatedClientAsync(
        UserRole role,
        Guid? compoundId = null,
        string? email = null)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        await dbContext.Database.EnsureCreatedAsync();

        var roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!createRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create test role {roleName}.");
            }
        }

        var userEmail = email ?? $"{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}@darak.test";
        var user = new ApplicationUser
        {
            Email = userEmail,
            UserName = userEmail,
            FullName = $"{roleName} HTTP User",
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, "Password@12345");
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to create test user: {errors}");
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
        if (!addRoleResult.Succeeded)
        {
            var errors = string.Join("; ", addRoleResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to add test user to role {roleName}: {errors}");
        }

        if (compoundId.HasValue && role is UserRole.CompoundAdmin or UserRole.Accountant or UserRole.Guard)
        {
            dbContext.UserCompoundAssignments.Add(new UserCompoundAssignment
            {
                UserId = user.Id,
                CompoundId = compoundId.Value,
                Role = role,
                IsActive = true
            });
            await dbContext.SaveChangesAsync();
        }

        var accessToken = await tokenService.CreateAccessTokenAsync(user);
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        return new AuthenticatedTestClient(client, user.Id, userEmail, role);
    }

    private void SetTestEnvironment(string name, string value)
    {
        if (!originalEnvironmentValues.ContainsKey(name))
        {
            originalEnvironmentValues[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }

    private void RestoreEnvironment()
    {
        if (environmentRestored)
        {
            return;
        }

        foreach (var item in originalEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(item.Key, item.Value);
        }

        environmentRestored = true;
    }
}

internal sealed record AuthenticatedTestClient(
    HttpClient Client,
    Guid UserId,
    string Email,
    UserRole Role);
