namespace DARAK.Api.DTOs.Common;

public enum ServiceResultStatus
{
    Success,
    NotFound,
    BadRequest,
    Conflict,
    Forbidden
}

public sealed class ServiceResult<T>
{
    private ServiceResult(
        ServiceResultStatus status,
        T? value,
        string? message,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        Status = status;
        Value = value;
        Message = message;
        Errors = errors;
    }

    public ServiceResultStatus Status { get; }

    public T? Value { get; }

    public string? Message { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public bool IsSuccess => Status == ServiceResultStatus.Success;

    public static ServiceResult<T> Success(T value)
    {
        return new ServiceResult<T>(ServiceResultStatus.Success, value, null, null);
    }

    public static ServiceResult<T> NotFound(string message)
    {
        return new ServiceResult<T>(ServiceResultStatus.NotFound, default, message, null);
    }

    public static ServiceResult<T> BadRequest(string message)
    {
        return new ServiceResult<T>(ServiceResultStatus.BadRequest, default, message, null);
    }

    public static ServiceResult<T> BadRequest(
        string message,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return new ServiceResult<T>(ServiceResultStatus.BadRequest, default, message, errors);
    }

    public static ServiceResult<T> Conflict(string message)
    {
        return new ServiceResult<T>(ServiceResultStatus.Conflict, default, message, null);
    }

    public static ServiceResult<T> Forbidden(string message)
    {
        return new ServiceResult<T>(ServiceResultStatus.Forbidden, default, message, null);
    }
}
