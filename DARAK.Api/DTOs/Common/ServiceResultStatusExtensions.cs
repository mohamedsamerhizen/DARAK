using Microsoft.AspNetCore.Http;

namespace DARAK.Api.DTOs.Common;

public static class ServiceResultStatusExtensions
{
    public static int ToErrorHttpStatusCode(this ServiceResultStatus status)
    {
        return status switch
        {
            ServiceResultStatus.NotFound => StatusCodes.Status404NotFound,
            ServiceResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ServiceResultStatus.Conflict => StatusCodes.Status409Conflict,
            ServiceResultStatus.BadRequest => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
