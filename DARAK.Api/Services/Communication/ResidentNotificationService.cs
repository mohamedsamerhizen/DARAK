using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentNotificationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IResidentNotificationService
{
    public async Task<ServiceResult<ResidentNotificationResponse>> CreateNotificationAsync(
        CreateResidentNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateNotificationRequest(request);
        if (validation is not null)
        {
            return ToResult<ResidentNotificationResponse>(validation);
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return ServiceResult<ResidentNotificationResponse>.NotFound("User was not found.");
        }

        if (!await CanCurrentScopeAccessTargetUserAsync(request.UserId, cancellationToken))
        {
            return ServiceResult<ResidentNotificationResponse>.Forbidden("Current user cannot send notifications to this user.");
        }

        var notification = new ResidentNotification
        {
            UserId = request.UserId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Type = request.Type,
            Severity = request.Severity,
            RelatedEntityType = TrimOrNull(request.RelatedEntityType),
            RelatedEntityId = request.RelatedEntityId
        };

        dbContext.ResidentNotifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ResidentNotificationResponse>.Success(
            ToNotificationResponse(notification));
    }

    public async Task<ServiceResult<PagedResult<ResidentNotificationResponse>>> SearchNotificationsAsync(
        ResidentNotificationSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<ResidentNotificationResponse>>.BadRequest("Current user is invalid.");
        }

        var notifications = ApplyNotificationFilters(
            dbContext.ResidentNotifications
                .AsNoTracking()
                .Where(notification => notification.UserId == currentUserId.Value),
            query);

        return ServiceResult<PagedResult<ResidentNotificationResponse>>.Success(
            await ToPagedNotificationResultAsync(notifications, query, cancellationToken));
    }

    public async Task<ServiceResult<ResidentNotificationResponse>> MarkNotificationAsReadAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentNotificationResponse>.BadRequest("Current user is invalid.");
        }

        var notification = await dbContext.ResidentNotifications
            .FirstOrDefaultAsync(item =>
                item.Id == id && item.UserId == currentUserId.Value,
                cancellationToken);
        if (notification is null)
        {
            return ServiceResult<ResidentNotificationResponse>.NotFound("Notification was not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<ResidentNotificationResponse>.Success(
            ToNotificationResponse(notification));
    }

    public async Task<ServiceResult<object?>> MarkAllNotificationsAsReadAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<object?>.BadRequest("Current user is invalid.");
        }

        var unreadNotifications = await dbContext.ResidentNotifications
            .Where(notification => notification.UserId == currentUserId.Value && !notification.IsRead)
            .ToArrayAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    private async Task<IQueryable<Announcement>> ApplyCurrentAnnouncementCompoundAccessAsync(
        IQueryable<Announcement> announcements,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return announcements;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return announcements.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return announcements;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return announcements.Where(_ => false);
        }

        return announcements.Where(announcement => scope.AllowedCompoundIds.Contains(announcement.CompoundId));
    }

    private async Task<IQueryable<CommunityPoll>> ApplyCurrentPollCompoundAccessAsync(
        IQueryable<CommunityPoll> polls,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return polls;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return polls.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return polls;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return polls.Where(_ => false);
        }

        return polls.Where(poll => scope.AllowedCompoundIds.Contains(poll.CompoundId));
    }

    private async Task<bool> CanAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<bool> CanCurrentScopeAccessTargetUserAsync(
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return true;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return false;
        }

        if (scope.IsSuperAdmin)
        {
            return true;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return false;
        }

        var residentInScope = await dbContext.ResidentProfiles
            .AsNoTracking()
            .AnyAsync(profile =>
                profile.UserId == targetUserId
                && profile.IsActive
                && scope.AllowedCompoundIds.Contains(profile.CompoundId),
                cancellationToken);
        if (residentInScope)
        {
            return true;
        }

        return await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .AnyAsync(assignment =>
                assignment.UserId == targetUserId
                && assignment.IsActive
                && scope.AllowedCompoundIds.Contains(assignment.CompoundId),
                cancellationToken);
    }

    private async Task<ServiceResult<Guid>> ResolveCompoundIdAsync(
        Guid? compoundId,
        CancellationToken cancellationToken)
    {
        if (!compoundId.HasValue || compoundId.Value == Guid.Empty)
        {
            return ServiceResult<Guid>.BadRequest("Compound id is required.");
        }

        var exists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == compoundId.Value && compound.IsActive, cancellationToken);
        if (!exists)
        {
            return ServiceResult<Guid>.NotFound("Compound was not found.");
        }

        return ServiceResult<Guid>.Success(compoundId.Value);
    }

    private async Task<Guid[]> GetResidentCompoundIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record =>
                record.OccupancyStatus == OccupancyStatus.Active
                && record.ResidentProfile.UserId == currentUserId
                && record.ResidentProfile.IsActive)
            .Select(record => record.PropertyUnit.CompoundId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private IQueryable<CommunityPoll> GetPollDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.CommunityPolls
            .Include(poll => poll.Options)
            .Include(poll => poll.Votes)
            .AsSplitQuery()
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<AnnouncementResponse> ToAnnouncementResponseAsync(
        Announcement announcement,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var readCount = await dbContext.AnnouncementReadReceipts
            .AsNoTracking()
            .CountAsync(receipt => receipt.AnnouncementId == announcement.Id, cancellationToken);
        var isRead = currentUserId.HasValue
            && await dbContext.AnnouncementReadReceipts
                .AsNoTracking()
                .AnyAsync(receipt =>
                    receipt.AnnouncementId == announcement.Id && receipt.UserId == currentUserId.Value,
                    cancellationToken);

        return ToAnnouncementResponse(announcement, isRead, readCount);
    }

    private async Task<IReadOnlyCollection<AnnouncementAudience>> GetAllowedAnnouncementAudiencesAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var audiences = new List<AnnouncementAudience>
        {
            AnnouncementAudience.AllResidents
        };

        var occupancyTypes = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.ResidentProfile.UserId == currentUserId
                && record.OccupancyStatus == OccupancyStatus.Active)
            .Select(record => record.OccupancyType)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (occupancyTypes.Contains(OccupancyType.Tenant))
        {
            audiences.Add(AnnouncementAudience.Tenants);
        }

        if (occupancyTypes.Contains(OccupancyType.OwnerCash)
            || occupancyTypes.Contains(OccupancyType.OwnerInstallment))
        {
            audiences.Add(AnnouncementAudience.Owners);
        }

        return audiences;
    }

    private async Task<PagedResult<AnnouncementResponse>> ToPagedAnnouncementResultAsync(
        IQueryable<Announcement> query,
        AnnouncementSearchQuery pagination,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenByDescending(announcement => announcement.PublishedAt ?? announcement.CreatedAt)
            .ThenByDescending(announcement => announcement.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(announcement => new AnnouncementResponse(
                announcement.Id,
                announcement.Title,
                announcement.Body,
                announcement.Category,
                announcement.Priority,
                announcement.Audience,
                announcement.Status,
                announcement.CompoundId,
                announcement.PublishedAt,
                announcement.ExpiresAt,
                announcement.CreatedByUserId,
                announcement.CreatedAt,
                announcement.UpdatedAt,
                announcement.IsPinned,
                announcement.IsActive,
                currentUserId.HasValue
                    && announcement.ReadReceipts.Any(receipt => receipt.UserId == currentUserId.Value),
                announcement.ReadReceipts.Count))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<AnnouncementResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<ResidentNotificationResponse>> ToPagedNotificationResultAsync(
        IQueryable<ResidentNotification> query,
        ResidentNotificationSearchQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(notification => notification.IsRead)
            .ThenByDescending(notification => notification.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(notification => new ResidentNotificationResponse(
                notification.Id,
                notification.UserId,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.Severity,
                notification.RelatedEntityType,
                notification.RelatedEntityId,
                notification.IsRead,
                notification.ReadAt,
                notification.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ResidentNotificationResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<CommunityPollResponse>> ToPagedPollResultAsync(
        IQueryable<CommunityPoll> query,
        CommunityPollSearchQuery pagination,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var polls = await query
            .Include(poll => poll.Options)
            .Include(poll => poll.Votes)
            .AsSplitQuery()
            .OrderByDescending(poll => poll.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = polls
            .Select(poll => ToPollResponse(poll, currentUserId))
            .ToArray();

        return new PagedResult<CommunityPollResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private static IQueryable<Announcement> ApplyAnnouncementFilters(
        IQueryable<Announcement> announcements,
        AnnouncementSearchQuery query)
    {
        if (query.Status.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.Status == query.Status.Value);
        }

        if (query.Category.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.Category == query.Category.Value);
        }

        if (query.Priority.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.Priority == query.Priority.Value);
        }

        if (query.Audience.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.Audience == query.Audience.Value);
        }

        if (query.CompoundId.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.CompoundId == query.CompoundId.Value);
        }

        if (query.IsPinned.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.IsPinned == query.IsPinned.Value);
        }

        if (query.IsActive.HasValue)
        {
            announcements = announcements.Where(announcement => announcement.IsActive == query.IsActive.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            announcements = announcements.Where(announcement =>
                announcement.Title.Contains(searchTerm)
                || announcement.Body.Contains(searchTerm));
        }

        return announcements;
    }

    private static IQueryable<Announcement> ApplyActiveAnnouncementScope(
        IQueryable<Announcement> announcements,
        IReadOnlyCollection<AnnouncementAudience> allowedAudiences,
        Guid[] residentCompoundIds,
        DateTime now)
    {
        return announcements.Where(announcement =>
            announcement.IsActive
            && announcement.Status == AnnouncementStatus.Published
            && residentCompoundIds.Contains(announcement.CompoundId)
            && (!announcement.PublishedAt.HasValue || announcement.PublishedAt <= now)
            && (!announcement.ExpiresAt.HasValue || announcement.ExpiresAt > now)
            && allowedAudiences.Contains(announcement.Audience));
    }

    private static IQueryable<ResidentNotification> ApplyNotificationFilters(
        IQueryable<ResidentNotification> notifications,
        ResidentNotificationSearchQuery query)
    {
        if (query.IsRead.HasValue)
        {
            notifications = notifications.Where(notification => notification.IsRead == query.IsRead.Value);
        }

        if (query.Type.HasValue)
        {
            notifications = notifications.Where(notification => notification.Type == query.Type.Value);
        }

        if (query.Severity.HasValue)
        {
            notifications = notifications.Where(notification => notification.Severity == query.Severity.Value);
        }

        return notifications;
    }

    private static IQueryable<CommunityPoll> ApplyPollFilters(
        IQueryable<CommunityPoll> polls,
        CommunityPollSearchQuery query)
    {
        if (query.Status.HasValue)
        {
            polls = polls.Where(poll => poll.Status == query.Status.Value);
        }

        if (query.StartsFrom.HasValue)
        {
            polls = polls.Where(poll => poll.StartsAt >= query.StartsFrom.Value);
        }

        if (query.EndsTo.HasValue)
        {
            polls = polls.Where(poll => poll.EndsAt <= query.EndsTo.Value);
        }

        if (query.CompoundId.HasValue)
        {
            polls = polls.Where(poll => poll.CompoundId == query.CompoundId.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            polls = polls.Where(poll =>
                poll.Question.Contains(searchTerm)
                || (poll.Description != null && poll.Description.Contains(searchTerm)));
        }

        return polls;
    }

    private static IQueryable<CommunityPoll> ApplyOpenPollScope(
        IQueryable<CommunityPoll> polls,
        Guid[] residentCompoundIds,
        DateTime now)
    {
        return polls.Where(poll =>
            poll.Status == CommunityPollStatus.Open
            && residentCompoundIds.Contains(poll.CompoundId)
            && poll.StartsAt <= now
            && poll.EndsAt >= now);
    }

    private static ValidationFailure? ValidateAnnouncementRequest(
        string title,
        string body,
        DateTime? expiresAt)
    {
        if (TrimOrNull(title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Announcement title is required.");
        }

        if (TrimOrNull(body) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Announcement body is required.");
        }

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Announcement expiry must be in the future.");
        }

        return null;
    }

    private static ValidationFailure? ValidateNotificationRequest(CreateResidentNotificationRequest request)
    {
        if (request.UserId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "User id is required.");
        }

        if (TrimOrNull(request.Title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Notification title is required.");
        }

        if (TrimOrNull(request.Message) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Notification message is required.");
        }

        return null;
    }

    private static ValidationFailure? ValidatePollRequest(
        string question,
        DateTime startsAt,
        DateTime endsAt,
        IReadOnlyCollection<CreateCommunityPollOptionRequest> options)
    {
        if (TrimOrNull(question) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll question is required.");
        }

        if (startsAt == default)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll start time is required.");
        }

        if (endsAt == default)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll end time is required.");
        }

        if (endsAt <= startsAt)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll end time must be after start time.");
        }

        if (options.Count < 2)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll must have at least two options.");
        }

        if (options.Any(option => TrimOrNull(option.Text) is null))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll option text is required.");
        }

        if (options.Any(option => option.DisplayOrder < 0))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll option display order cannot be negative.");
        }

        var duplicateOptionExists = options
            .Select(option => option.Text.Trim())
            .GroupBy(option => option, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (duplicateOptionExists)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll options must be unique.");
        }

        return null;
    }

    private static ValidationFailure? ValidatePollCanOpen(CommunityPoll poll)
    {
        if (poll.Options.Count < 2)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll must have at least two options.");
        }

        if (poll.EndsAt <= poll.StartsAt)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll end time must be after start time.");
        }

        if (poll.EndsAt <= DateTime.UtcNow)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Poll end time must be in the future.");
        }

        return null;
    }

    private static IReadOnlyCollection<PollOptionInput> NormalizePollOptions(
        IReadOnlyCollection<CreateCommunityPollOptionRequest> options)
    {
        return options
            .Select((option, index) => new PollOptionInput(
                option.Text.Trim(),
                option.DisplayOrder == 0 ? index + 1 : option.DisplayOrder))
            .OrderBy(option => option.DisplayOrder)
            .ThenBy(option => option.Text)
            .ToArray();
    }

    private static AnnouncementResponse ToAnnouncementResponse(
        Announcement announcement,
        bool isRead,
        int readCount)
    {
        return new AnnouncementResponse(
            announcement.Id,
            announcement.Title,
            announcement.Body,
            announcement.Category,
            announcement.Priority,
            announcement.Audience,
            announcement.Status,
            announcement.CompoundId,
            announcement.PublishedAt,
            announcement.ExpiresAt,
            announcement.CreatedByUserId,
            announcement.CreatedAt,
            announcement.UpdatedAt,
            announcement.IsPinned,
            announcement.IsActive,
            isRead,
            readCount);
    }

    private static AnnouncementReadReceiptResponse ToReadReceiptResponse(AnnouncementReadReceipt receipt)
    {
        return new AnnouncementReadReceiptResponse(
            receipt.Id,
            receipt.AnnouncementId,
            receipt.UserId,
            receipt.ReadAt);
    }

    private static ResidentNotificationResponse ToNotificationResponse(ResidentNotification notification)
    {
        return new ResidentNotificationResponse(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Message,
            notification.Type,
            notification.Severity,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.IsRead,
            notification.ReadAt,
            notification.CreatedAt);
    }

    private static CommunityPollResponse ToPollResponse(
        CommunityPoll poll,
        Guid? currentUserId)
    {
        var options = poll.Options
            .OrderBy(option => option.DisplayOrder)
            .ThenBy(option => option.Text)
            .Select(option => new CommunityPollOptionResponse(
                option.Id,
                option.Text,
                option.DisplayOrder))
            .ToArray();
        var selectedOptionIds = currentUserId.HasValue
            ? poll.Votes
                .Where(vote => vote.UserId == currentUserId.Value)
                .Select(vote => vote.PollOptionId)
                .Distinct()
                .ToArray()
            : Array.Empty<Guid>();

        return new CommunityPollResponse(
            poll.Id,
            poll.Question,
            poll.Description,
            poll.Status,
            poll.CompoundId,
            poll.StartsAt,
            poll.EndsAt,
            poll.AllowsMultipleChoices,
            poll.CreatedByUserId,
            poll.CreatedAt,
            poll.UpdatedAt,
            options,
            selectedOptionIds);
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);

    private sealed record PollOptionInput(string Text, int DisplayOrder);
}
