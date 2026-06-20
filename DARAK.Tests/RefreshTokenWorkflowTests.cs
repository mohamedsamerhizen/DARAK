using System.Security.Cryptography;
using System.Text;
using DARAK.Api.Authentication;
using DARAK.Api.DTOs;
using DARAK.Api.Entities;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class RefreshTokenWorkflowTests
{
    [Fact]
    public async Task CreateAuthResponseAsync_StoresHashedTokenOnly()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "create-refresh@test.local");
        var tokenService = new DeterministicTokenService(["raw-refresh-token"]);
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        var response = await service.CreateAuthResponseAsync(user, "127.0.0.1");

        response.RefreshToken.Should().Be("raw-refresh-token");
        var stored = await dbContext.RefreshTokens.SingleAsync();
        stored.UserId.Should().Be(user.Id);
        stored.TokenHash.Should().Be(tokenService.HashToken("raw-refresh-token"));
        stored.TokenHash.Should().NotBe("raw-refresh-token");
        stored.CreatedByIp.Should().Be("127.0.0.1");
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_RevokesOldTokenAndCreatesActiveReplacement()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "rotate-refresh@test.local");
        var tokenService = new DeterministicTokenService(["new-refresh-token"]);
        var oldHash = tokenService.HashToken("old-refresh-token");
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = oldHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedByIp = "old-ip"
        });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        var response = await service.RotateRefreshTokenAsync("old-refresh-token", "new-ip");

        response.RefreshToken.Should().Be("new-refresh-token");
        var tokens = await dbContext.RefreshTokens.OrderBy(token => token.CreatedAtUtc).ToArrayAsync();
        tokens.Should().HaveCount(2);
        tokens[0].IsActive.Should().BeFalse();
        tokens[0].RevokedByIp.Should().Be("new-ip");
        tokens[0].RevokedReason.Should().Be("Rotated");
        tokens[0].ReplacedByTokenHash.Should().Be(tokenService.HashToken("new-refresh-token"));
        tokens[1].IsActive.Should().BeTrue();
        tokens[1].TokenHash.Should().Be(tokenService.HashToken("new-refresh-token"));
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_DoesNotIssueReplacementWhenUserIsNoLongerConfirmed()
    {
        await using var dbContext = TestDb.Create();
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, "unconfirmed-refresh@test.local");
        user.EmailConfirmed = false;
        (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
        var tokenService = new DeterministicTokenService(["should-not-be-issued"]);
        var oldHash = tokenService.HashToken("old-refresh-token");
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = oldHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedByIp = "old-ip"
        });
        await dbContext.SaveChangesAsync();
        var service = new RefreshTokenService(dbContext, userManager, tokenService);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RotateRefreshTokenAsync("old-refresh-token", "new-ip"));

        exception.Message.Should().Be("Authentication failed.");
        var tokens = await dbContext.RefreshTokens.ToArrayAsync();
        tokens.Should().ContainSingle();
        tokens[0].IsActive.Should().BeFalse();
        tokens[0].RevokedByIp.Should().Be("new-ip");
        tokens[0].RevokedReason.Should().Be("Account state changed");
    }

    private sealed class DeterministicTokenService : ITokenService
    {
        private readonly Queue<string> refreshTokens;

        public DeterministicTokenService(IEnumerable<string> refreshTokens)
        {
            this.refreshTokens = new Queue<string>(refreshTokens);
        }

        public Task<GeneratedAccessToken> CreateAccessTokenAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GeneratedAccessToken("access-token", DateTime.UtcNow.AddMinutes(15)));
        }

        public string GenerateRefreshToken()
        {
            return refreshTokens.Dequeue();
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
