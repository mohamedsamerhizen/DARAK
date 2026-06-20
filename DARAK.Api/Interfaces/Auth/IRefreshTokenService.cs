using DARAK.Api.DTOs;
using DARAK.Api.Identity;

namespace DARAK.Api.Interfaces;

public interface IRefreshTokenService
{
    Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> RotateRefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        Guid userId,
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
