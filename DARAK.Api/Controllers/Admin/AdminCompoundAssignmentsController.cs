using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/admin/compound-assignments")]
public sealed class AdminCompoundAssignmentsController(
    ICurrentUserService currentUserService,
    IUserCompoundAssignmentService assignmentService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserCompoundAssignmentResponse>>> Search(
        [FromQuery] UserCompoundAssignmentSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await assignmentService.SearchAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserCompoundAssignmentResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await assignmentService.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserCompoundAssignmentResponse>> Create(
        CreateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await assignmentService.CreateAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<UserCompoundAssignmentResponse>> Update(
        Guid id,
        UpdateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await assignmentService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await assignmentService.DeactivateAsync(id, cancellationToken));
    }
}
