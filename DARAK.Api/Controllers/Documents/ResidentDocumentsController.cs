using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/documents")]
public sealed class ResidentDocumentsController(
    IDocumentService documentService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentFileResponse>>> Search(
        [FromQuery] DocumentQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentService.SearchDocumentsAsync(
            query,
            currentUserService.UserId,
            isManager: false,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentFileResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentService.GetDocumentAsync(
            id,
            currentUserService.UserId,
            isManager: false,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await documentService.DownloadDocumentAsync(
            id,
            currentUserService.UserId,
            isManager: false,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorObjectResult(result);
        }

        return PhysicalFile(
            result.Value!.PhysicalPath,
            result.Value.ContentType,
            result.Value.OriginalFileName);
    }

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return HttpContext.Request.Headers.UserAgent.ToString();
    }
}
