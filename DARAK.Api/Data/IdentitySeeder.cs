using System.ComponentModel.DataAnnotations;
using DARAK.Api.Authentication;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DARAK.Api.Data;

public static class IdentitySeeder
{
    private const string DefaultBootstrapFullName = "System Administrator";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        await BootstrapFirstSuperAdminAsync(services);
    }

    private static async Task BootstrapFirstSuperAdminAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var options = configuration
            .GetSection(BootstrapAdminOptions.SectionName)
            .Get<BootstrapAdminOptions>()
            ?? new BootstrapAdminOptions();

        if (!options.Enabled)
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(nameof(IdentitySeeder));
        var existingSuperAdmins = await userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin));
        if (existingSuperAdmins.Count > 0)
        {
            logger?.LogInformation("BootstrapAdmin is enabled, but a SuperAdmin already exists. Bootstrap skipped.");
            return;
        }

        ValidateBootstrapOptions(options);

        var email = options.Email.Trim();
        var fullName = string.IsNullOrWhiteSpace(options.FullName)
            ? DefaultBootstrapFullName
            : options.FullName.Trim();
        var superAdmin = await userManager.FindByEmailAsync(email);

        if (superAdmin is null)
        {
            superAdmin = new ApplicationUser
            {
                Email = email,
                UserName = email,
                FullName = fullName,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(superAdmin, options.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to bootstrap SuperAdmin: {errors}");
            }

            logger?.LogInformation("BootstrapAdmin created the first SuperAdmin account for {Email}.", email);
        }
        else if (!superAdmin.EmailConfirmed || !superAdmin.LockoutEnabled || superAdmin.FullName != fullName)
        {
            superAdmin.EmailConfirmed = true;
            superAdmin.LockoutEnabled = true;
            superAdmin.FullName = fullName;

            var updateResult = await userManager.UpdateAsync(superAdmin);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join("; ", updateResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to prepare BootstrapAdmin account: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(superAdmin, nameof(UserRole.SuperAdmin)))
        {
            var roleResult = await userManager.AddToRoleAsync(superAdmin, nameof(UserRole.SuperAdmin));
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to assign BootstrapAdmin role: {errors}");
            }
        }

        logger?.LogInformation("BootstrapAdmin ensured the first SuperAdmin role assignment for {Email}.", email);
    }

    private static void ValidateBootstrapOptions(BootstrapAdminOptions options)
    {
        if (StartupSecurityValidator.IsPlaceholderSecret(options.Email))
        {
            throw new InvalidOperationException("BootstrapAdmin email is missing or still contains a placeholder value.");
        }

        if (!new EmailAddressAttribute().IsValid(options.Email))
        {
            throw new InvalidOperationException("BootstrapAdmin email is not a valid email address.");
        }

        if (StartupSecurityValidator.IsPlaceholderSecret(options.Password))
        {
            throw new InvalidOperationException("BootstrapAdmin password is missing or still contains a placeholder value.");
        }

        if (!IsStrongBootstrapPassword(options.Password))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin password must be at least 12 characters and include uppercase, lowercase, digit, and non-alphanumeric characters.");
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
}
