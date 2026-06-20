using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Services;

public sealed class CommunicationService(
    IAnnouncementService announcementService,
    IResidentNotificationService residentNotificationService,
    ICommunityPollService communityPollService)
    : ICommunicationService
{
    public Task<PagedResult<AnnouncementResponse>> SearchAnnouncementsAsync(
        AnnouncementSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        return announcementService.SearchAnnouncementsAsync(query, cancellationToken);
    }

    public Task<ServiceResult<PagedResult<AnnouncementResponse>>> SearchActiveAnnouncementsAsync(
        AnnouncementSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return announcementService.SearchActiveAnnouncementsAsync(query, currentUserId, cancellationToken);
    }

    public Task<ServiceResult<AnnouncementResponse>> GetAnnouncementAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        return announcementService.GetAnnouncementAsync(id, currentUserId, isManager, cancellationToken);
    }

    public Task<ServiceResult<AnnouncementResponse>> CreateAnnouncementAsync(
        Guid? currentUserId,
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        return announcementService.CreateAnnouncementAsync(currentUserId, request, cancellationToken);
    }

    public Task<ServiceResult<AnnouncementResponse>> UpdateAnnouncementAsync(
        Guid id,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        return announcementService.UpdateAnnouncementAsync(id, request, cancellationToken);
    }

    public Task<ServiceResult<AnnouncementResponse>> PublishAnnouncementAsync(
        Guid id,
        PublishAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        return announcementService.PublishAnnouncementAsync(id, request, cancellationToken);
    }

    public Task<ServiceResult<AnnouncementResponse>> ArchiveAnnouncementAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return announcementService.ArchiveAnnouncementAsync(id, cancellationToken);
    }

    public Task<ServiceResult<AnnouncementReadReceiptResponse>> MarkAnnouncementAsReadAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return announcementService.MarkAnnouncementAsReadAsync(id, currentUserId, cancellationToken);
    }

    public Task<ServiceResult<ResidentNotificationResponse>> CreateNotificationAsync(
        CreateResidentNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return residentNotificationService.CreateNotificationAsync(request, cancellationToken);
    }

    public Task<ServiceResult<PagedResult<ResidentNotificationResponse>>> SearchNotificationsAsync(
        ResidentNotificationSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return residentNotificationService.SearchNotificationsAsync(query, currentUserId, cancellationToken);
    }

    public Task<ServiceResult<ResidentNotificationResponse>> MarkNotificationAsReadAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return residentNotificationService.MarkNotificationAsReadAsync(id, currentUserId, cancellationToken);
    }

    public Task<ServiceResult<object?>> MarkAllNotificationsAsReadAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return residentNotificationService.MarkAllNotificationsAsReadAsync(currentUserId, cancellationToken);
    }

    public Task<PagedResult<CommunityPollResponse>> SearchPollsAsync(
        CommunityPollSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.SearchPollsAsync(query, cancellationToken);
    }

    public Task<ServiceResult<PagedResult<CommunityPollResponse>>> SearchOpenPollsAsync(
        CommunityPollSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.SearchOpenPollsAsync(query, currentUserId, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> GetPollAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.GetPollAsync(id, currentUserId, isManager, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> CreatePollAsync(
        Guid? currentUserId,
        CreateCommunityPollRequest request,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.CreatePollAsync(currentUserId, request, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> UpdatePollAsync(
        Guid id,
        UpdateCommunityPollRequest request,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.UpdatePollAsync(id, request, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> OpenPollAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.OpenPollAsync(id, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> ClosePollAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.ClosePollAsync(id, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> ArchivePollAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.ArchivePollAsync(id, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResponse>> SubmitVoteAsync(
        Guid id,
        Guid? currentUserId,
        SubmitCommunityPollVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.SubmitVoteAsync(id, currentUserId, request, cancellationToken);
    }

    public Task<ServiceResult<CommunityPollResultResponse>> GetPollResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return communityPollService.GetPollResultsAsync(id, cancellationToken);
    }
}
