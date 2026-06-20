using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface ICommunicationService
{
    Task<PagedResult<AnnouncementResponse>> SearchAnnouncementsAsync(
        AnnouncementSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<AnnouncementResponse>>> SearchActiveAnnouncementsAsync(
        AnnouncementSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementResponse>> GetAnnouncementAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementResponse>> CreateAnnouncementAsync(
        Guid? currentUserId,
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementResponse>> UpdateAnnouncementAsync(
        Guid id,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementResponse>> PublishAnnouncementAsync(
        Guid id,
        PublishAnnouncementRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementResponse>> ArchiveAnnouncementAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnnouncementReadReceiptResponse>> MarkAnnouncementAsReadAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentNotificationResponse>> CreateNotificationAsync(
        CreateResidentNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ResidentNotificationResponse>>> SearchNotificationsAsync(
        ResidentNotificationSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentNotificationResponse>> MarkNotificationAsReadAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> MarkAllNotificationsAsReadAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<CommunityPollResponse>> SearchPollsAsync(
        CommunityPollSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<CommunityPollResponse>>> SearchOpenPollsAsync(
        CommunityPollSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> GetPollAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> CreatePollAsync(
        Guid? currentUserId,
        CreateCommunityPollRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> UpdatePollAsync(
        Guid id,
        UpdateCommunityPollRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> OpenPollAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> ClosePollAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> ArchivePollAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResponse>> SubmitVoteAsync(
        Guid id,
        Guid? currentUserId,
        SubmitCommunityPollVoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityPollResultResponse>> GetPollResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
