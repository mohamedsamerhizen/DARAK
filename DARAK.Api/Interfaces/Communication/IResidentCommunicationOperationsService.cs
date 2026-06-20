using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface IResidentCommunicationOperationsService
{
    Task<ServiceResult<ResidentCommunicationOperationsSummaryResponse>> GetAdminSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageDetailsResponse>> CreateUtilityOutageAsync(
        Guid? currentUserId,
        CreateUtilityOutageRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<UtilityOutageResponse>> SearchUtilityOutagesAsync(
        UtilityOutageQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageDetailsResponse>> GetUtilityOutageAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageDetailsResponse>> PublishUtilityOutageUpdateAsync(
        Guid id,
        Guid? currentUserId,
        PublishUtilityOutageUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageDetailsResponse>> ResolveUtilityOutageAsync(
        Guid id,
        Guid? currentUserId,
        ResolveUtilityOutageRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageDetailsResponse>> CancelUtilityOutageAsync(
        Guid id,
        Guid? currentUserId,
        CancelUtilityOutageRequest request,
        CancellationToken cancellationToken = default);



    Task<ServiceResult<CommunicationCommandCenterResponse>> GetCommunicationCommandCenterAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementAcknowledgementBoardResponse>> GetAnnouncementAcknowledgementBoardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageOperationsBoardResponse>> GetUtilityOutageOperationsBoardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentCommunicationImpactReportResponse>> GetResidentCommunicationImpactReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunicationResponseIntelligenceResponse>> GetCommunicationResponseIntelligenceAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunicationRiskDashboardResponse>> GetCommunicationRiskDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentCommunicationOperationsSummaryResponse>> GetResidentSummaryAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<UtilityOutageResponse>>> SearchResidentUtilityOutagesAsync(
        Guid? currentUserId,
        UtilityOutageQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityOutageDetailsResponse>> GetResidentUtilityOutageAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
