using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Complaints;
using DARAK.Api.DTOs.Violations;

namespace DARAK.Api.Interfaces;

public interface IComplaintViolationService
{
    Task<PagedResult<ComplaintResponse>> SearchComplaintsAdminAsync(
        ComplaintSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplaintResponse>> GetComplaintAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplaintResponse>> MarkComplaintUnderReviewAsync(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplaintResponse>> ResolveComplaintAsync(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplaintResponse>> RejectComplaintAsync(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationResponse>> ConvertComplaintToViolationAsync(
        Guid id,
        Guid? createdByUserId,
        ConvertComplaintToViolationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ComplaintResponse>>> SearchComplaintsResidentAsync(
        Guid userId,
        ComplaintSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplaintResponse>> GetComplaintResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplaintResponse>> CreateComplaintResidentAsync(
        Guid userId,
        CreateComplaintRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ViolationResponse>> SearchViolationsAsync(
        ViolationSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationResponse>> GetViolationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationResponse>> CreateViolationAsync(
        Guid? createdByUserId,
        CreateViolationRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ViolationFineResponse>> SearchViolationFinesAdminAsync(
        ViolationFineSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationFineResponse>> GetViolationFineAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationFineResponse>> CreateViolationFineAsync(
        CreateViolationFineRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationFineResponse>> CancelViolationFineAsync(
        Guid id,
        CancelViolationFineRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ViolationFineResponse>>> SearchViolationFinesResidentAsync(
        Guid userId,
        ViolationFineSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationFineResponse>> GetViolationFineResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);
}
