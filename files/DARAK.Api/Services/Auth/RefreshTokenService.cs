using System.Collections.Concurrent;
using DARAK.Api.Authentication;
using DARAK.Api.Data;
using DARAK.Api.DTOs;
using DARAK.Api.Entities;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class RefreshTokenService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService)
    : IRefreshTokenService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshTokenLocks = new(StringComparer.Ordinal);

    public async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await tokenService.CreateAccessTokenAsync(user, cancellationToken);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken(refreshToken),
            ExpiresAtUtc = tokenService.GetRefreshTokenExpirationUtc(),
            CreatedByIp = ipAddress
        };

        dbContext.RefreshTokens.Add(refreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, accessToken, refreshToken, refreshTokenEntity.ExpiresAtUtc);
    }

    public async Task<AuthResponse> RotateRefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashToken(refreshToken);
        var tokenLock = RefreshTokenLocks.GetOrAdd(tokenHash, _ => new SemaphoreSlim(1, 1));
        await tokenLock.WaitAsync(cancellationToken);

        try
        {
            var storedToken = await dbContext.RefreshTokens
                .Include(token => token.User)
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

            if (storedToken is null || storedToken.User is null)
            {
                throw new UnauthorizedAccessException("Authentication failed.");
            }

            if (!storedToken.IsActive)
            {
                await RevokeReplacementChainAsync(
                    storedToken,
                    ipAddress,
                    cancellationToken);

                throw new UnauthorizedAccessException("Authentication failed.");
            }

            var currentUser = await userManager.FindByIdAsync(storedToken.UserId.ToString());
            if (currentUser is null
                || !await userManager.IsEmailConfirmedAsync(currentUser)
                || await userManager.IsLockedOutAsync(currentUser))
            {
                throw new UnauthorizedAccessException("Authentication failed.");
            }

            var newRefreshToken = tokenService.GenerateRefreshToken();
            var newRefreshTokenHash = tokenService.HashToken(newRefreshToken);
            var newRefreshTokenEntity = new RefreshToken
            {
                UserId = storedToken.UserId,
                TokenHash = newRefreshTokenHash,
                ExpiresAtUtc = tokenService.GetRefreshTokenExpirationUtc(),
                CreatedByIp = ipAddress
            };

            storedToken.RevokedAtUtc = DateTime.UtcNow;
            storedToken.RevokedByIp = ipAddress;
            storedToken.ReplacedByTokenHash = newRefreshTokenHash;
            storedToken.RevokedReason = "Rotated";

            dbContext.RefreshTokens.Add(newRefreshTokenEntity);
            await dbContext.SaveChangesAsync(cancellationToken);

            var accessToken = await tokenService.CreateAccessTokenAsync(currentUser, cancellationToken);
            return await BuildAuthResponseAsync(
                currentUser,
                accessToken,
                newRefreshToken,
                newRefreshTokenEntity.ExpiresAtUtc);
        }
        finally
        {
            tokenLock.Release();

            if (tokenLock.CurrentCount == 1)
            {
                RefreshTokenLocks.TryRemove(tokenHash, out _);
            }
        }
    }

    public async Task LogoutAsync(
        Guid userId,
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                token => token.UserId == userId && token.TokenHash == tokenHash,
                cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
        {
            return;
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.RevokedReason = "Logged out";

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(
        ApplicationUser user,
        GeneratedAccessToken accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc)
    {
        var roles = await userManager.GetRolesAsync(user);
        var userResponse = new UserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles.ToArray());

        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            userResponse);
    }

    private async Task RevokeReplacementChainAsync(
        RefreshToken reusedToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var nextHash = reusedToken.ReplacedByTokenHash;
        while (!string.IsNullOrWhiteSpace(nextHash))
        {
            var replacement = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(
                    token => token.UserId == reusedToken.UserId
                        && token.TokenHash == nextHash,
                    cancellationToken);
            if (replacement is null)
            {
                break;
            }

            if (replacement.IsActive)
            {
                replacement.RevokedAtUtc = DateTime.UtcNow;
                replacement.RevokedByIp = ipAddress;
                replacement.RevokedReason = "Revoked after refresh token reuse detection.";
            }

            nextHash = replacement.ReplacedByTokenHash;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
