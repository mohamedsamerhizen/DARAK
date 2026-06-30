using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class AnnouncementService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null,
    IAuditLogService? auditLogService = null)
    : IAnnouncementService
{
    public async Task<PagedResult<AnnouncementResponse>> SearchAnnouncementsAsync(
        AnnouncementSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var announcements = ApplyAnnouncementFilters(dbContext.Announcements.AsNoTracking(), query);
        announcements = await ApplyCurrentAnnouncementCompoundAccessAsync(announcements, cancellationToken);

        return await ToPagedAnnouncementResultAsync(
            announcements,
            query,
            currentUserId: null,
            cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<AnnouncementResponse>>> SearchActiveAnnouncementsAsync(
        AnnouncementSearchQuery query,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<AnnouncementResponse>>.BadRequest("Current user is invalid.");
        }

        var allowedAudiences = await GetAllowedAnnouncementAudiencesAsync(
            currentUserId.Value,
            cancellationToken);
        var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
        var announcements = ApplyActiveAnnouncementScope(
            ApplyAnnouncementFilters(dbContext.Announcements.AsNoTracking(), query),
            allowedAudiences,
            residentCompoundIds,
            DateTime.UtcNow);

        return ServiceResult<PagedResult<AnnouncementResponse>>.Success(
            await ToPagedAnnouncementResultAsync(
                announcements,
                query,
                currentUserId,
                cancellationToken));
    }

    public async Task<ServiceResult<AnnouncementResponse>> GetAnnouncementAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        var announcements = dbContext.Announcements
            .AsNoTracking()
            .Where(announcement => announcement.Id == id);

        if (isManager)
        {
            announcements = await ApplyCurrentAnnouncementCompoundAccessAsync(announcements, cancellationToken);
        }
        else
        {
            if (!currentUserId.HasValue)
            {
                return ServiceResult<AnnouncementResponse>.BadRequest("Current user is invalid.");
            }

            var allowedAudiences = await GetAllowedAnnouncementAudiencesAsync(
                currentUserId.Value,
                cancellationToken);
            var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
            announcements = ApplyActiveAnnouncementScope(
                announcements,
                allowedAudiences,
                residentCompoundIds,
                DateTime.UtcNow);
        }

        var response = await announcements
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
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.")
            : ServiceResult<AnnouncementResponse>.Success(response);
    }

    public async Task<ServiceResult<AnnouncementResponse>> CreateAnnouncementAsync(
        Guid? currentUserId,
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateAnnouncementRequest(
            request.Title,
            request.Body,
            request.ExpiresAt);
        if (validation is not null)
        {
            return ToResult<AnnouncementResponse>(validation);
        }

        var compoundResult = await ResolveCompoundIdAsync(request.CompoundId, cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<AnnouncementResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Announcement compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<AnnouncementResponse>.Forbidden("Current user cannot access this compound.");
        }

        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Category = request.Category,
            Priority = request.Priority,
            Audience = request.Audience,
            CompoundId = compoundResult.Value,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = currentUserId,
            IsPinned = request.IsPinned
        };

        dbContext.Announcements.Add(announcement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnnouncementResponse>.Success(
            await ToAnnouncementResponseAsync(announcement, currentUserId, cancellationToken));
    }

    public async Task<ServiceResult<AnnouncementResponse>> UpdateAnnouncementAsync(
        Guid id,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        var announcement = await dbContext.Announcements
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (announcement is null)
        {
            return ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.");
        }

        if (!await CanAccessCompoundAsync(announcement.CompoundId, cancellationToken))
        {
            return ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.");
        }

        var validation = ValidateAnnouncementRequest(
            request.Title,
            request.Body,
            request.ExpiresAt);
        if (validation is not null)
        {
            return ToResult<AnnouncementResponse>(validation);
        }

        var compoundResult = await ResolveCompoundIdAsync(request.CompoundId, cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<AnnouncementResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Announcement compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<AnnouncementResponse>.Forbidden("Current user cannot access this compound.");
        }

        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body.Trim();
        announcement.Category = request.Category;
        announcement.Priority = request.Priority;
        announcement.Audience = request.Audience;
        announcement.CompoundId = compoundResult.Value;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.IsPinned = request.IsPinned;
        announcement.IsActive = request.IsActive;
        announcement.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnnouncementResponse>.Success(
            await ToAnnouncementResponseAsync(announcement, currentUserId: null, cancellationToken));
    }

    public async Task<ServiceResult<AnnouncementResponse>> PublishAnnouncementAsync(
        Guid id,
        PublishAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        var announcement = await dbContext.Announcements
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (announcement is null)
        {
            return ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.");
        }

        if (!await CanAccessCompoundAsync(announcement.CompoundId, cancellationToken))
        {
            return ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.");
        }

        if (!announcement.IsActive)
        {
            return ServiceResult<AnnouncementResponse>.BadRequest("Inactive announcements cannot be published.");
        }

        if (announcement.Status == AnnouncementStatus.Archived)
        {
            return ServiceResult<AnnouncementResponse>.BadRequest("Archived announcements cannot be published.");
        }

        var wasAlreadyPublished = announcement.Status == AnnouncementStatus.Published
            && announcement.PublishedAt.HasValue;
        var publishedAt = request.PublishedAt ?? DateTime.UtcNow;
        var expiresAt = request.ExpiresAt ?? announcement.ExpiresAt;
        if (expiresAt.HasValue && expiresAt.Value <= publishedAt)
        {
            return ServiceResult<AnnouncementResponse>.BadRequest("Announcement expiry must be after publish time.");
        }

        announcement.Status = AnnouncementStatus.Published;
        announcement.PublishedAt = publishedAt;
        announcement.ExpiresAt = expiresAt;
        announcement.UpdatedAt = DateTime.UtcNow;

        if (!wasAlreadyPublished)
        {
            await AddAnnouncementNotificationsAsync(announcement, publishedAt, cancellationToken);

            if (auditLogService is not null)
            {
                await auditLogService.AppendEntryAsync(new AuditLogRecord(
                    CompoundId: announcement.CompoundId,
                    ResidentProfileId: null,
                    ActorUserId: announcement.CreatedByUserId,
                    ActorRole: null,
                    ActionType: AuditActionType.AnnouncementPublished,
                    EntityType: AuditEntityType.Announcement,
                    EntityId: announcement.Id,
                    Severity: announcement.Priority == AnnouncementPriority.Critical ? AuditSeverity.High : AuditSeverity.Medium,
                    SourceModule: "Communication",
                    Description: "Announcement published and resident notifications queued."), cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnnouncementResponse>.Success(
            await ToAnnouncementResponseAsync(announcement, currentUserId: null, cancellationToken));
    }

    public async Task<ServiceResult<AnnouncementResponse>> ArchiveAnnouncementAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var announcement = await dbContext.Announcements
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (announcement is null)
        {
            return ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.");
        }

        if (!await CanAccessCompoundAsync(announcement.CompoundId, cancellationToken))
        {
            return ServiceResult<AnnouncementResponse>.NotFound("Announcement was not found.");
        }

        announcement.Status = AnnouncementStatus.Archived;
        announcement.IsActive = false;
        announcement.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnnouncementResponse>.Success(
            await ToAnnouncementResponseAsync(announcement, currentUserId: null, cancellationToken));
    }

    public async Task<ServiceResult<AnnouncementReadReceiptResponse>> MarkAnnouncementAsReadAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<AnnouncementReadReceiptResponse>.BadRequest("Current user is invalid.");
        }

        var allowedAudiences = await GetAllowedAnnouncementAudiencesAsync(
            currentUserId.Value,
            cancellationToken);
        var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
        var canRead = await ApplyActiveAnnouncementScope(
                dbContext.Announcements.AsNoTracking().Where(announcement => announcement.Id == id),
                allowedAudiences,
                residentCompoundIds,
                DateTime.UtcNow)
            .AnyAsync(cancellationToken);
        if (!canRead)
        {
            return ServiceResult<AnnouncementReadReceiptResponse>.NotFound("Announcement was not found.");
        }

        var existingReceipt = await dbContext.AnnouncementReadReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(receipt =>
                receipt.AnnouncementId == id && receipt.UserId == currentUserId.Value,
                cancellationToken);
        if (existingReceipt is not null)
        {
            return ServiceResult<AnnouncementReadReceiptResponse>.Success(
                ToReadReceiptResponse(existingReceipt));
        }

        var receipt = new AnnouncementReadReceipt
        {
            AnnouncementId = id,
            UserId = currentUserId.Value,
            ReadAt = DateTime.UtcNow
        };

        dbContext.AnnouncementReadReceipts.Add(receipt);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnnouncementReadReceiptResponse>.Success(
            ToReadReceiptResponse(receipt));
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

    private async Task AddAnnouncementNotificationsAsync(
        Announcement announcement,
        DateTime scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        if (announcement.Audience is AnnouncementAudience.Admins or AnnouncementAudience.Managers)
        {
            return;
        }

        var recipients = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record =>
                record.CompoundId == announcement.CompoundId
                && record.OccupancyStatus == OccupancyStatus.Active
                && record.ResidentProfile.IsActive)
            .Select(record => new
            {
                record.ResidentProfileId,
                record.ResidentProfile.UserId,
                record.ResidentProfile.FullName,
                record.ResidentProfile.PhoneNumber,
                record.OccupancyType
            })
            .ToListAsync(cancellationToken);

        var scopedRecipients = recipients
            .Where(recipient => announcement.Audience switch
            {
                AnnouncementAudience.Tenants => recipient.OccupancyType == OccupancyType.Tenant,
                AnnouncementAudience.Owners => recipient.OccupancyType is OccupancyType.OwnerCash or OccupancyType.OwnerInstallment,
                _ => true
            })
            .GroupBy(recipient => recipient.UserId)
            .Select(group => group.First())
            .ToArray();

        if (scopedRecipients.Length == 0)
        {
            return;
        }

        var userIds = scopedRecipients.Select(recipient => recipient.UserId).ToArray();
        var preferences = await dbContext.ResidentNotificationPreferences
            .Where(preference => userIds.Contains(preference.UserId))
            .ToDictionaryAsync(preference => preference.UserId, cancellationToken);

        var notificationPriority = announcement.Priority switch
        {
            AnnouncementPriority.Critical => NotificationPriority.Urgent,
            AnnouncementPriority.High => NotificationPriority.High,
            AnnouncementPriority.Low => NotificationPriority.Low,
            _ => NotificationPriority.Normal
        };
        var severity = announcement.Category == AnnouncementCategory.Emergency
            || announcement.Priority == AnnouncementPriority.Critical
                ? ResidentNotificationSeverity.Critical
                : announcement.Priority == AnnouncementPriority.High
                    ? ResidentNotificationSeverity.Warning
                    : ResidentNotificationSeverity.Info;

        foreach (var recipient in scopedRecipients)
        {
            preferences.TryGetValue(recipient.UserId, out var preference);
            var suppressionReason = ResidentNotificationPreferencePolicy.GetSuppressionReason(
                preference,
                ResidentNotificationType.Announcement,
                severity,
                notificationPriority,
                scheduledAtUtc);
            if (suppressionReason is not null)
            {
                continue;
            }

            dbContext.ResidentNotifications.Add(new ResidentNotification
            {
                UserId = recipient.UserId,
                Title = announcement.Title,
                Message = announcement.Body,
                Type = ResidentNotificationType.Announcement,
                Severity = severity,
                RelatedEntityType = nameof(Announcement),
                RelatedEntityId = announcement.Id,
                CreatedAt = scheduledAtUtc
            });

            dbContext.NotificationOutboxes.Add(new NotificationOutbox
            {
                CompoundId = announcement.CompoundId,
                ResidentProfileId = recipient.ResidentProfileId,
                RecipientUserId = recipient.UserId,
                Channel = NotificationChannel.InApp,
                EventType = NotificationEventType.AnnouncementPublished,
                Priority = notificationPriority,
                RecipientName = recipient.FullName,
                RecipientPhoneNumber = recipient.PhoneNumber,
                Subject = announcement.Title,
                Body = announcement.Body,
                RelatedEntityType = NotificationRelatedEntityType.Announcement,
                RelatedEntityId = announcement.Id,
                ScheduledAtUtc = scheduledAtUtc,
                CreatedByUserId = announcement.CreatedByUserId
            });
        }
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
