using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface ICompoundAccessService
{
    Task<CompoundAccessScope> GetCurrentScopeAsync(
        CancellationToken cancellationToken = default);

    Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default);

    Task<bool> CanUserAccessCompoundAsync(
        Guid userId,
        Guid compoundId,
        UserRole requiredRole,
        CancellationToken cancellationToken = default);

    Task<Guid[]> GetAllowedCompoundIdsForUserRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default);
}

public sealed record CompoundAccessScope(
    bool IsAuthenticated,
    bool IsSuperAdmin,
    Guid[] AllowedCompoundIds)
{
    public bool CanAccess(Guid compoundId)
    {
        return IsSuperAdmin || AllowedCompoundIds.Contains(compoundId);
    }
}
