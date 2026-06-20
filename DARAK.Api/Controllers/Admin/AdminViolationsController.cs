using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Violations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ViolationFineReaders)]
[Route("api/admin/violations")]
public sealed class AdminViolationsController(
    ICurrentUserService currentUserService,
    IComplaintViolationService complaintViolationService)
    : ApiControllerBase
{
    [Authorize(Roles = RoleNames.ViolationAdministrators)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ViolationResponse>>> SearchViolations(
        [FromQuery] ViolationSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await complaintViolationService.SearchViolationsAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ViolationAdministrators)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ViolationResponse>> GetViolation(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.GetViolationAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ViolationAdministrators)]
    [HttpPost]
    public async Task<ActionResult<ViolationResponse>> CreateViolation(
        CreateViolationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await complaintViolationService.CreateViolationAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetViolation), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.ViolationFineReaders)]
    [HttpGet("fines")]
    public async Task<ActionResult<PagedResult<ViolationFineResponse>>> SearchFines(
        [FromQuery] ViolationFineSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await complaintViolationService.SearchViolationFinesAdminAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ViolationFineReaders)]
    [HttpGet("fines/{id:guid}")]
    public async Task<ActionResult<ViolationFineResponse>> GetFine(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.GetViolationFineAdminAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ViolationFineManagers)]
    [HttpPost("fines")]
    public async Task<ActionResult<ViolationFineResponse>> CreateFine(
        CreateViolationFineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await complaintViolationService.CreateViolationFineAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetFine), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.ViolationFineManagers)]
    [HttpPost("fines/{id:guid}/cancel")]
    public async Task<ActionResult<ViolationFineResponse>> CancelFine(
        Guid id,
        CancelViolationFineRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.CancelViolationFineAsync(
            id,
            request,
            cancellationToken));
    }
}
