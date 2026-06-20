using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/admin/users")]
public sealed class AdminUsersController(IAdminUserService userService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserResponse>>> Search(
        [FromQuery] AdminUserSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await userService.SearchAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await userService.GetAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<ActionResult<AdminUserResponse>> AddRole(
        Guid id,
        AssignUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await userService.AddRoleAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(
        Guid id,
        UserRole role,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await userService.RemoveRoleAsync(id, role, cancellationToken));
    }
}
