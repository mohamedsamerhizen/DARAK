using DARAK.Api.Data;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CompoundAccessService(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    UserManager<ApplicationUser> userManager)
    : ICompoundAccessService
{
    private static readonly UserRole[] AssignmentScopedRoles =
    [
        UserRole.CompoundAdmin,
        UserRole.Accountant,
        UserRole.Guard
    ];

    public async Task<CompoundAccessScope> GetCurrentScopeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return new CompoundAccessScope(false, false, []);
        }

        return await GetScopeForUserAsync(currentUserService.UserId.Value, cancellationToken);
    }

    public async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId == Guid.Empty)
        {
            return false;
        }

        var scope = await GetCurrentScopeAsync(cancellationToken);
        return scope.CanAccess(compoundId);
    }

    public async Task<bool> CanUserAccessCompoundAsync(
        Guid userId,
        Guid compoundId,
        UserRole requiredRole,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || compoundId == Guid.Empty)
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (await userManager.IsInRoleAsync(user, nameof(UserRole.SuperAdmin)))
        {
            return true;
        }

        if (!await userManager.IsInRoleAsync(user, requiredRole.ToString()))
        {
            return false;
        }

        return await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .AnyAsync(assignment =>
                assignment.UserId == userId
                && assignment.CompoundId == compoundId
                && assignment.Role == requiredRole
                && assignment.IsActive,
                cancellationToken);
    }

    public async Task<Guid[]> GetAllowedCompoundIdsForUserRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        if (await userManager.IsInRoleAsync(user, nameof(UserRole.SuperAdmin)))
        {
            return await dbContext.Compounds
                .AsNoTracking()
                .Where(compound => compound.IsActive)
                .Select(compound => compound.Id)
                .ToArrayAsync(cancellationToken);
        }

        if (!await userManager.IsInRoleAsync(user, role.ToString()))
        {
            return [];
        }

        return await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.UserId == userId
                && assignment.Role == role
                && assignment.IsActive)
            .Select(assignment => assignment.CompoundId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task<CompoundAccessScope> GetScopeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new CompoundAccessScope(false, false, []);
        }

        if (await userManager.IsInRoleAsync(user, nameof(UserRole.SuperAdmin)))
        {
            return new CompoundAccessScope(true, true, []);
        }

        var roles = await userManager.GetRolesAsync(user);
        var scopedRoles = roles
            .Select(role => Enum.TryParse<UserRole>(role, out var parsedRole) ? parsedRole : (UserRole?)null)
            .Where(role => role.HasValue && AssignmentScopedRoles.Contains(role.Value))
            .Select(role => role!.Value)
            .ToArray();

        if (scopedRoles.Length == 0)
        {
            return new CompoundAccessScope(true, false, []);
        }

        var allowedCompoundIds = await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.UserId == userId
                && assignment.IsActive
                && scopedRoles.Contains(assignment.Role))
            .Select(assignment => assignment.CompoundId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return new CompoundAccessScope(true, false, allowedCompoundIds);
    }
}
