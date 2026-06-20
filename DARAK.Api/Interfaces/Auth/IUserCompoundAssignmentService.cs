using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;

namespace DARAK.Api.Interfaces;

public interface IUserCompoundAssignmentService
{
    Task<PagedResult<UserCompoundAssignmentResponse>> SearchAsync(
        UserCompoundAssignmentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UserCompoundAssignmentResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UserCompoundAssignmentResponse>> CreateAsync(
        Guid? createdByUserId,
        CreateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UserCompoundAssignmentResponse>> UpdateAsync(
        Guid id,
        UpdateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
