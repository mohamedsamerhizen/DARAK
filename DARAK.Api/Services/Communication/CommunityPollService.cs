using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CommunityPollService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : ICommunityPollService
{
    public async Task<PagedResult<CommunityPollResponse>> SearchPollsAsync(
        CommunityPollSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var polls = ApplyPollFilters(dbContext.CommunityPolls.AsNoTracking(), query);
        polls = await ApplyCurrentPollCompoundAccessAsync(polls, cancellationToken);

        return await ToPagedPollResultAsync(
            polls,
            query,
            currentUserId: null,
            cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<CommunityPollResponse>>> SearchOpenPollsAsync(
        CommunityPollSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<CommunityPollResponse>>.BadRequest("Current user is invalid.");
        }

        var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
        var polls = ApplyOpenPollScope(
            ApplyPollFilters(dbContext.CommunityPolls.AsNoTracking(), query),
            residentCompoundIds,
            DateTime.UtcNow);

        return ServiceResult<PagedResult<CommunityPollResponse>>.Success(
            await ToPagedPollResultAsync(
                polls,
                query,
                currentUserId,
                cancellationToken));
    }

    public async Task<ServiceResult<CommunityPollResponse>> GetPollAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        var polls = GetPollDetailsQuery(asNoTracking: true)
            .Where(poll => poll.Id == id);

        if (isManager)
        {
            polls = await ApplyCurrentPollCompoundAccessAsync(polls, cancellationToken);
        }
        else
        {
            if (!currentUserId.HasValue)
            {
                return ServiceResult<CommunityPollResponse>.BadRequest("Current user is invalid.");
            }

            var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
            polls = ApplyOpenPollScope(polls, residentCompoundIds, DateTime.UtcNow);
        }

        var poll = await polls.FirstOrDefaultAsync(cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        return ServiceResult<CommunityPollResponse>.Success(
            ToPollResponse(poll, currentUserId));
    }

    public async Task<ServiceResult<CommunityPollResponse>> CreatePollAsync(
        Guid? currentUserId,
        CreateCommunityPollRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePollRequest(
            request.Question,
            request.StartsAt,
            request.EndsAt,
            request.Options);
        if (validation is not null)
        {
            return ToResult<CommunityPollResponse>(validation);
        }

        var compoundResult = await ResolveCompoundIdAsync(request.CompoundId, cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<CommunityPollResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Poll compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<CommunityPollResponse>.Forbidden("Current user cannot access this compound.");
        }

        var poll = new CommunityPoll
        {
            Question = request.Question.Trim(),
            Description = TrimOrNull(request.Description),
            CompoundId = compoundResult.Value,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            AllowsMultipleChoices = request.AllowsMultipleChoices,
            CreatedByUserId = currentUserId
        };

        foreach (var option in NormalizePollOptions(request.Options))
        {
            poll.Options.Add(new CommunityPollOption
            {
                Text = option.Text,
                DisplayOrder = option.DisplayOrder
            });
        }

        dbContext.CommunityPolls.Add(poll);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CommunityPollResponse>.Success(
            ToPollResponse(poll, currentUserId: null));
    }

    public async Task<ServiceResult<CommunityPollResponse>> UpdatePollAsync(
        Guid id,
        UpdateCommunityPollRequest request,
        CancellationToken cancellationToken = default)
    {
        var poll = await GetPollDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (!await CanAccessCompoundAsync(poll.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (poll.Status != CommunityPollStatus.Draft)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("Only draft polls can be updated.");
        }

        var validation = ValidatePollRequest(
            request.Question,
            request.StartsAt,
            request.EndsAt,
            request.Options);
        if (validation is not null)
        {
            return ToResult<CommunityPollResponse>(validation);
        }

        var compoundResult = await ResolveCompoundIdAsync(request.CompoundId, cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<CommunityPollResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Poll compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<CommunityPollResponse>.Forbidden("Current user cannot access this compound.");
        }

        poll.Question = request.Question.Trim();
        poll.Description = TrimOrNull(request.Description);
        poll.CompoundId = compoundResult.Value;
        poll.StartsAt = request.StartsAt;
        poll.EndsAt = request.EndsAt;
        poll.AllowsMultipleChoices = request.AllowsMultipleChoices;
        poll.UpdatedAt = DateTime.UtcNow;

        var existingOptions = poll.Options.ToArray();
        dbContext.CommunityPollOptions.RemoveRange(existingOptions);
        poll.Options.Clear();
        foreach (var option in NormalizePollOptions(request.Options))
        {
            poll.Options.Add(new CommunityPollOption
            {
                PollId = poll.Id,
                Text = option.Text,
                DisplayOrder = option.DisplayOrder
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CommunityPollResponse>.Success(
            ToPollResponse(poll, currentUserId: null));
    }

    public async Task<ServiceResult<CommunityPollResponse>> OpenPollAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var poll = await GetPollDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (!await CanAccessCompoundAsync(poll.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (poll.Status == CommunityPollStatus.Open)
        {
            return ServiceResult<CommunityPollResponse>.Success(ToPollResponse(poll, currentUserId: null));
        }

        if (poll.Status != CommunityPollStatus.Draft)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("Only draft polls can be opened.");
        }

        var validation = ValidatePollCanOpen(poll);
        if (validation is not null)
        {
            return ToResult<CommunityPollResponse>(validation);
        }

        poll.Status = CommunityPollStatus.Open;
        poll.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CommunityPollResponse>.Success(ToPollResponse(poll, currentUserId: null));
    }

    public async Task<ServiceResult<CommunityPollResponse>> ClosePollAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var poll = await GetPollDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (!await CanAccessCompoundAsync(poll.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (poll.Status != CommunityPollStatus.Open)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("Only open polls can be closed.");
        }

        poll.Status = CommunityPollStatus.Closed;
        poll.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CommunityPollResponse>.Success(ToPollResponse(poll, currentUserId: null));
    }

    public async Task<ServiceResult<CommunityPollResponse>> ArchivePollAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var poll = await GetPollDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        if (!await CanAccessCompoundAsync(poll.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        poll.Status = CommunityPollStatus.Archived;
        poll.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CommunityPollResponse>.Success(ToPollResponse(poll, currentUserId: null));
    }

    public async Task<ServiceResult<CommunityPollResponse>> SubmitVoteAsync(
        Guid id,
        Guid? currentUserId,
        SubmitCommunityPollVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("Current user is invalid.");
        }

        var poll = await dbContext.CommunityPolls
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
        if (!residentCompoundIds.Contains(poll.CompoundId))
        {
            return ServiceResult<CommunityPollResponse>.NotFound("Community poll was not found.");
        }

        var now = DateTime.UtcNow;
        if (poll.Status != CommunityPollStatus.Open)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("Only open polls accept votes.");
        }

        if (now < poll.StartsAt || now > poll.EndsAt)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("Poll is not accepting votes at this time.");
        }

        var selectedOptionIds = request.PollOptionIds
            .Where(optionId => optionId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (selectedOptionIds.Length == 0)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("At least one poll option is required.");
        }

        if (!poll.AllowsMultipleChoices && selectedOptionIds.Length != 1)
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("This poll allows one option only.");
        }

        var availableOptionIds = poll.Options
            .Select(option => option.Id)
            .ToHashSet();
        if (selectedOptionIds.Any(optionId => !availableOptionIds.Contains(optionId)))
        {
            return ServiceResult<CommunityPollResponse>.BadRequest("One or more poll options are invalid.");
        }

        if (poll.AllowsMultipleChoices)
        {
            var existingOptionIds = await dbContext.CommunityPollVotes
                .AsNoTracking()
                .Where(vote => vote.PollId == poll.Id
                    && vote.UserId == currentUserId.Value
                    && selectedOptionIds.Contains(vote.PollOptionId))
                .Select(vote => vote.PollOptionId)
                .ToArrayAsync(cancellationToken);
            if (existingOptionIds.Length > 0)
            {
                return ServiceResult<CommunityPollResponse>.Conflict(
                    "Current user already voted for one or more selected options.");
            }
        }
        else
        {
            var hasExistingVote = await dbContext.CommunityPollVotes
                .AsNoTracking()
                .AnyAsync(vote =>
                    vote.PollId == poll.Id && vote.UserId == currentUserId.Value,
                    cancellationToken);
            if (hasExistingVote)
            {
                return ServiceResult<CommunityPollResponse>.Conflict("Current user already voted in this poll.");
            }
        }

        foreach (var optionId in selectedOptionIds)
        {
            dbContext.CommunityPollVotes.Add(new CommunityPollVote
            {
                PollId = poll.Id,
                PollOptionId = optionId,
                UserId = currentUserId.Value
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetPollAsync(poll.Id, currentUserId, isManager: false, cancellationToken);
    }

    public async Task<ServiceResult<CommunityPollResultResponse>> GetPollResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var poll = await GetPollDetailsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poll is null)
        {
            return ServiceResult<CommunityPollResultResponse>.NotFound("Community poll was not found.");
        }

        if (!await CanAccessCompoundAsync(poll.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunityPollResultResponse>.NotFound("Community poll was not found.");
        }

        var totalVotes = poll.Votes.Count;
        var totalVoters = poll.Votes
            .Select(vote => vote.UserId)
            .Distinct()
            .Count();
        var optionResults = poll.Options
            .OrderBy(option => option.DisplayOrder)
            .ThenBy(option => option.Text)
            .Select(option =>
            {
                var voteCount = poll.Votes.Count(vote => vote.PollOptionId == option.Id);
                var percentage = totalVotes == 0
                    ? 0m
                    : Math.Round(voteCount * 100m / totalVotes, 2, MidpointRounding.AwayFromZero);

                return new CommunityPollOptionResultResponse(
                    option.Id,
                    option.Text,
                    option.DisplayOrder,
                    voteCount,
                    percentage);
            })
            .ToArray();

        return ServiceResult<CommunityPollResultResponse>.Success(
            new CommunityPollResultResponse(
                poll.Id,
                poll.Question,
                poll.Status,
                totalVotes,
                totalVoters,
                optionResults));
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
