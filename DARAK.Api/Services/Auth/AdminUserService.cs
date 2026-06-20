using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class AdminUserService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
    : IAdminUserService
{
    private static readonly UserRole[] ScopedCompoundRoles =
    [
        UserRole.CompoundAdmin,
        UserRole.Accountant,
        UserRole.Guard
    ];

    public async Task<PagedResult<AdminUserResponse>> SearchAsync(
        AdminUserSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var users = dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            users = users.Where(user =>
                user.FullName.Contains(term)
                || (user.Email != null && user.Email.Contains(term)));
        }

        if (query.Role.HasValue)
        {
            var roleName = query.Role.Value.ToString();
            users =
                from user in users
                join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where role.Name == roleName
                select user;
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var pageUsers = await users
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        var items = new List<AdminUserResponse>(pageUsers.Length);
        foreach (var user in pageUsers)
        {
            items.Add(await ToResponseAsync(user));
        }

        return new PagedResult<AdminUserResponse>(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<AdminUserResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return ServiceResult<AdminUserResponse>.NotFound("User was not found.");
        }

        return ServiceResult<AdminUserResponse>.Success(await ToResponseAsync(user));
    }

    public async Task<ServiceResult<AdminUserResponse>> AddRoleAsync(
        Guid id,
        AssignUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRoleAsync(request.Role);
        if (validation is not null)
        {
            return ServiceResult<AdminUserResponse>.BadRequest(validation);
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return ServiceResult<AdminUserResponse>.NotFound("User was not found.");
        }

        var roleName = request.Role.ToString();
        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return ServiceResult<AdminUserResponse>.Conflict("User already has this role.");
        }

        var result = await userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            return ServiceResult<AdminUserResponse>.BadRequest(
                "Role assignment failed.",
                ToErrorDictionary(result.Errors));
        }

        return ServiceResult<AdminUserResponse>.Success(await ToResponseAsync(user));
    }

    public async Task<ServiceResult<object?>> RemoveRoleAsync(
        Guid id,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRoleAsync(role);
        if (validation is not null)
        {
            return ServiceResult<object?>.BadRequest(validation);
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return ServiceResult<object?>.NotFound("User was not found.");
        }

        var roleName = role.ToString();
        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            return ServiceResult<object?>.BadRequest("User does not have this role.");
        }

        if (role == UserRole.SuperAdmin && await IsLastSuperAdminAsync(user))
        {
            return ServiceResult<object?>.Conflict("Cannot remove the last SuperAdmin role.");
        }

        if (ScopedCompoundRoles.Contains(role))
        {
            var hasActiveAssignments = await dbContext.UserCompoundAssignments.AnyAsync(
                assignment => assignment.UserId == user.Id
                    && assignment.Role == role
                    && assignment.IsActive,
                cancellationToken);
            if (hasActiveAssignments)
            {
                return ServiceResult<object?>.Conflict(
                    "Deactivate this user's active compound assignments for the role before removing it.");
            }
        }

        var result = await userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            return ServiceResult<object?>.BadRequest(
                "Role removal failed.",
                ToErrorDictionary(result.Errors));
        }

        return ServiceResult<object?>.Success(null);
    }

    private async Task<string?> ValidateRoleAsync(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return "Invalid role.";
        }

        if (!await roleManager.RoleExistsAsync(role.ToString()))
        {
            return "Role was not found in the identity store.";
        }

        return null;
    }

    private async Task<bool> IsLastSuperAdminAsync(ApplicationUser user)
    {
        var superAdmins = await userManager.GetUsersInRoleAsync(UserRole.SuperAdmin.ToString());
        return superAdmins.Count == 1 && superAdmins[0].Id == user.Id;
    }

    private async Task<AdminUserResponse> ToResponseAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        return new AdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.EmailConfirmed,
            isLockedOut,
            user.CreatedAtUtc,
            roles.OrderBy(role => role).ToArray());
    }

    private static IReadOnlyDictionary<string, string[]> ToErrorDictionary(IEnumerable<IdentityError> errors)
    {
        return errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "Identity" : error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray());
    }
}
