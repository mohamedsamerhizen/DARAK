using DARAK.Api.Enums;
using DARAK.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace DARAK.Api.Data;

public static class IdentitySeeder
{
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

        var environment = services.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var email = configuration["DevelopmentSuperAdmin:Email"];
        var password = configuration["DevelopmentSuperAdmin:Password"];
        var fullName = configuration["DevelopmentSuperAdmin:FullName"] ?? "Development SuperAdmin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Development SuperAdmin credentials are not configured.");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var superAdmin = await userManager.FindByEmailAsync(email);

        if (superAdmin is null)
        {
            superAdmin = new ApplicationUser
            {
                Email = email,
                UserName = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(superAdmin, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to seed Development SuperAdmin: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(superAdmin, nameof(UserRole.SuperAdmin)))
        {
            var roleResult = await userManager.AddToRoleAsync(superAdmin, nameof(UserRole.SuperAdmin));
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to assign Development SuperAdmin role: {errors}");
            }
        }
    }
}
