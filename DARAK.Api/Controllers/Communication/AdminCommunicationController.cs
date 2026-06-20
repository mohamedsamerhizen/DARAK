using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.CommunicationManagers)]
[Route("api/admin/communication")]
public sealed class AdminCommunicationController(
    IAnnouncementService announcementService,
    ICommunityPollService communityPollService,
    IResidentNotificationService residentNotificationService,
    IConversationService conversationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("announcements")]
    public async Task<ActionResult<PagedResult<AnnouncementResponse>>> SearchAnnouncements(
        [FromQuery] AnnouncementSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await announcementService.SearchAnnouncementsAsync(query, cancellationToken));
    }

    [HttpGet("announcements/{id:guid}")]
    public async Task<ActionResult<AnnouncementResponse>> GetAnnouncement(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.GetAnnouncementAsync(
            id,
            currentUserService.UserId,
            isManager: true,
            cancellationToken));
    }

    [HttpPost("announcements")]
    public async Task<ActionResult<AnnouncementResponse>> CreateAnnouncement(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await announcementService.CreateAnnouncementAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetAnnouncement), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("announcements/{id:guid}")]
    public async Task<ActionResult<AnnouncementResponse>> UpdateAnnouncement(
        Guid id,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.UpdateAnnouncementAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("announcements/{id:guid}/publish")]
    public async Task<ActionResult<AnnouncementResponse>> PublishAnnouncement(
        Guid id,
        PublishAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.PublishAnnouncementAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("announcements/{id:guid}/archive")]
    public async Task<ActionResult<AnnouncementResponse>> ArchiveAnnouncement(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await announcementService.ArchiveAnnouncementAsync(
            id,
            cancellationToken));
    }

    [HttpGet("polls")]
    public async Task<ActionResult<PagedResult<CommunityPollResponse>>> SearchPolls(
        [FromQuery] CommunityPollSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await communityPollService.SearchPollsAsync(query, cancellationToken));
    }

    [HttpGet("polls/{id:guid}")]
    public async Task<ActionResult<CommunityPollResponse>> GetPoll(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.GetPollAsync(
            id,
            currentUserService.UserId,
            isManager: true,
            cancellationToken));
    }

    [HttpPost("polls")]
    public async Task<ActionResult<CommunityPollResponse>> CreatePoll(
        CreateCommunityPollRequest request,
        CancellationToken cancellationToken)
    {
        var result = await communityPollService.CreatePollAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetPoll), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("polls/{id:guid}")]
    public async Task<ActionResult<CommunityPollResponse>> UpdatePoll(
        Guid id,
        UpdateCommunityPollRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.UpdatePollAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("polls/{id:guid}/open")]
    public async Task<ActionResult<CommunityPollResponse>> OpenPoll(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.OpenPollAsync(id, cancellationToken));
    }

    [HttpPatch("polls/{id:guid}/close")]
    public async Task<ActionResult<CommunityPollResponse>> ClosePoll(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.ClosePollAsync(id, cancellationToken));
    }

    [HttpPatch("polls/{id:guid}/archive")]
    public async Task<ActionResult<CommunityPollResponse>> ArchivePoll(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.ArchivePollAsync(id, cancellationToken));
    }

    [HttpGet("polls/{id:guid}/results")]
    public async Task<ActionResult<CommunityPollResultResponse>> GetPollResults(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await communityPollService.GetPollResultsAsync(id, cancellationToken));
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

    [HttpPost("notifications")]
    public async Task<ActionResult<ResidentNotificationResponse>> CreateNotification(
        CreateResidentNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentNotificationService.CreateNotificationAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return Created("/api/admin/communication/notifications", result.Value);
    }


    [HttpGet("support-dashboard")]
    public async Task<ActionResult<SupportDashboardResponse>> GetSupportDashboard(
        [FromQuery] SupportDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.GetSupportDashboardAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResult<ConversationResponse>>> SearchConversations(
        [FromQuery] ConversationSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.SearchAdminConversationsAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("conversations/unassigned")]
    public async Task<ActionResult<PagedResult<ConversationResponse>>> SearchUnassignedConversations(
        [FromQuery] ConversationSearchQuery query,
        CancellationToken cancellationToken)
    {
        var scopedQuery = new ConversationSearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            CompoundId = query.CompoundId,
            ResidentProfileId = query.ResidentProfileId,
            PropertyUnitId = query.PropertyUnitId,
            Status = query.Status,
            Priority = query.Priority,
            Topic = query.Topic,
            IssueType = query.IssueType,
            EscalationLevel = query.EscalationLevel,
            IsUnassigned = true,
            SearchTerm = query.SearchTerm
        };

        return ToActionResult(await conversationService.SearchAdminConversationsAsync(
            currentUserService.UserId,
            scopedQuery,
            cancellationToken));
    }

    [HttpGet("conversations/assigned-to-me")]
    public async Task<ActionResult<PagedResult<ConversationResponse>>> SearchAssignedToMeConversations(
        [FromQuery] ConversationSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return BadRequest(ApiErrorResponseFactory.Create(
                HttpContext,
                "Current user is invalid.",
                null));
        }

        var scopedQuery = new ConversationSearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            CompoundId = query.CompoundId,
            ResidentProfileId = query.ResidentProfileId,
            PropertyUnitId = query.PropertyUnitId,
            Status = query.Status,
            Priority = query.Priority,
            Topic = query.Topic,
            IssueType = query.IssueType,
            EscalationLevel = query.EscalationLevel,
            AssignedToUserId = currentUserService.UserId.Value,
            SearchTerm = query.SearchTerm
        };

        return ToActionResult(await conversationService.SearchAdminConversationsAsync(
            currentUserService.UserId,
            scopedQuery,
            cancellationToken));
    }


    [HttpGet("conversations/escalated")]
    public async Task<ActionResult<PagedResult<ConversationResponse>>> SearchEscalatedConversations(
        [FromQuery] ConversationSearchQuery query,
        CancellationToken cancellationToken)
    {
        var scopedQuery = new ConversationSearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            CompoundId = query.CompoundId,
            ResidentProfileId = query.ResidentProfileId,
            PropertyUnitId = query.PropertyUnitId,
            Status = query.Status,
            Priority = query.Priority,
            Topic = query.Topic,
            IssueType = query.IssueType,
            AssignedToUserId = query.AssignedToUserId,
            IsEscalated = true,
            SearchTerm = query.SearchTerm
        };

        return ToActionResult(await conversationService.SearchAdminConversationsAsync(
            currentUserService.UserId,
            scopedQuery,
            cancellationToken));
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<AdminConversationDetailsResponse>> GetConversation(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.GetAdminConversationDetailsAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ConversationResponse>> ReplyToConversation(
        Guid id,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.AddAdminReplyAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("conversations/{id:guid}/assign")]
    public async Task<ActionResult<ConversationResponse>> AssignConversation(
        Guid id,
        AssignConversationRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.AssignConversationAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("conversations/{id:guid}/priority")]
    public async Task<ActionResult<ConversationResponse>> ChangeConversationPriority(
        Guid id,
        ChangeConversationPriorityRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.ChangePriorityAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }


    [HttpPost("conversations/{id:guid}/escalate")]
    public async Task<ActionResult<ConversationResponse>> EscalateConversation(
        Guid id,
        EscalateConversationRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.EscalateConversationAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPost("conversations/{id:guid}/internal-notes")]
    public async Task<ActionResult<ConversationResponse>> AddInternalNote(
        Guid id,
        AddInternalNoteRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.AddInternalNoteAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("conversations/{id:guid}/resolve")]
    public async Task<ActionResult<ConversationResponse>> ResolveConversation(
        Guid id,
        CompleteConversationRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.ResolveConversationAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("conversations/{id:guid}/close")]
    public async Task<ActionResult<ConversationResponse>> CloseConversation(
        Guid id,
        CompleteConversationRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await conversationService.CloseConversationAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

}
