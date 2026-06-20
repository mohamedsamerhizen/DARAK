using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface IStaffMemberService
{
    Task<PagedResult<StaffMemberResponse>> SearchStaffMembersAsync(
        StaffMemberQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StaffMemberResponse>> GetStaffMemberAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StaffMemberResponse>> CreateStaffMemberAsync(
        CreateStaffMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StaffMemberResponse>> UpdateStaffMemberAsync(
        Guid id,
        UpdateStaffMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StaffMemberResponse>> SetStaffMemberStatusAsync(
        Guid id,
        StaffStatus status,
        CancellationToken cancellationToken = default);
}
