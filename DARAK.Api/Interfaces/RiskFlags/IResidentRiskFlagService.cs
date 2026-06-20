using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.RiskFlags;

namespace DARAK.Api.Interfaces;

public interface IResidentRiskFlagService
{
    Task<ServiceResult<ResidentRiskFlagResponse>> CreateFlagAsync(
        Guid? currentUserId,
        CreateResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ResidentRiskFlagResponse>>> SearchFlagsAsync(
        Guid? currentUserId,
        ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ResidentRiskFlagResponse>>> GetResidentFlagsAsync(
        Guid? currentUserId,
        Guid residentProfileId,
        ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> GetDetailsAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> AssignAsync(
        Guid? currentUserId,
        Guid id,
        AssignResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> ChangeSeverityAsync(
        Guid? currentUserId,
        Guid id,
        ChangeResidentRiskFlagSeverityRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> MarkReviewedAsync(
        Guid? currentUserId,
        Guid id,
        ReviewResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> ResolveAsync(
        Guid? currentUserId,
        Guid id,
        CloseResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> DismissAsync(
        Guid? currentUserId,
        Guid id,
        CloseResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDetailsResponse>> AddNoteAsync(
        Guid? currentUserId,
        Guid id,
        AddResidentRiskFlagNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentRiskFlagDashboardResponse>> GetDashboardAsync(
        Guid? currentUserId,
        Guid? compoundId,
        CancellationToken cancellationToken = default);
}
