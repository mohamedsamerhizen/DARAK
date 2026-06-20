using DARAK.Api.Authentication;
using DARAK.Api.Identity;

namespace DARAK.Api.Interfaces;

public interface ITokenService
{
    Task<GeneratedAccessToken> CreateAccessTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);

    string GenerateRefreshToken();

    string HashToken(string token);

    DateTime GetRefreshTokenExpirationUtc();
}
