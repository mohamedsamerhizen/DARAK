using DARAK.Api.DTOs.Common;
using DARAK.Api.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ToErrorObjectResult(result);
    }

    protected IActionResult ToNoContentResult(ServiceResult<object?> result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return ToErrorObjectResult(result);
    }

    protected ObjectResult ToErrorObjectResult<T>(ServiceResult<T> result)
    {
        var response = ApiErrorResponseFactory.Create(
            HttpContext,
            result.Message ?? "Request failed.",
            result.Errors);

        return StatusCode(result.Status.ToErrorHttpStatusCode(), response);
    }
}

