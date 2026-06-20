using DARAK.Api.Enums;
using DARAK.Api.Interfaces;

namespace DARAK.Tests;

internal sealed class FakeCompoundAccessService(
    Guid[]? allowedCompoundIds = null,
    Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>? roleAccess = null,
    bool isAuthenticated = true,
    bool isSuperAdmin = false)
    : ICompoundAccessService
{
    private readonly Guid[] allowedCompoundIds = allowedCompoundIds ?? [];
    private readonly Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool> roleAccess = roleAccess ?? [];

    public Task<CompoundAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompoundAccessScope(isAuthenticated, isSuperAdmin, allowedCompoundIds));
    }

    public Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(isSuperAdmin || allowedCompoundIds.Contains(compoundId));
    }

    public Task<bool> CanUserAccessCompoundAsync(
        Guid userId,
        Guid compoundId,
        UserRole requiredRole,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            isSuperAdmin
            || roleAccess.GetValueOrDefault((userId, compoundId, requiredRole)));
    }

    public Task<Guid[]> GetAllowedCompoundIdsForUserRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var compoundIds = roleAccess
            .Where(item => item.Key.UserId == userId && item.Key.Role == role && item.Value)
            .Select(item => item.Key.CompoundId)
            .Distinct()
            .ToArray();

        return Task.FromResult(isSuperAdmin ? allowedCompoundIds : compoundIds);
    }
}
