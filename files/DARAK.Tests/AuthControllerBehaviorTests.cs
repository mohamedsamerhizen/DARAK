using DARAK.Api.DTOs;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using DARAK.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class AuthControllerBehaviorTests
{
    [Fact]
    public async Task Register_AssignsResidentRoleAndRequiresConfirmationWithoutIssuingTokens()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var roleManager = IdentityTestHelpers.CreateRoleManager(dbContext);
        await IdentityTestHelpers.SeedRolesAsync(roleManager);
        var refreshTokenService = new FakeRefreshTokenService();
        var controller = CreateController(userManager, refreshTokenService);

        var result = await controller.Register(
            new RegisterRequest
            {
                FullName = "Resident User",
                Email = "resident-register@test.local",
                Password = "StrongPass1!"
            },
            CancellationToken.None);

        var accepted = result.Result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Value.Should().NotBeNull();
        var user = await userManager.FindByEmailAsync("resident-register@test.local");
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();
        (await userManager.IsInRoleAsync(user, nameof(UserRole.Resident))).Should().BeTrue();
        refreshTokenService.CreatedForUserId.Should().BeNull();
    }

    [Fact]
    public async Task Register_RollsBackUserWhenResidentRoleAssignmentFails()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var controller = CreateController(userManager, new FakeRefreshTokenService());

        var result = await controller.Register(
            new RegisterRequest
            {
                FullName = "Rollback Resident",
                Email = "rollback-register@test.local",
                Password = "StrongPass1!"
            },
            CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await userManager.FindByEmailAsync("rollback-register@test.local")).Should().BeNull();
        (await dbContext.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Register_ReturnsGenericConflictMessageForExistingEmail()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var roleManager = IdentityTestHelpers.CreateRoleManager(dbContext);
        await IdentityTestHelpers.SeedRolesAsync(roleManager);
        await IdentityTestHelpers.CreateUserAsync(userManager, "existing@test.local");
        var controller = CreateController(userManager, new FakeRefreshTokenService());

        var result = await controller.Register(
            new RegisterRequest
            {
                FullName = "Duplicate User",
                Email = "existing@test.local",
                Password = "StrongPass1!"
            },
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var error = conflict.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Message.Should().Be("Registration could not be completed.");
        error.Message.Should().NotContain("already registered");
        error.Message.Should().NotContain("existing@test.local");
    }

    [Fact]
    public async Task Login_ReturnsSameUnauthorizedMessageForUnknownUserAndWrongPassword()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        await IdentityTestHelpers.CreateUserAsync(userManager, "known@test.local");
        var controller = CreateController(userManager, new FakeRefreshTokenService());

        var unknown = await controller.Login(
            new LoginRequest { Email = "unknown@test.local", Password = "Whatever1!" },
            CancellationToken.None);
        var wrongPassword = await controller.Login(
            new LoginRequest { Email = "known@test.local", Password = "WrongPassword1!" },
            CancellationToken.None);

        var unknownError = unknown.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject
            .Value.Should().BeOfType<ApiErrorResponse>().Subject;
        var wrongPasswordError = wrongPassword.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject
            .Value.Should().BeOfType<ApiErrorResponse>().Subject;
        unknownError.Message.Should().Be("Invalid email or password.");
        wrongPasswordError.Message.Should().Be(unknownError.Message);
    }


    [Fact]
    public async Task Register_EnablesLockoutForNewUsers()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var roleManager = IdentityTestHelpers.CreateRoleManager(dbContext);
        await IdentityTestHelpers.SeedRolesAsync(roleManager);
        var controller = CreateController(userManager, new FakeRefreshTokenService());

        await controller.Register(
            new RegisterRequest
            {
                FullName = "Lockout Resident",
                Email = "lockout-register@test.local",
                Password = "StrongPass1!"
            },
            CancellationToken.None);

        var user = await userManager.FindByEmailAsync("lockout-register@test.local");
        user.Should().NotBeNull();
        (await userManager.GetLockoutEnabledAsync(user!)).Should().BeTrue();
    }

    [Fact]
    public async Task Login_InvalidPassword_IncrementsAccessFailedCountAndLocksUser()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = new ApplicationUser
        {
            Email = "lockout-login@test.local",
            UserName = "lockout-login@test.local",
            FullName = "Lockout Login",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        var createResult = await userManager.CreateAsync(user, "StrongPass1!");
        createResult.Succeeded.Should().BeTrue();
        var controller = CreateController(userManager, new FakeRefreshTokenService());

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await controller.Login(
                new LoginRequest { Email = "lockout-login@test.local", Password = "WrongPass1!" },
                CancellationToken.None);

            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        (await userManager.GetAccessFailedCountAsync(user)).Should().Be(0);
        (await userManager.IsLockedOutAsync(user)).Should().BeTrue();
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_ReturnsGenericUnauthorizedWithoutIssuingTokens()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = new ApplicationUser
        {
            Email = "unconfirmed-login@test.local",
            UserName = "unconfirmed-login@test.local",
            FullName = "Unconfirmed Login",
            EmailConfirmed = false,
            LockoutEnabled = true
        };
        var createResult = await userManager.CreateAsync(user, "StrongPass1!");
        createResult.Succeeded.Should().BeTrue();
        var refreshTokenService = new FakeRefreshTokenService();
        var controller = CreateController(userManager, refreshTokenService);

        var result = await controller.Login(
            new LoginRequest { Email = "unconfirmed-login@test.local", Password = "StrongPass1!" },
            CancellationToken.None);

        var error = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject
            .Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Message.Should().Be("Invalid email or password.");
        refreshTokenService.CreatedForUserId.Should().BeNull();
    }

    [Fact]
    public async Task Login_SuccessfulPassword_ResetsAccessFailedCount()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = new ApplicationUser
        {
            Email = "reset-login@test.local",
            UserName = "reset-login@test.local",
            FullName = "Reset Login",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        var createResult = await userManager.CreateAsync(user, "StrongPass1!");
        createResult.Succeeded.Should().BeTrue();
        await userManager.AccessFailedAsync(user);
        await userManager.AccessFailedAsync(user);
        (await userManager.GetAccessFailedCountAsync(user)).Should().Be(2);
        var refreshTokenService = new FakeRefreshTokenService();
        var controller = CreateController(userManager, refreshTokenService);

        var result = await controller.Login(
            new LoginRequest { Email = "reset-login@test.local", Password = "StrongPass1!" },
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        (await userManager.GetAccessFailedCountAsync(user)).Should().Be(0);
        refreshTokenService.CreatedForUserId.Should().Be(user.Id);
    }

    private static AuthController CreateController(
        Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokenService)
    {
        return new AuthController(userManager, refreshTokenService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public Guid? CreatedForUserId { get; private set; }

        public Task<AuthResponse> CreateAuthResponseAsync(
            ApplicationUser user,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            CreatedForUserId = user.Id;
            return Task.FromResult(new AuthResponse(
                "access-token",
                DateTime.UtcNow.AddMinutes(15),
                "refresh-token",
                DateTime.UtcNow.AddDays(7),
                new UserResponse(user.Id, user.Email ?? string.Empty, user.FullName, [nameof(UserRole.Resident)])));
        }

        public Task<AuthResponse> RotateRefreshTokenAsync(
            string refreshToken,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task LogoutAsync(
            Guid userId,
            string refreshToken,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
