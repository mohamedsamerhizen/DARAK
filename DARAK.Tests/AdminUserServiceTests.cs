using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class AdminUserServiceTests
{
    [Fact]
    public async Task AddRoleAsync_AddsRequestedRoleToExistingUser()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(identity.UserManager, "compound-admin@test.local");
        var service = new AdminUserService(dbContext, identity.UserManager, identity.RoleManager);

        var result = await service.AddRoleAsync(
            user.Id,
            new AssignUserRoleRequest { Role = UserRole.CompoundAdmin });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Roles.Should().Contain(nameof(UserRole.CompoundAdmin));
        (await identity.UserManager.IsInRoleAsync(user, nameof(UserRole.CompoundAdmin))).Should().BeTrue();
    }

    [Fact]
    public async Task AddRoleAsync_RejectsDuplicateRoleAssignment()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(identity.UserManager, "guard@test.local");
        await identity.UserManager.AddToRoleAsync(user, nameof(UserRole.Guard));
        var service = new AdminUserService(dbContext, identity.UserManager, identity.RoleManager);

        var result = await service.AddRoleAsync(
            user.Id,
            new AssignUserRoleRequest { Role = UserRole.Guard });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task RemoveRoleAsync_BlocksRemovingLastSuperAdmin()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var superAdmin = await IdentityTestHelpers.CreateUserAsync(identity.UserManager, "super@test.local");
        await identity.UserManager.AddToRoleAsync(superAdmin, nameof(UserRole.SuperAdmin));
        var service = new AdminUserService(dbContext, identity.UserManager, identity.RoleManager);

        var result = await service.RemoveRoleAsync(superAdmin.Id, UserRole.SuperAdmin);

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        (await identity.UserManager.IsInRoleAsync(superAdmin, nameof(UserRole.SuperAdmin))).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveRoleAsync_BlocksScopedRoleWhenActiveCompoundAssignmentsExist()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var guard = await IdentityTestHelpers.CreateUserAsync(identity.UserManager, "guard-assigned@test.local");
        await identity.UserManager.AddToRoleAsync(guard, nameof(UserRole.Guard));

        var compound = new Compound
        {
            Name = "Assigned Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Test"
        };
        dbContext.Compounds.Add(compound);
        dbContext.UserCompoundAssignments.Add(new UserCompoundAssignment
        {
            UserId = guard.Id,
            CompoundId = compound.Id,
            Role = UserRole.Guard,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var service = new AdminUserService(dbContext, identity.UserManager, identity.RoleManager);

        var result = await service.RemoveRoleAsync(guard.Id, UserRole.Guard);

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        (await identity.UserManager.IsInRoleAsync(guard, nameof(UserRole.Guard))).Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_FiltersUsersByRole()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var accountant = await IdentityTestHelpers.CreateUserAsync(identity.UserManager, "accountant@test.local", "Accountant User");
        var resident = await IdentityTestHelpers.CreateUserAsync(identity.UserManager, "resident@test.local", "Resident User");
        await identity.UserManager.AddToRoleAsync(accountant, nameof(UserRole.Accountant));
        await identity.UserManager.AddToRoleAsync(resident, nameof(UserRole.Resident));
        var service = new AdminUserService(dbContext, identity.UserManager, identity.RoleManager);

        var result = await service.SearchAsync(new AdminUserSearchQuery { Role = UserRole.Accountant });

        result.TotalCount.Should().Be(1);
        result.Items.Single().Email.Should().Be("accountant@test.local");
    }

    private static async Task<IdentityFixture> CreateIdentityAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var roleManager = IdentityTestHelpers.CreateRoleManager(dbContext);
        await IdentityTestHelpers.SeedRolesAsync(roleManager);

        return new IdentityFixture(userManager, roleManager);
    }

    private sealed record IdentityFixture(
        Microsoft.AspNetCore.Identity.UserManager<DARAK.Api.Identity.ApplicationUser> UserManager,
        Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<Guid>> RoleManager);
}
