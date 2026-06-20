using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserResponse>> SearchAsync(
        AdminUserSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminUserResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminUserResponse>> AddRoleAsync(
        Guid id,
        AssignUserRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> RemoveRoleAsync(
        Guid id,
        UserRole role,
        CancellationToken cancellationToken = default);
}
