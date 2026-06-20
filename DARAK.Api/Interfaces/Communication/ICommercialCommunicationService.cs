using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface ICommercialCommunicationService
{
    Task<ServiceResult<ResidentNotificationPreferenceResponse>> GetPreferencesAsync(
        Guid? currentUserId,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentNotificationPreferenceResponse>> UpdatePreferencesAsync(
        Guid? currentUserId,
        Guid? userId,
        UpdateResidentNotificationPreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<CommunicationCampaignResponse>>> SearchCampaignsAsync(
        CommunicationCampaignSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunicationCampaignDetailsResponse>> GetCampaignAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunicationCampaignResponse>> CreateCampaignAsync(
        Guid? currentUserId,
        CreateCommunicationCampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunicationCampaignDetailsResponse>> SendCampaignAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunicationDeliveryAnalyticsResponse>> GetDeliveryAnalyticsAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);
}
