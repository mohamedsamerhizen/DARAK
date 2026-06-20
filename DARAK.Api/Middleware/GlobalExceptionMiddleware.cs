using DARAK.Api.Helpers;

namespace DARAK.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(httpContext, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception occurred.");
        }
        else
        {
            logger.LogWarning(exception, "Request failed with a handled exception.");
        }

        var message = statusCode switch
        {
            StatusCodes.Status500InternalServerError => "An unexpected error occurred.",
            StatusCodes.Status401Unauthorized => "Authentication failed.",
            _ => exception.Message
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = ApiErrorResponseFactory.Create(httpContext, message);
        await httpContext.Response.WriteAsJsonAsync(response);
    }
}
