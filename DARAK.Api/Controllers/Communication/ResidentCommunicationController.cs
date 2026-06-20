using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/communication")]
public sealed class ResidentCommunicationController(
    IAnnouncementService announcementService,
    ICommunityPollService communityPollService,
    IResidentNotificationService residentNotificationService,
    IConversationService conversationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("announcements")]
    public async Task<ActionResult<PagedResult<AnnouncementResponse>>> SearchActiveAnnouncements(
        [FromQuery] AnnouncementSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.SearchActiveAnnouncementsAsync(
            query,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("announcements/{id:guid}")]
    public async Task<ActionResult<AnnouncementResponse>> GetAnnouncement(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.GetAnnouncementAsync(
            id,
            currentUserService.UserId,
            isManager: false,
            cancellationToken));
    }

    [HttpPatch("announcements/{id:guid}/read")]
    public async Task<ActionResult<AnnouncementReadReceiptResponse>> MarkAnnouncementAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.MarkAnnouncementAsReadAsync(
            id,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("polls")]
    public async Task<ActionResult<PagedResult<CommunityPollResponse>>> SearchOpenPolls(
        [FromQuery] CommunityPollSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.SearchOpenPollsAsync(
            query,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("polls/{id:guid}")]
    public async Task<ActionResult<CommunityPollResponse>> GetPoll(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.GetPollAsync(
            id,
            currentUserService.UserId,
            isManager: false,
            cancellationToken));
    }

    [HttpPost("polls/{id:guid}/vote")]
    public async Task<ActionResult<CommunityPollResponse>> Vote(
        Guid id,
        SubmitCommunityPollVoteRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.SubmitVoteAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<PagedResult<ResidentNotificationResponse>>> SearchNotifications(
        [FromQuery] ResidentNotificationSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentNotificationService.SearchNotificationsAsync(
            query,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpPatch("notifications/{id:guid}/read")]
    public async Task<ActionResult<ResidentNotificationResponse>> MarkNotificationAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentNotificationService.MarkNotificationAsReadAsync(
            id,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpPatch("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsAsRead(CancellationToken cancellationToken)
    {
        return ToNoContentResult(await residentNotificationService.MarkAllNotificationsAsReadAsync(
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ResidentConversationResponse>> OpenConversation(
        ResidentOpenConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await conversationService.OpenResidentConversationAsync(
            currentUserService.UserId,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetConversation), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResult<ResidentConversationResponse>>> SearchConversations(
        [FromQuery] ConversationSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.SearchResidentConversationsAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ResidentConversationResponse>> GetConversation(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.GetResidentConversationAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ResidentConversationResponse>> SendMessage(
        Guid id,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.AddResidentMessageAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPost("conversations/{id:guid}/reopen")]
    public async Task<ActionResult<ResidentConversationResponse>> ReopenConversation(
        Guid id,
        ReopenConversationRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.ReopenResidentConversationAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

}
