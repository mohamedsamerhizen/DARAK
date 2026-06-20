namespace DARAK.Api.Helpers;

public static class RequestContextHelper
{
    public static string? GetClientIpAddress(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
