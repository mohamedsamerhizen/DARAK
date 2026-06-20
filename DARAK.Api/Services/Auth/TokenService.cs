using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DARAK.Api.Authentication;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DARAK.Api.Services;

public sealed class TokenService(
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> jwtOptionsAccessor)
    : ITokenService
{
    private readonly JwtOptions jwtOptions = jwtOptionsAccessor.Value;

    public async Task<GeneratedAccessToken> CreateAccessTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var roles = await userManager.GetRolesAsync(user);
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(jwtOptions.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new GeneratedAccessToken(tokenValue, expiresAtUtc);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(randomBytes);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public DateTime GetRefreshTokenExpirationUtc()
    {
        return DateTime.UtcNow.AddDays(jwtOptions.RefreshTokenDays);
    }
}
