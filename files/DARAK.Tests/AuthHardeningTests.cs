using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DARAK.Api.Authentication;
using DARAK.Api.Controllers;
using DARAK.Api.DTOs;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using DARAK.Api.Middleware;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DARAK.Tests;

public sealed class AuthHardeningTests
{
    [Theory]
    [InlineData(nameof(AuthController.Register), RateLimitPolicyNames.AuthRegister)]
    [InlineData(nameof(AuthController.Login), RateLimitPolicyNames.AuthLogin)]
    [InlineData(nameof(AuthController.Refresh), RateLimitPolicyNames.AuthRefresh)]
    public void AuthEndpoints_HaveExpectedRateLimitPolicies(string methodName, string expectedPolicy)
    {
        var method = typeof(AuthController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == methodName);

        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PolicyName.Should().Be(expectedPolicy);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_HidesUnauthorizedExceptionDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new UnauthorizedAccessException("secret token state"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("Authentication failed.");
        body.Should().NotContain("secret token state");
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_UsesSameMessageForUnknownAndInactiveTokens()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "refresh@test.local");
        var tokenService = new FakeTokenService();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken("inactive-token"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            RevokedReason = "Rotated"
        });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        var unknown = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("unknown-token", "127.0.0.1"));
        var inactive = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("inactive-token", "127.0.0.1"));

        unknown.Message.Should().Be("Authentication failed.");
        inactive.Message.Should().Be("Authentication failed.");
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_RevokesReplacementChainOnReuse()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "reuse@test.local");
        var tokenService = new FakeTokenService();
        var replacementHash = tokenService.HashToken("replacement-token");
        dbContext.RefreshTokens.AddRange(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = tokenService.HashToken("reused-token"),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                RevokedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ReplacedByTokenHash = replacementHash,
                RevokedReason = "Rotated"
            },
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = replacementHash,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("reused-token", "127.0.0.1"));

        var replacement = dbContext.RefreshTokens.Single(token => token.TokenHash == replacementHash);
        replacement.IsActive.Should().BeFalse();
        replacement.RevokedReason.Should().Be("Revoked after refresh token reuse detection.");
    }


    [Fact]
    public async Task RotateRefreshTokenAsync_SecondUseOfRotatedTokenDoesNotIssueAnotherReplacement()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "double-rotate@test.local");
        var tokenService = new FakeTokenService();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken("old-token"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        await service.RotateRefreshTokenAsync("old-token", "first-ip");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("old-token", "second-ip"));

        var tokens = dbContext.RefreshTokens.OrderBy(token => token.CreatedAtUtc).ToArray();
        tokens.Should().HaveCount(2);
        tokens[0].RevokedReason.Should().Be("Rotated");
        tokens[1].RevokedReason.Should().Be("Revoked after refresh token reuse detection.");
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_UnconfirmedUser_DoesNotIssueReplacement()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = new ApplicationUser
        {
            Email = "unconfirmed-refresh@test.local",
            UserName = "unconfirmed-refresh@test.local",
            FullName = "Unconfirmed Refresh",
            EmailConfirmed = false,
            LockoutEnabled = true
        };
        var createResult = await userManager.CreateAsync(user, "StrongPass1!");
        createResult.Succeeded.Should().BeTrue();
        var tokenService = new FakeTokenService();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken("unconfirmed-token"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("unconfirmed-token", "127.0.0.1"));

        error.Message.Should().Be("Authentication failed.");
        var tokens = dbContext.RefreshTokens.ToArray();
        tokens.Should().ContainSingle();
        tokens[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_LockedUser_DoesNotIssueReplacement()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "locked-refresh@test.local");
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));
        var tokenService = new FakeTokenService();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken("locked-token"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("locked-token", "127.0.0.1"));

        error.Message.Should().Be("Authentication failed.");
        dbContext.RefreshTokens.Should().ContainSingle();
    }

    private sealed class FakeTokenService : ITokenService
    {
        public Task<GeneratedAccessToken> CreateAccessTokenAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GeneratedAccessToken("access-token", DateTime.UtcNow.AddMinutes(15)));
        }

        public string GenerateRefreshToken()
        {
            return "new-refresh-token";
        }

        public string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        public DateTime GetRefreshTokenExpirationUtc()
        {
            return DateTime.UtcNow.AddDays(7);
        }
    }
}
