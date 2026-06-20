namespace DARAK.Api.DTOs;

public sealed record ApiErrorResponse(
    string TraceId,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors = null);
