using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.DocumentManagers)]
[Route("api/admin/document-management")]
public sealed class AdminDocumentManagementController(
    IDocumentManagementService documentManagementService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DocumentManagementDashboardResponse>> GetDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.GetDashboardAsync(compoundId, cancellationToken));
    }

    [HttpGet("compliance")]
    public async Task<ActionResult<DocumentComplianceReportResponse>> GetComplianceReport(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.GetComplianceReportAsync(compoundId, cancellationToken));
    }

    [HttpGet("requirements")]
    public async Task<ActionResult<PagedResult<DocumentRequirementResponse>>> SearchRequirements(
        [FromQuery] DocumentRequirementSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.SearchRequirementsAsync(query, cancellationToken));
    }

    [HttpGet("requirements/{id:guid}")]
    public async Task<ActionResult<DocumentRequirementResponse>> GetRequirement(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.GetRequirementAsync(id, cancellationToken));
    }

    [HttpPost("requirements")]
    public async Task<ActionResult<DocumentRequirementResponse>> CreateRequirement(
        CreateDocumentRequirementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await documentManagementService.CreateRequirementAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetRequirement), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("requirements/{id:guid}")]
    public async Task<ActionResult<DocumentRequirementResponse>> UpdateRequirement(
        Guid id,
        UpdateDocumentRequirementRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.UpdateRequirementAsync(id, request, cancellationToken));
    }

    [HttpPost("requirements/{id:guid}/deactivate")]
    public async Task<ActionResult<DocumentRequirementResponse>> DeactivateRequirement(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.DeactivateRequirementAsync(id, cancellationToken));
    }

    [HttpPost("documents/{id:guid}/approve")]
    public async Task<ActionResult<DocumentFileResponse>> ApproveDocument(
        Guid id,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.ApproveDocumentAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPost("documents/{id:guid}/reject")]
    public async Task<ActionResult<DocumentFileResponse>> RejectDocument(
        Guid id,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.RejectDocumentAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpGet("residents/{residentProfileId:guid}/checklist")]
    public async Task<ActionResult<ResidentDocumentChecklistResponse>> GetResidentChecklist(
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await documentManagementService.GetResidentChecklistAsync(
            residentProfileId,
            cancellationToken));
    }
}
