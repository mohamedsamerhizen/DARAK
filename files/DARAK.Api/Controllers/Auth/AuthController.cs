using DARAK.Api.DTOs;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IRefreshTokenService refreshTokenService)
    : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.AuthRegister)]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return Conflict(ApiErrorResponseFactory.Create(HttpContext, "Registration could not be completed."));
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            EmailConfirmed = false,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(ApiErrorResponseFactory.Create(
                HttpContext,
                "Registration failed.",
                ToErrorDictionary(createResult.Errors)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, nameof(UserRole.Resident));
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return BadRequest(ApiErrorResponseFactory.Create(
                HttpContext,
                "Registration could not be completed.",
                ToErrorDictionary(roleResult.Errors)));
        }

        return Accepted(new
        {
            message = "Registration accepted. Email confirmation is required before sign-in."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Invalid email or password."));
        }

        if (userManager.SupportsUserLockout && !await userManager.GetLockoutEnabledAsync(user))
        {
            await userManager.SetLockoutEnabledAsync(user, enabled: true);
        }

        if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Invalid email or password."));
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            if (userManager.SupportsUserLockout)
            {
                await userManager.AccessFailedAsync(user);
            }

            return Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Invalid email or password."));
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Invalid email or password."));
        }

        if (userManager.SupportsUserLockout)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }

        var response = await refreshTokenService.CreateAuthResponseAsync(
            user,
            RequestContextHelper.GetClientIpAddress(HttpContext),
            cancellationToken);

        return Ok(response);
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await refreshTokenService.RotateRefreshTokenAsync(
            request.RefreshToken,
            RequestContextHelper.GetClientIpAddress(HttpContext),
            cancellationToken);

        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue = userManager.GetUserId(User);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Current user is invalid."));
        }

        await refreshTokenService.LogoutAsync(
            userId,
            request.RefreshToken,
            RequestContextHelper.GetClientIpAddress(HttpContext),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Current user was not found."));
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new UserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles.ToArray()));
    }

    private static IReadOnlyDictionary<string, string[]> ToErrorDictionary(IEnumerable<IdentityError> errors)
    {
        return errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "Identity" : error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray());
    }
}
