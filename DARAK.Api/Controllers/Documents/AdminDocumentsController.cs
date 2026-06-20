using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.DocumentManagers)]
[Route("api/admin/documents")]
public sealed class AdminDocumentsController(
    IDocumentService documentService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(DocumentService.MaxFileSizeInBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DocumentService.MaxFileSizeInBytes)]
    public async Task<ActionResult<DocumentFileResponse>> Upload(
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await documentService.UploadDocumentAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}/metadata")]
    public async Task<ActionResult<DocumentFileResponse>> UpdateMetadata(
        Guid id,
        UpdateDocumentMetadataRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentService.UpdateMetadataAsync(id, request, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentFileResponse>>> Search(
        [FromQuery] DocumentQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentService.SearchDocumentsAsync(
            query,
            currentUserService.UserId,
            isManager: true,
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
            isManager: true,
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
            isManager: true,
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await documentService.SoftDeleteDocumentAsync(
            id,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("{id:guid}/access-logs")]
    public async Task<ActionResult<PagedResult<DocumentAccessLogResponse>>> AccessLogs(
        Guid id,
        [FromQuery] DocumentAccessLogQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentService.GetAccessLogsAsync(id, query, cancellationToken));
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
