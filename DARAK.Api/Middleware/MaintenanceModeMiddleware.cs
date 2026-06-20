using DARAK.Api.Data;
using DARAK.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Middleware;

public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    private const string MaintenanceEnabledKey = "system.maintenance.enabled";
    private const string MaintenanceMessageKey = "system.maintenance.message";
    private const string DefaultMessage = "The system is currently under maintenance.";

    public async Task InvokeAsync(HttpContext httpContext, ApplicationDbContext dbContext)
    {
        if (IsWhitelisted(httpContext) || IsSuperAdmin(httpContext))
        {
            await next(httpContext);
            return;
        }

        var enabledValue = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.CompoundId == null && setting.Key == MaintenanceEnabledKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (!bool.TryParse(enabledValue, out var isEnabled) || !isEnabled)
        {
            await next(httpContext);
            return;
        }

        var message = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.CompoundId == null && setting.Key == MaintenanceMessageKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(ApiErrorResponseFactory.Create(
            httpContext,
            string.IsNullOrWhiteSpace(message) ? DefaultMessage : message));
    }

    private static bool IsWhitelisted(HttpContext httpContext)
    {
        if (HttpMethods.IsOptions(httpContext.Request.Method))
        {
            return true;
        }

        var path = httpContext.Request.Path;
        return path.StartsWithSegments(new PathString("/health"), StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(new PathString("/swagger"), StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(new PathString("/api/auth/login"), StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(new PathString("/api/auth/refresh"), StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(new PathString("/api/admin/system/version"), StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(new PathString("/api/admin/system/maintenance-mode"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuperAdmin(HttpContext httpContext)
    {
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(RoleNames.SuperAdmin);
    }
}
