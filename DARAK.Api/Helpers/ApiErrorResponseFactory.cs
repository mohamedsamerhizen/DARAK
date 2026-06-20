using System.Diagnostics;
using DARAK.Api.DTOs;

namespace DARAK.Api.Helpers;

public static class ApiErrorResponseFactory
{
    public static ApiErrorResponse Create(
        HttpContext httpContext,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        return new ApiErrorResponse(traceId, message, errors);
    }
}
