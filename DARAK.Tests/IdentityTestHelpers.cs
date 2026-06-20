using DARAK.Api.Data;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DARAK.Tests;

internal static class IdentityTestHelpers
{
    public static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext dbContext)
    {
        var store = new UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(dbContext);

        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    public static RoleManager<IdentityRole<Guid>> CreateRoleManager(ApplicationDbContext dbContext)
    {
        var store = new RoleStore<IdentityRole<Guid>, ApplicationDbContext, Guid>(dbContext);

        return new RoleManager<IdentityRole<Guid>>(
            store,
            [new RoleValidator<IdentityRole<Guid>>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole<Guid>>>.Instance);
    }

    public static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to seed role {role}.");
                }
            }
        }
    }

    public static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName = "Test User")
    {
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            FullName = fullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create test user {email}.");
        }

        return user;
    }
}
