using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentCommunicationOperationsService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null,
    IAuditLogService? auditLogService = null)
    : IResidentCommunicationOperationsService
{
    private const int MaxTitleLength = 150;
    private const int MaxDescriptionLength = 4000;
    private const int MaxUpdateMessageLength = 2000;

    public async Task<ServiceResult<ResidentCommunicationOperationsSummaryResponse>> GetAdminSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<ResidentCommunicationOperationsSummaryResponse>.Forbidden("Current user cannot access resident communications.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<ResidentCommunicationOperationsSummaryResponse>.NotFound("Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var announcements = dbContext.Announcements.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        var outages = dbContext.UtilityOutages.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        var outbox = dbContext.NotificationOutboxes.AsNoTracking();

        if (compoundId.HasValue)
        {
            announcements = announcements.Where(item => item.CompoundId == compoundId.Value);
            outages = outages.Where(item => item.CompoundId == compoundId.Value);
            outbox = outbox.Where(item => item.CompoundId == compoundId.Value);
        }
        else if (!scope.IsSuperAdmin)
        {
            outbox = outbox.Where(item => item.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(item.CompoundId.Value));
        }

        var activeAnnouncements = await announcements.CountAsync(item =>
            item.IsActive
            && item.Status == AnnouncementStatus.Published
            && (!item.PublishedAt.HasValue || item.PublishedAt.Value <= now)
            && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now), cancellationToken);

        return ServiceResult<ResidentCommunicationOperationsSummaryResponse>.Success(new ResidentCommunicationOperationsSummaryResponse(
            activeAnnouncements,
            await outages.CountAsync(IsOpenOutageExpression(), cancellationToken),
            await outages.CountAsync(item => item.Severity == UtilityOutageSeverity.Critical
                && (item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active), cancellationToken),
            await outages.CountAsync(item => item.Status == UtilityOutageStatus.Planned, cancellationToken),
            await outages.CountAsync(item => item.Status == UtilityOutageStatus.Resolved, cancellationToken),
            0,
            await outbox.CountAsync(item => item.Status == NotificationStatus.Pending, cancellationToken)));
    }

    public async Task<ServiceResult<UtilityOutageDetailsResponse>> CreateUtilityOutageAsync(
        Guid? currentUserId,
        CreateUtilityOutageRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = TrimOrNull(request.Title);
        var description = TrimOrNull(request.Description);
        if (title is null)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Outage title is required.");
        }

        if (description is null)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Outage description is required.");
        }

        if (title.Length > MaxTitleLength || description.Length > MaxDescriptionLength)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Outage text exceeds the allowed length.");
        }

        var validation = await ValidateOutageScopeAsync(
            request.CompoundId,
            request.AffectedScope,
            request.BuildingId,
            request.FloorId,
            request.PropertyUnitId,
            cancellationToken);
        if (!validation.IsSuccess)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest(validation.Message ?? "Outage scope is invalid.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<UtilityOutageDetailsResponse>.NotFound("Compound was not found.");
        }

        var start = request.EstimatedStartAtUtc ?? DateTime.UtcNow;
        if (request.EstimatedEndAtUtc.HasValue && request.EstimatedEndAtUtc.Value <= start)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Estimated outage end time must be after the start time.");
        }

        if (request.Status is UtilityOutageStatus.Resolved or UtilityOutageStatus.Cancelled)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("New outages must start as Planned or Active.");
        }

        var outage = new UtilityOutage
        {
            CompoundId = request.CompoundId,
            BuildingId = request.AffectedScope is UtilityOutageAffectedScope.Building or UtilityOutageAffectedScope.Floor or UtilityOutageAffectedScope.Unit
                ? request.BuildingId
                : null,
            FloorId = request.AffectedScope is UtilityOutageAffectedScope.Floor or UtilityOutageAffectedScope.Unit
                ? request.FloorId
                : null,
            PropertyUnitId = request.AffectedScope == UtilityOutageAffectedScope.Unit
                ? request.PropertyUnitId
                : null,
            ServiceType = request.ServiceType,
            AffectedScope = request.AffectedScope,
            Status = request.Status,
            Severity = request.Severity,
            Title = title,
            Description = description,
            EstimatedStartAtUtc = start,
            EstimatedEndAtUtc = request.EstimatedEndAtUtc,
            CreatedByUserId = currentUserId,
            NotifyResidents = request.NotifyResidents,
            PublishedAtUtc = request.PublishAnnouncement ? DateTime.UtcNow : null
        };

        dbContext.UtilityOutages.Add(outage);

        if (request.PublishAnnouncement)
        {
            var announcement = new Announcement
            {
                CompoundId = request.CompoundId,
                Title = title,
                Body = description,
                Category = AnnouncementCategory.Utility,
                Priority = MapAnnouncementPriority(request.Severity),
                Audience = AnnouncementAudience.AllResidents,
                Status = AnnouncementStatus.Published,
                PublishedAt = DateTime.UtcNow,
                ExpiresAt = request.EstimatedEndAtUtc,
                CreatedByUserId = currentUserId,
                IsPinned = request.Severity is UtilityOutageSeverity.High or UtilityOutageSeverity.Critical,
                IsActive = true
            };
            dbContext.Announcements.Add(announcement);
            outage.AnnouncementId = announcement.Id;
        }

        if (request.NotifyResidents)
        {
            var recipients = await GetAffectedResidentRecipientsAsync(outage, cancellationToken);
            var outboxItemCount = await AddResidentNotificationsAsync(
                outage,
                recipients,
                currentUserId,
                title,
                description,
                MapNotificationPriority(outage.Severity),
                cancellationToken);
            outage.RecipientCount = recipients.Count;
            outage.OutboxItemCount = outboxItemCount;
        }

        if (auditLogService is not null)
        {
            await auditLogService.AppendEntryAsync(new AuditLogRecord(
                CompoundId: outage.CompoundId,
                ResidentProfileId: null,
                ActorUserId: currentUserId,
                ActorRole: null,
                ActionType: AuditActionType.UtilityOutagePublished,
                EntityType: AuditEntityType.UtilityOutage,
                EntityId: outage.Id,
                Severity: outage.Severity == UtilityOutageSeverity.Critical ? AuditSeverity.High : AuditSeverity.Medium,
                SourceModule: "Communication",
                Description: "Utility outage created and resident communication scope evaluated."), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UtilityOutageDetailsResponse>.Success(await ToDetailsAsync(outage.Id, cancellationToken));
    }

    public async Task<PagedResult<UtilityOutageResponse>> SearchUtilityOutagesAsync(
        UtilityOutageQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var outages = ApplyOutageFilters(dbContext.UtilityOutages.AsNoTracking(), query)
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        return await ToPagedOutageResultAsync(outages, query, cancellationToken);
    }

    public async Task<ServiceResult<UtilityOutageDetailsResponse>> GetUtilityOutageAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var exists = await dbContext.UtilityOutages.AsNoTracking()
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .AnyAsync(item => item.Id == id, cancellationToken);
        return exists
            ? ServiceResult<UtilityOutageDetailsResponse>.Success(await ToDetailsAsync(id, cancellationToken))
            : ServiceResult<UtilityOutageDetailsResponse>.NotFound("Utility outage was not found.");
    }

    public async Task<ServiceResult<UtilityOutageDetailsResponse>> PublishUtilityOutageUpdateAsync(
        Guid id,
        Guid? currentUserId,
        PublishUtilityOutageUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var outage = await GetEditableOutageAsync(id, cancellationToken);
        if (outage is null)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.NotFound("Utility outage was not found.");
        }

        if (outage.Status is UtilityOutageStatus.Resolved or UtilityOutageStatus.Cancelled)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Closed outages cannot receive updates.");
        }

        var message = TrimOrNull(request.Message);
        if (message is null)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Outage update message is required.");
        }

        if (message.Length > MaxUpdateMessageLength)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Outage update message is too long.");
        }

        if (request.NewEstimatedEndAtUtc.HasValue && request.NewEstimatedEndAtUtc.Value <= outage.EstimatedStartAtUtc)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("New estimated end time must be after the outage start time.");
        }

        var now = DateTime.UtcNow;
        outage.EstimatedEndAtUtc = request.NewEstimatedEndAtUtc ?? outage.EstimatedEndAtUtc;
        outage.UpdatedAtUtc = now;
        dbContext.UtilityOutageUpdates.Add(new UtilityOutageUpdate
        {
            UtilityOutageId = outage.Id,
            CreatedByUserId = currentUserId,
            UpdateType = request.UpdateType,
            Message = message,
            NewEstimatedEndAtUtc = request.NewEstimatedEndAtUtc,
            CreatedAtUtc = now
        });

        if (request.NotifyResidents)
        {
            var recipients = await GetAffectedResidentRecipientsAsync(outage, cancellationToken);
            var outboxItemCount = await AddResidentNotificationsAsync(
                outage,
                recipients,
                currentUserId,
                outage.Title,
                message,
                MapNotificationPriority(outage.Severity),
                cancellationToken);
            outage.RecipientCount = Math.Max(outage.RecipientCount, recipients.Count);
            outage.OutboxItemCount += outboxItemCount;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UtilityOutageDetailsResponse>.Success(await ToDetailsAsync(outage.Id, cancellationToken));
    }

    public async Task<ServiceResult<UtilityOutageDetailsResponse>> ResolveUtilityOutageAsync(
        Guid id,
        Guid? currentUserId,
        ResolveUtilityOutageRequest request,
        CancellationToken cancellationToken = default)
    {
        var outage = await GetEditableOutageAsync(id, cancellationToken);
        if (outage is null)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.NotFound("Utility outage was not found.");
        }

        if (outage.Status == UtilityOutageStatus.Resolved)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Utility outage is already resolved.");
        }

        if (outage.Status == UtilityOutageStatus.Cancelled)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Cancelled outages cannot be resolved.");
        }

        var notes = TrimOrNull(request.ResolutionNotes);
        var now = DateTime.UtcNow;
        outage.Status = UtilityOutageStatus.Resolved;
        outage.ResolvedAtUtc = now;
        outage.ResolvedByUserId = currentUserId;
        outage.ResolutionNotes = notes;
        outage.UpdatedAtUtc = now;
        dbContext.UtilityOutageUpdates.Add(new UtilityOutageUpdate
        {
            UtilityOutageId = outage.Id,
            CreatedByUserId = currentUserId,
            UpdateType = UtilityOutageUpdateType.Resolved,
            Message = notes ?? "Utility outage resolved.",
            CreatedAtUtc = now
        });

        if (request.NotifyResidents)
        {
            var recipients = await GetAffectedResidentRecipientsAsync(outage, cancellationToken);
            var outboxItemCount = await AddResidentNotificationsAsync(
                outage,
                recipients,
                currentUserId,
                outage.Title,
                notes ?? "Utility outage resolved.",
                NotificationPriority.Normal,
                cancellationToken);
            outage.OutboxItemCount += outboxItemCount;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UtilityOutageDetailsResponse>.Success(await ToDetailsAsync(outage.Id, cancellationToken));
    }

    public async Task<ServiceResult<UtilityOutageDetailsResponse>> CancelUtilityOutageAsync(
        Guid id,
        Guid? currentUserId,
        CancelUtilityOutageRequest request,
        CancellationToken cancellationToken = default)
    {
        var outage = await GetEditableOutageAsync(id, cancellationToken);
        if (outage is null)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.NotFound("Utility outage was not found.");
        }

        if (outage.Status == UtilityOutageStatus.Cancelled)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Utility outage is already cancelled.");
        }

        if (outage.Status == UtilityOutageStatus.Resolved)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Resolved outages cannot be cancelled.");
        }

        var reason = TrimOrNull(request.Reason) ?? "Utility outage cancelled.";
        outage.Status = UtilityOutageStatus.Cancelled;
        outage.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.UtilityOutageUpdates.Add(new UtilityOutageUpdate
        {
            UtilityOutageId = outage.Id,
            CreatedByUserId = currentUserId,
            UpdateType = UtilityOutageUpdateType.Cancelled,
            Message = reason,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (request.NotifyResidents)
        {
            var recipients = await GetAffectedResidentRecipientsAsync(outage, cancellationToken);
            var outboxItemCount = await AddResidentNotificationsAsync(
                outage,
                recipients,
                currentUserId,
                outage.Title,
                reason,
                NotificationPriority.Normal,
                cancellationToken);
            outage.OutboxItemCount += outboxItemCount;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UtilityOutageDetailsResponse>.Success(await ToDetailsAsync(outage.Id, cancellationToken));
    }



    public async Task<ServiceResult<CommunicationCommandCenterResponse>> GetCommunicationCommandCenterAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateCommunicationOperationsScopeAsync(compoundId, cancellationToken);
        if (!scopeValidation.IsSuccess)
        {
            return ServiceResult<CommunicationCommandCenterResponse>.NotFound(scopeValidation.Message ?? "Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var scope = scopeValidation.Value!;
        var announcements = ApplyAnnouncementScope(dbContext.Announcements.AsNoTracking(), scope, compoundId);
        var outages = ApplyOutageScope(dbContext.UtilityOutages.AsNoTracking(), scope, compoundId);
        var outbox = ApplyOutboxScope(dbContext.NotificationOutboxes.AsNoTracking(), scope, compoundId);
        var conversations = ApplyConversationScope(dbContext.Conversations.AsNoTracking(), scope, compoundId);

        var activeAnnouncementCount = await announcements.CountAsync(item =>
            item.IsActive
            && item.Status == AnnouncementStatus.Published
            && (!item.PublishedAt.HasValue || item.PublishedAt.Value <= now)
            && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now), cancellationToken);
        var criticalAnnouncementCount = await announcements.CountAsync(item =>
            item.IsActive
            && item.Status == AnnouncementStatus.Published
            && (!item.PublishedAt.HasValue || item.PublishedAt.Value <= now)
            && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now)
            && item.Priority == AnnouncementPriority.Critical, cancellationToken);
        var acknowledgementBoard = await BuildAnnouncementAcknowledgementBoardAsync(scope, compoundId, now, cancellationToken);
        var activeOutageCount = await outages.CountAsync(IsOpenOutageExpression(), cancellationToken);
        var criticalOutageCount = await outages.CountAsync(item =>
            (item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active)
            && item.Severity == UtilityOutageSeverity.Critical, cancellationToken);
        var overdueOutageCount = await outages.CountAsync(item =>
            (item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active)
            && item.EstimatedEndAtUtc.HasValue
            && item.EstimatedEndAtUtc.Value < now, cancellationToken);
        var pendingOutboxItemCount = await outbox.CountAsync(item => item.Status == NotificationStatus.Pending, cancellationToken);
        var failedOutboxItemCount = await outbox.CountAsync(item => item.Status == NotificationStatus.Failed, cancellationToken);
        var openConversationCount = await conversations.CountAsync(IsOpenConversationExpression(), cancellationToken);
        var urgentConversationCount = await conversations.CountAsync(item =>
            (item.Status == ConversationStatus.Open
                || item.Status == ConversationStatus.PendingAdminReply
                || item.Status == ConversationStatus.PendingResidentReply
                || item.Status == ConversationStatus.Reopened)
            && item.Priority == ConversationPriority.Urgent, cancellationToken);
        var escalatedConversationCount = await conversations.CountAsync(item =>
            (item.Status == ConversationStatus.Open
                || item.Status == ConversationStatus.PendingAdminReply
                || item.Status == ConversationStatus.PendingResidentReply
                || item.Status == ConversationStatus.Reopened)
            && item.EscalationLevel != ConversationEscalationLevel.None, cancellationToken);
        var unassignedConversationCount = await conversations.CountAsync(item =>
            (item.Status == ConversationStatus.Open
                || item.Status == ConversationStatus.PendingAdminReply
                || item.Status == ConversationStatus.PendingResidentReply
                || item.Status == ConversationStatus.Reopened)
            && !item.AssignedToUserId.HasValue, cancellationToken);

        var criticalActionCount = criticalAnnouncementCount
            + criticalOutageCount
            + overdueOutageCount
            + failedOutboxItemCount
            + urgentConversationCount
            + escalatedConversationCount;
        var recommendedActions = BuildCommunicationCommandCenterActions(
            acknowledgementBoard.TotalMissingAcknowledgementCount,
            criticalOutageCount,
            overdueOutageCount,
            failedOutboxItemCount,
            urgentConversationCount,
            escalatedConversationCount,
            unassignedConversationCount);

        return ServiceResult<CommunicationCommandCenterResponse>.Success(new CommunicationCommandCenterResponse(
            compoundId,
            now,
            activeAnnouncementCount,
            criticalAnnouncementCount,
            acknowledgementBoard.TotalMissingAcknowledgementCount,
            activeOutageCount,
            criticalOutageCount,
            overdueOutageCount,
            pendingOutboxItemCount,
            failedOutboxItemCount,
            openConversationCount,
            urgentConversationCount,
            escalatedConversationCount,
            unassignedConversationCount,
            DetermineCommunicationOverallRisk(criticalActionCount, failedOutboxItemCount, overdueOutageCount),
            criticalActionCount,
            recommendedActions));
    }

    public async Task<ServiceResult<AnnouncementAcknowledgementBoardResponse>> GetAnnouncementAcknowledgementBoardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateCommunicationOperationsScopeAsync(compoundId, cancellationToken);
        if (!scopeValidation.IsSuccess)
        {
            return ServiceResult<AnnouncementAcknowledgementBoardResponse>.NotFound(scopeValidation.Message ?? "Compound was not found.");
        }

        var now = DateTime.UtcNow;
        return ServiceResult<AnnouncementAcknowledgementBoardResponse>.Success(
            await BuildAnnouncementAcknowledgementBoardAsync(scopeValidation.Value!, compoundId, now, cancellationToken));
    }

    public async Task<ServiceResult<UtilityOutageOperationsBoardResponse>> GetUtilityOutageOperationsBoardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateCommunicationOperationsScopeAsync(compoundId, cancellationToken);
        if (!scopeValidation.IsSuccess)
        {
            return ServiceResult<UtilityOutageOperationsBoardResponse>.NotFound(scopeValidation.Message ?? "Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var scope = scopeValidation.Value!;
        var openOutages = await ApplyOutageScope(dbContext.UtilityOutages.AsNoTracking(), scope, compoundId)
            .Where(item => item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active)
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.ServiceType,
                item.AffectedScope,
                item.Status,
                item.Severity,
                item.Title,
                item.EstimatedStartAtUtc,
                item.EstimatedEndAtUtc,
                item.RecipientCount,
                item.OutboxItemCount,
                UpdateCount = item.Updates.Count
            })
            .ToArrayAsync(cancellationToken);

        var items = openOutages
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EstimatedEndAtUtc ?? DateTime.MaxValue)
            .ThenBy(item => item.EstimatedStartAtUtc)
            .Take(50)
            .Select(item =>
            {
                var elapsedMinutes = Math.Max(0, (int)Math.Round((now - item.EstimatedStartAtUtc).TotalMinutes));
                var isOverdue = item.EstimatedEndAtUtc.HasValue && item.EstimatedEndAtUtc.Value < now;
                return new UtilityOutageOperationsItemResponse(
                    item.Id,
                    item.CompoundId,
                    item.ServiceType,
                    item.AffectedScope,
                    item.Status,
                    item.Severity,
                    item.Title,
                    item.EstimatedStartAtUtc,
                    item.EstimatedEndAtUtc,
                    elapsedMinutes,
                    isOverdue,
                    item.UpdateCount,
                    item.RecipientCount,
                    item.OutboxItemCount,
                    DetermineOutageOperationalRisk(item.Severity, isOverdue, item.UpdateCount),
                    DetermineOutageRecommendedAction(item.Severity, isOverdue, item.UpdateCount));
            })
            .ToArray();

        return ServiceResult<UtilityOutageOperationsBoardResponse>.Success(new UtilityOutageOperationsBoardResponse(
            compoundId,
            now,
            openOutages.Length,
            openOutages.Count(item => item.Severity == UtilityOutageSeverity.Critical),
            openOutages.Count(item => item.EstimatedEndAtUtc.HasValue && item.EstimatedEndAtUtc.Value < now),
            openOutages.Sum(item => item.UpdateCount),
            openOutages.Sum(item => item.OutboxItemCount),
            items));
    }

    public async Task<ServiceResult<ResidentCommunicationImpactReportResponse>> GetResidentCommunicationImpactReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateCommunicationOperationsScopeAsync(compoundId, cancellationToken);
        if (!scopeValidation.IsSuccess)
        {
            return ServiceResult<ResidentCommunicationImpactReportResponse>.NotFound(scopeValidation.Message ?? "Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var scope = scopeValidation.Value!;
        var activeOutages = await ApplyOutageScope(dbContext.UtilityOutages.AsNoTracking(), scope, compoundId)
            .Where(item => item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active)
            .Select(item => new
            {
                item.CompoundId,
                item.BuildingId,
                item.FloorId,
                item.PropertyUnitId,
                item.AffectedScope,
                item.Severity
            })
            .ToArrayAsync(cancellationToken);

        var occupancies = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.OccupancyStatus == OccupancyStatus.Active && record.ResidentProfile.IsActive)
            .Where(record => !compoundId.HasValue || record.CompoundId == compoundId.Value)
            .ApplyCompoundAccess(scope, record => record.CompoundId)
            .Select(record => new
            {
                record.ResidentProfileId,
                record.ResidentProfile.UserId,
                record.CompoundId,
                record.PropertyUnitId,
                record.PropertyUnit.BuildingId,
                record.PropertyUnit.FloorId,
                record.ResidentProfile.FullName
            })
            .ToArrayAsync(cancellationToken);

        var userIds = occupancies.Select(item => item.UserId).Distinct().ToArray();
        var unreadNotifications = await dbContext.ResidentNotifications.AsNoTracking()
            .Where(item => !item.IsRead && userIds.Contains(item.UserId))
            .GroupBy(item => item.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var pendingOutboxItems = await ApplyOutboxScope(dbContext.NotificationOutboxes.AsNoTracking(), scope, compoundId)
            .Where(item => item.Status == NotificationStatus.Pending && item.ResidentProfileId.HasValue)
            .GroupBy(item => item.ResidentProfileId!.Value)
            .Select(group => new { ResidentProfileId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        var unreadByUserId = unreadNotifications.ToDictionary(item => item.UserId, item => item.Count);
        var pendingByResidentId = pendingOutboxItems.ToDictionary(item => item.ResidentProfileId, item => item.Count);

        var items = occupancies
            .Select(occupancy =>
            {
                var affectedOutages = activeOutages.Where(outage => IsResidentAffectedByOutage(
                    outage.CompoundId,
                    outage.AffectedScope,
                    outage.BuildingId,
                    outage.FloorId,
                    outage.PropertyUnitId,
                    occupancy.CompoundId,
                    occupancy.BuildingId,
                    occupancy.FloorId,
                    occupancy.PropertyUnitId)).ToArray();
                var criticalOutageCount = affectedOutages.Count(item => item.Severity == UtilityOutageSeverity.Critical);
                var unreadCount = unreadByUserId.GetValueOrDefault(occupancy.UserId);
                var pendingCount = pendingByResidentId.GetValueOrDefault(occupancy.ResidentProfileId);
                return new ResidentCommunicationImpactItemResponse(
                    occupancy.ResidentProfileId,
                    occupancy.UserId,
                    occupancy.CompoundId,
                    occupancy.FullName,
                    affectedOutages.Length,
                    criticalOutageCount,
                    unreadCount,
                    pendingCount,
                    DetermineResidentCommunicationImpact(affectedOutages.Length, criticalOutageCount, unreadCount, pendingCount),
                    DetermineResidentCommunicationAction(affectedOutages.Length, criticalOutageCount, unreadCount, pendingCount));
            })
            .Where(item => item.ActiveOutageCount > 0 || item.UnreadNotificationCount > 0 || item.PendingOutboxItemCount > 0)
            .OrderByDescending(item => item.ImpactLevel == "Critical")
            .ThenByDescending(item => item.CriticalOutageCount)
            .ThenByDescending(item => item.UnreadNotificationCount)
            .Take(100)
            .ToArray();

        return ServiceResult<ResidentCommunicationImpactReportResponse>.Success(new ResidentCommunicationImpactReportResponse(
            compoundId,
            now,
            items.Length,
            items.Count(item => item.ImpactLevel == "Critical"),
            items.Sum(item => item.UnreadNotificationCount),
            items));
    }

    public async Task<ServiceResult<CommunicationResponseIntelligenceResponse>> GetCommunicationResponseIntelligenceAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateCommunicationOperationsScopeAsync(compoundId, cancellationToken);
        if (!scopeValidation.IsSuccess)
        {
            return ServiceResult<CommunicationResponseIntelligenceResponse>.NotFound(scopeValidation.Message ?? "Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var conversations = await ApplyConversationScope(dbContext.Conversations.AsNoTracking(), scopeValidation.Value!, compoundId)
            .Where(item => item.Status == ConversationStatus.Open
                || item.Status == ConversationStatus.PendingAdminReply
                || item.Status == ConversationStatus.PendingResidentReply
                || item.Status == ConversationStatus.Reopened)
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.ResidentProfileId,
                item.PropertyUnitId,
                item.Status,
                item.Priority,
                item.Topic,
                item.EscalationLevel,
                item.AssignedToUserId,
                item.CreatedAtUtc,
                item.LastMessageAtUtc,
                item.LastResidentMessageAtUtc,
                item.LastAdminMessageAtUtc
            })
            .ToArrayAsync(cancellationToken);

        var staleItems = conversations
            .Select(item =>
            {
                var ageHours = Math.Max(0, (int)Math.Round((now - item.CreatedAtUtc).TotalHours));
                var lastMessageHours = Math.Max(0, (int)Math.Round((now - item.LastMessageAtUtc).TotalHours));
                var isStale = IsConversationStale(item.Status, item.Priority, item.EscalationLevel, lastMessageHours);
                return new
                {
                    Item = item,
                    AgeHours = ageHours,
                    LastMessageHours = lastMessageHours,
                    IsStale = isStale
                };
            })
            .Where(item => item.IsStale)
            .OrderByDescending(item => item.Item.Priority)
            .ThenByDescending(item => item.LastMessageHours)
            .Take(25)
            .Select(item => new CommunicationSlaConversationItemResponse(
                item.Item.Id,
                item.Item.CompoundId,
                item.Item.ResidentProfileId,
                item.Item.PropertyUnitId,
                item.Item.Status,
                item.Item.Priority,
                item.Item.Topic,
                item.Item.EscalationLevel,
                item.Item.AssignedToUserId,
                item.AgeHours,
                item.LastMessageHours,
                DetermineConversationSlaRisk(item.Item.Priority, item.Item.EscalationLevel, item.LastMessageHours),
                DetermineConversationSlaAction(item.Item.Status, item.Item.AssignedToUserId, item.LastMessageHours)))
            .ToArray();

        var averageOpenAgeHours = conversations.Length == 0
            ? 0m
            : Math.Round((decimal)conversations.Average(item => Math.Max(0, (now - item.CreatedAtUtc).TotalHours)), 2);

        return ServiceResult<CommunicationResponseIntelligenceResponse>.Success(new CommunicationResponseIntelligenceResponse(
            compoundId,
            now,
            conversations.Length,
            conversations.Count(item => item.Status == ConversationStatus.PendingAdminReply),
            conversations.Count(item => item.Priority == ConversationPriority.Urgent),
            conversations.Count(item => item.EscalationLevel != ConversationEscalationLevel.None),
            staleItems.Length,
            averageOpenAgeHours,
            staleItems));
    }

    public async Task<ServiceResult<CommunicationRiskDashboardResponse>> GetCommunicationRiskDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var commandCenterResult = await GetCommunicationCommandCenterAsync(compoundId, cancellationToken);
        if (!commandCenterResult.IsSuccess)
        {
            return ServiceResult<CommunicationRiskDashboardResponse>.NotFound(commandCenterResult.Message ?? "Compound was not found.");
        }

        var command = commandCenterResult.Value!;
        var signals = new List<CommunicationRiskSignalResponse>();
        if (command.CriticalOutageCount > 0)
        {
            signals.Add(new CommunicationRiskSignalResponse(
                "UtilityOutages",
                "Critical",
                "Critical utility outage active",
                "One or more critical utility outages are still active or planned.",
                "Keep the outage board updated and publish resident-facing progress updates.",
                command.CriticalOutageCount));
        }

        if (command.OverdueOutageCount > 0)
        {
            signals.Add(new CommunicationRiskSignalResponse(
                "UtilityOutages",
                "Critical",
                "Outage SLA is overdue",
                "Open utility outages have passed their estimated restoration time.",
                "Escalate to operations and publish a revised estimated restoration update.",
                command.OverdueOutageCount));
        }

        if (command.CriticalAnnouncementCount > 0 && command.AnnouncementAcknowledgementGapCount > 0)
        {
            signals.Add(new CommunicationRiskSignalResponse(
                "Announcements",
                "Warning",
                "Critical announcements need acknowledgement follow-up",
                "Critical announcements still have missing resident acknowledgement/read coverage.",
                "Send a follow-up broadcast and call residents in the affected scope if needed.",
                command.AnnouncementAcknowledgementGapCount));
        }

        if (command.FailedOutboxItemCount > 0)
        {
            signals.Add(new CommunicationRiskSignalResponse(
                "NotificationDelivery",
                "Critical",
                "Notification delivery failures detected",
                "Some communication outbox items have failed delivery.",
                "Review provider errors and retry high-priority messages.",
                command.FailedOutboxItemCount));
        }

        if (command.UrgentConversationCount > 0 || command.EscalatedConversationCount > 0)
        {
            signals.Add(new CommunicationRiskSignalResponse(
                "ResidentSupport",
                command.EscalatedConversationCount > 0 ? "Critical" : "Warning",
                "Urgent or escalated conversations require action",
                "Resident conversations include urgent or escalated cases.",
                "Assign accountable staff and close the oldest pending admin replies first.",
                command.UrgentConversationCount + command.EscalatedConversationCount));
        }

        if (command.UnassignedConversationCount > 0)
        {
            signals.Add(new CommunicationRiskSignalResponse(
                "ResidentSupport",
                "Warning",
                "Open conversations are unassigned",
                "Some open resident conversations do not have an accountable employee.",
                "Assign each open conversation to a responsible staff member.",
                command.UnassignedConversationCount));
        }

        return ServiceResult<CommunicationRiskDashboardResponse>.Success(new CommunicationRiskDashboardResponse(
            compoundId,
            command.GeneratedAtUtc,
            DetermineCommunicationOverallRisk(command.CriticalActionCount, command.FailedOutboxItemCount, command.OverdueOutageCount),
            signals.Count(item => item.Severity == "Critical"),
            signals.Count(item => item.Severity == "Warning"),
            signals));
    }

    public async Task<ServiceResult<ResidentCommunicationOperationsSummaryResponse>> GetResidentSummaryAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentCommunicationOperationsSummaryResponse>.BadRequest("Current user is invalid.");
        }

        var now = DateTime.UtcNow;
        var residentOutages = await GetResidentVisibleOutagesAsync(currentUserId.Value, dbContext.UtilityOutages.AsNoTracking(), cancellationToken);
        var compoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
        var announcements = dbContext.Announcements.AsNoTracking().Where(item => compoundIds.Contains(item.CompoundId));
        var activeAnnouncements = await announcements.CountAsync(item =>
            item.IsActive
            && item.Status == AnnouncementStatus.Published
            && (!item.PublishedAt.HasValue || item.PublishedAt.Value <= now)
            && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now), cancellationToken);
        var readAnnouncementIds = await dbContext.AnnouncementReadReceipts
            .AsNoTracking()
            .Where(item => item.UserId == currentUserId.Value)
            .Select(item => item.AnnouncementId)
            .ToArrayAsync(cancellationToken);
        var activeAnnouncementIds = await announcements
            .Where(item => item.IsActive
                && item.Status == AnnouncementStatus.Published
                && (!item.PublishedAt.HasValue || item.PublishedAt.Value <= now)
                && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return ServiceResult<ResidentCommunicationOperationsSummaryResponse>.Success(new ResidentCommunicationOperationsSummaryResponse(
            activeAnnouncements,
            await residentOutages.CountAsync(IsOpenOutageExpression(), cancellationToken),
            await residentOutages.CountAsync(item => item.Severity == UtilityOutageSeverity.Critical
                && (item.Status == UtilityOutageStatus.Active || item.Status == UtilityOutageStatus.Planned), cancellationToken),
            await residentOutages.CountAsync(item => item.Status == UtilityOutageStatus.Planned, cancellationToken),
            await residentOutages.CountAsync(item => item.Status == UtilityOutageStatus.Resolved, cancellationToken),
            activeAnnouncementIds.Count(id => !readAnnouncementIds.Contains(id)),
            0));
    }

    public async Task<ServiceResult<PagedResult<UtilityOutageResponse>>> SearchResidentUtilityOutagesAsync(
        Guid? currentUserId,
        UtilityOutageQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<UtilityOutageResponse>>.BadRequest("Current user is invalid.");
        }

        var outages = await GetResidentVisibleOutagesAsync(
            currentUserId.Value,
            ApplyOutageFilters(dbContext.UtilityOutages.AsNoTracking(), query),
            cancellationToken);

        return ServiceResult<PagedResult<UtilityOutageResponse>>.Success(
            await ToPagedOutageResultAsync(outages, query, cancellationToken));
    }

    public async Task<ServiceResult<UtilityOutageDetailsResponse>> GetResidentUtilityOutageAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UtilityOutageDetailsResponse>.BadRequest("Current user is invalid.");
        }

        var outages = await GetResidentVisibleOutagesAsync(
            currentUserId.Value,
            dbContext.UtilityOutages.AsNoTracking().Where(item => item.Id == id),
            cancellationToken);
        var canSee = await outages.AnyAsync(cancellationToken);
        return canSee
            ? ServiceResult<UtilityOutageDetailsResponse>.Success(await ToDetailsAsync(id, cancellationToken))
            : ServiceResult<UtilityOutageDetailsResponse>.NotFound("Utility outage was not found.");
    }



    private async Task<ServiceResult<CompoundAccessScope>> ValidateCommunicationOperationsScopeAsync(
        Guid? compoundId,
        CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<CompoundAccessScope>.Forbidden("Current user cannot access resident communications.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<CompoundAccessScope>.NotFound("Compound was not found.");
        }

        return ServiceResult<CompoundAccessScope>.Success(scope);
    }

    private static IQueryable<Announcement> ApplyAnnouncementScope(
        IQueryable<Announcement> query,
        CompoundAccessScope scope,
        Guid? compoundId)
    {
        query = query.ApplyCompoundAccess(scope, item => item.CompoundId);
        return compoundId.HasValue ? query.Where(item => item.CompoundId == compoundId.Value) : query;
    }

    private static IQueryable<UtilityOutage> ApplyOutageScope(
        IQueryable<UtilityOutage> query,
        CompoundAccessScope scope,
        Guid? compoundId)
    {
        query = query.ApplyCompoundAccess(scope, item => item.CompoundId);
        return compoundId.HasValue ? query.Where(item => item.CompoundId == compoundId.Value) : query;
    }

    private static IQueryable<NotificationOutbox> ApplyOutboxScope(
        IQueryable<NotificationOutbox> query,
        CompoundAccessScope scope,
        Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.CompoundId == compoundId.Value);
        }

        if (!scope.IsAuthenticated)
        {
            return query.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return scope.AllowedCompoundIds.Length == 0
            ? query.Where(_ => false)
            : query.Where(item => item.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(item.CompoundId.Value));
    }

    private static IQueryable<Conversation> ApplyConversationScope(
        IQueryable<Conversation> query,
        CompoundAccessScope scope,
        Guid? compoundId)
    {
        query = query.ApplyCompoundAccess(scope, item => item.CompoundId);
        return compoundId.HasValue ? query.Where(item => item.CompoundId == compoundId.Value) : query;
    }

    private async Task<AnnouncementAcknowledgementBoardResponse> BuildAnnouncementAcknowledgementBoardAsync(
        CompoundAccessScope scope,
        Guid? compoundId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeAnnouncements = await ApplyAnnouncementScope(dbContext.Announcements.AsNoTracking(), scope, compoundId)
            .Where(item => item.IsActive
                && item.Status == AnnouncementStatus.Published
                && (!item.PublishedAt.HasValue || item.PublishedAt.Value <= now)
                && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now))
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.Title,
                item.Priority,
                item.Category,
                item.PublishedAt,
                item.ExpiresAt,
                item.IsPinned
            })
            .ToArrayAsync(cancellationToken);

        var announcementIds = activeAnnouncements.Select(item => item.Id).ToArray();
        var readCounts = await dbContext.AnnouncementReadReceipts.AsNoTracking()
            .Where(item => announcementIds.Contains(item.AnnouncementId))
            .GroupBy(item => item.AnnouncementId)
            .Select(group => new { AnnouncementId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var readCountByAnnouncementId = readCounts.ToDictionary(item => item.AnnouncementId, item => item.Count);
        var compoundIds = activeAnnouncements.Select(item => item.CompoundId).Distinct().ToArray();
        var residentCounts = await dbContext.ResidentProfiles.AsNoTracking()
            .Where(item => item.IsActive && compoundIds.Contains(item.CompoundId))
            .GroupBy(item => item.CompoundId)
            .Select(group => new { CompoundId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var residentCountByCompoundId = residentCounts.ToDictionary(item => item.CompoundId, item => item.Count);

        var items = activeAnnouncements
            .Select(item =>
            {
                var expected = residentCountByCompoundId.GetValueOrDefault(item.CompoundId);
                var acknowledged = Math.Min(readCountByAnnouncementId.GetValueOrDefault(item.Id), expected);
                var missing = Math.Max(0, expected - acknowledged);
                var rate = expected == 0 ? 100m : Math.Round((decimal)acknowledged * 100m / expected, 2);
                return new AnnouncementAcknowledgementItemResponse(
                    item.Id,
                    item.CompoundId,
                    item.Title,
                    item.Priority,
                    item.Category,
                    item.PublishedAt,
                    item.ExpiresAt,
                    item.IsPinned,
                    expected,
                    acknowledged,
                    missing,
                    rate,
                    DetermineAnnouncementAcknowledgementRisk(item.Priority, missing, rate),
                    DetermineAnnouncementAcknowledgementAction(item.Priority, missing, rate));
            })
            .OrderByDescending(item => item.RiskLevel == "Critical")
            .ThenByDescending(item => item.RiskLevel == "Warning")
            .ThenByDescending(item => item.Priority)
            .ThenByDescending(item => item.MissingAcknowledgementCount)
            .Take(50)
            .ToArray();

        return new AnnouncementAcknowledgementBoardResponse(
            compoundId,
            now,
            activeAnnouncements.Length,
            activeAnnouncements.Count(item => item.Priority == AnnouncementPriority.Critical),
            items.Sum(item => item.ExpectedAcknowledgementCount),
            items.Sum(item => item.AcknowledgedCount),
            items.Sum(item => item.MissingAcknowledgementCount),
            items);
    }

    private static System.Linq.Expressions.Expression<Func<Conversation, bool>> IsOpenConversationExpression()
    {
        return item => item.Status == ConversationStatus.Open
            || item.Status == ConversationStatus.PendingAdminReply
            || item.Status == ConversationStatus.PendingResidentReply
            || item.Status == ConversationStatus.Reopened;
    }

    private static bool IsResidentAffectedByOutage(
        Guid outageCompoundId,
        UtilityOutageAffectedScope outageScope,
        Guid? outageBuildingId,
        Guid? outageFloorId,
        Guid? outagePropertyUnitId,
        Guid residentCompoundId,
        Guid? residentBuildingId,
        Guid? residentFloorId,
        Guid residentPropertyUnitId)
    {
        return outageCompoundId == residentCompoundId
            && (outageScope == UtilityOutageAffectedScope.Compound
                || (outageScope == UtilityOutageAffectedScope.Building && outageBuildingId.HasValue && residentBuildingId == outageBuildingId.Value)
                || (outageScope == UtilityOutageAffectedScope.Floor && outageFloorId.HasValue && residentFloorId == outageFloorId.Value)
                || (outageScope == UtilityOutageAffectedScope.Unit && outagePropertyUnitId.HasValue && residentPropertyUnitId == outagePropertyUnitId.Value));
    }

    private static IReadOnlyList<string> BuildCommunicationCommandCenterActions(
        int acknowledgementGapCount,
        int criticalOutageCount,
        int overdueOutageCount,
        int failedOutboxItemCount,
        int urgentConversationCount,
        int escalatedConversationCount,
        int unassignedConversationCount)
    {
        var actions = new List<string>();
        if (criticalOutageCount > 0)
        {
            actions.Add("Publish operational updates for critical outages and confirm affected resident coverage.");
        }

        if (overdueOutageCount > 0)
        {
            actions.Add("Escalate overdue outages and publish revised restoration times.");
        }

        if (acknowledgementGapCount > 0)
        {
            actions.Add("Review critical announcement acknowledgement gaps and send follow-up broadcasts.");
        }

        if (failedOutboxItemCount > 0)
        {
            actions.Add("Review failed notification outbox items and retry high-priority delivery.");
        }

        if (urgentConversationCount > 0 || escalatedConversationCount > 0)
        {
            actions.Add("Assign urgent and escalated conversations to accountable staff immediately.");
        }

        if (unassignedConversationCount > 0)
        {
            actions.Add("Assign all open unassigned conversations before the next shift handover.");
        }

        if (actions.Count == 0)
        {
            actions.Add("No critical communication action is currently required.");
        }

        return actions;
    }

    private static string DetermineCommunicationOverallRisk(int criticalActionCount, int failedOutboxItemCount, int overdueOutageCount)
    {
        if (failedOutboxItemCount > 0 || overdueOutageCount > 0 || criticalActionCount >= 5)
        {
            return "Critical";
        }

        if (criticalActionCount > 0)
        {
            return "Warning";
        }

        return "Normal";
    }

    private static string DetermineAnnouncementAcknowledgementRisk(AnnouncementPriority priority, int missingCount, decimal acknowledgementRate)
    {
        if (missingCount == 0)
        {
            return "Normal";
        }

        if (priority == AnnouncementPriority.Critical || acknowledgementRate < 50m)
        {
            return "Critical";
        }

        return priority == AnnouncementPriority.High || acknowledgementRate < 80m ? "Warning" : "Monitor";
    }

    private static string DetermineAnnouncementAcknowledgementAction(AnnouncementPriority priority, int missingCount, decimal acknowledgementRate)
    {
        if (missingCount == 0)
        {
            return "No follow-up required.";
        }

        if (priority == AnnouncementPriority.Critical || acknowledgementRate < 50m)
        {
            return "Escalate acknowledgement follow-up and notify affected residents again.";
        }

        if (priority == AnnouncementPriority.High || acknowledgementRate < 80m)
        {
            return "Send follow-up reminder and monitor acknowledgement coverage.";
        }

        return "Monitor acknowledgement completion.";
    }

    private static string DetermineOutageOperationalRisk(UtilityOutageSeverity severity, bool isOverdue, int updateCount)
    {
        if (isOverdue || severity == UtilityOutageSeverity.Critical)
        {
            return "Critical";
        }

        if (severity == UtilityOutageSeverity.High || updateCount == 0)
        {
            return "Warning";
        }

        return "Normal";
    }

    private static string DetermineOutageRecommendedAction(UtilityOutageSeverity severity, bool isOverdue, int updateCount)
    {
        if (isOverdue)
        {
            return "Publish revised restoration time and escalate to operations.";
        }

        if (severity == UtilityOutageSeverity.Critical)
        {
            return "Keep residents updated frequently until resolved.";
        }

        if (updateCount == 0)
        {
            return "Publish an initial resident-facing outage update.";
        }

        return "Continue monitoring until resolved.";
    }

    private static string DetermineResidentCommunicationImpact(int activeOutageCount, int criticalOutageCount, int unreadNotificationCount, int pendingOutboxCount)
    {
        if (criticalOutageCount > 0 || pendingOutboxCount >= 3)
        {
            return "Critical";
        }

        if (activeOutageCount > 0 || unreadNotificationCount >= 3 || pendingOutboxCount > 0)
        {
            return "Warning";
        }

        return "Normal";
    }

    private static string DetermineResidentCommunicationAction(int activeOutageCount, int criticalOutageCount, int unreadNotificationCount, int pendingOutboxCount)
    {
        if (criticalOutageCount > 0)
        {
            return "Confirm resident received critical outage communication.";
        }

        if (pendingOutboxCount > 0)
        {
            return "Check pending notification delivery and retry if needed.";
        }

        if (unreadNotificationCount >= 3)
        {
            return "Send concise reminder or use an alternative communication channel.";
        }

        if (activeOutageCount > 0)
        {
            return "Keep resident updated until outage is resolved.";
        }

        return "No direct follow-up required.";
    }

    private static bool IsConversationStale(
        ConversationStatus status,
        ConversationPriority priority,
        ConversationEscalationLevel escalationLevel,
        int hoursSinceLastMessage)
    {
        if (status == ConversationStatus.PendingAdminReply && hoursSinceLastMessage >= 12)
        {
            return true;
        }

        if (priority == ConversationPriority.Urgent && hoursSinceLastMessage >= 4)
        {
            return true;
        }

        return escalationLevel != ConversationEscalationLevel.None && hoursSinceLastMessage >= 8;
    }

    private static string DetermineConversationSlaRisk(
        ConversationPriority priority,
        ConversationEscalationLevel escalationLevel,
        int hoursSinceLastMessage)
    {
        if (priority == ConversationPriority.Urgent || escalationLevel == ConversationEscalationLevel.Critical || hoursSinceLastMessage >= 24)
        {
            return "Critical";
        }

        return escalationLevel != ConversationEscalationLevel.None || hoursSinceLastMessage >= 12 ? "Warning" : "Monitor";
    }

    private static string DetermineConversationSlaAction(ConversationStatus status, Guid? assignedToUserId, int hoursSinceLastMessage)
    {
        if (!assignedToUserId.HasValue)
        {
            return "Assign the conversation to a responsible staff member.";
        }

        if (status == ConversationStatus.PendingAdminReply)
        {
            return "Send an admin reply or status update to the resident.";
        }

        if (hoursSinceLastMessage >= 24)
        {
            return "Review the conversation and confirm next action before shift handover.";
        }

        return "Monitor conversation response time.";
    }

    private async Task<UtilityOutage?> GetEditableOutageAsync(Guid id, CancellationToken cancellationToken)
    {
        var outage = await dbContext.UtilityOutages
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (outage is null || !await CanAccessCompoundAsync(outage.CompoundId, cancellationToken))
        {
            return null;
        }

        return outage;
    }

    private async Task<ServiceResult<bool>> ValidateOutageScopeAsync(
        Guid compoundId,
        UtilityOutageAffectedScope affectedScope,
        Guid? buildingId,
        Guid? floorId,
        Guid? propertyUnitId,
        CancellationToken cancellationToken)
    {
        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(item => item.Id == compoundId && item.IsActive, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<bool>.BadRequest("Compound was not found.");
        }

        if (affectedScope == UtilityOutageAffectedScope.Compound)
        {
            return ServiceResult<bool>.Success(true);
        }

        if (affectedScope == UtilityOutageAffectedScope.Building)
        {
            if (!buildingId.HasValue)
            {
                return ServiceResult<bool>.BadRequest("Building id is required for building-scoped outages.");
            }

            var validBuilding = await dbContext.Buildings.AsNoTracking()
                .AnyAsync(item => item.Id == buildingId.Value && item.CompoundId == compoundId, cancellationToken);
            return validBuilding ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.BadRequest("Building was not found in the selected compound.");
        }

        if (affectedScope == UtilityOutageAffectedScope.Floor)
        {
            if (!floorId.HasValue)
            {
                return ServiceResult<bool>.BadRequest("Floor id is required for floor-scoped outages.");
            }

            var validFloor = await dbContext.Floors.AsNoTracking()
                .AnyAsync(item => item.Id == floorId.Value
                    && item.CompoundId == compoundId
                    && (!buildingId.HasValue || item.BuildingId == buildingId.Value), cancellationToken);
            return validFloor ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.BadRequest("Floor was not found in the selected compound.");
        }

        if (!propertyUnitId.HasValue)
        {
            return ServiceResult<bool>.BadRequest("Property unit id is required for unit-scoped outages.");
        }

        var validUnit = await dbContext.PropertyUnits.AsNoTracking()
            .AnyAsync(item => item.Id == propertyUnitId.Value
                && item.CompoundId == compoundId
                && (!buildingId.HasValue || item.BuildingId == buildingId.Value)
                && (!floorId.HasValue || item.FloorId == floorId.Value), cancellationToken);
        return validUnit ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.BadRequest("Property unit was not found in the selected compound.");
    }

    private async Task<List<OutageResidentRecipient>> GetAffectedResidentRecipientsAsync(
        UtilityOutage outage,
        CancellationToken cancellationToken)
    {
        var query = dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.CompoundId == outage.CompoundId
                && record.OccupancyStatus == OccupancyStatus.Active
                && record.ResidentProfile.IsActive);

        if (outage.AffectedScope == UtilityOutageAffectedScope.Building && outage.BuildingId.HasValue)
        {
            query = query.Where(record => record.PropertyUnit.BuildingId == outage.BuildingId.Value);
        }
        else if (outage.AffectedScope == UtilityOutageAffectedScope.Floor && outage.FloorId.HasValue)
        {
            query = query.Where(record => record.PropertyUnit.FloorId == outage.FloorId.Value);
        }
        else if (outage.AffectedScope == UtilityOutageAffectedScope.Unit && outage.PropertyUnitId.HasValue)
        {
            query = query.Where(record => record.PropertyUnitId == outage.PropertyUnitId.Value);
        }

        return await query
            .Select(record => new OutageResidentRecipient(
                record.ResidentProfileId,
                record.ResidentProfile.UserId,
                record.ResidentProfile.FullName,
                record.ResidentProfile.PhoneNumber))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<int> AddResidentNotificationsAsync(
        UtilityOutage outage,
        IReadOnlyCollection<OutageResidentRecipient> recipients,
        Guid? currentUserId,
        string title,
        string message,
        NotificationPriority priority,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userIds = recipients.Select(recipient => recipient.UserId).Distinct().ToArray();
        var preferences = await dbContext.ResidentNotificationPreferences
            .Where(preference => userIds.Contains(preference.UserId))
            .ToDictionaryAsync(preference => preference.UserId, cancellationToken);
        var severity = MapResidentNotificationSeverity(priority);
        var outboxItemCount = 0;

        foreach (var recipient in recipients)
        {
            preferences.TryGetValue(recipient.UserId, out var preference);
            var suppressionReason = ResidentNotificationPreferencePolicy.GetSuppressionReason(
                preference,
                ResidentNotificationType.Announcement,
                severity,
                priority,
                now);
            if (suppressionReason is not null)
            {
                continue;
            }

            dbContext.ResidentNotifications.Add(new ResidentNotification
            {
                UserId = recipient.UserId,
                Title = title,
                Message = message,
                Type = ResidentNotificationType.Announcement,
                Severity = severity,
                RelatedEntityType = nameof(UtilityOutage),
                RelatedEntityId = outage.Id,
                CreatedAt = now
            });

            dbContext.NotificationOutboxes.Add(new NotificationOutbox
            {
                CompoundId = outage.CompoundId,
                ResidentProfileId = recipient.ResidentProfileId,
                RecipientUserId = recipient.UserId,
                Channel = NotificationChannel.InApp,
                EventType = outage.Status == UtilityOutageStatus.Resolved
                    ? NotificationEventType.UtilityOutageResolved
                    : NotificationEventType.UtilityOutageUpdated,
                Priority = priority,
                RecipientName = recipient.FullName,
                RecipientPhoneNumber = recipient.PhoneNumber,
                Subject = title,
                Body = message,
                RelatedEntityType = NotificationRelatedEntityType.UtilityOutage,
                RelatedEntityId = outage.Id,
                ScheduledAtUtc = now,
                CreatedByUserId = currentUserId
            });
            outboxItemCount++;
        }

        return outboxItemCount;
    }

    private async Task<IQueryable<UtilityOutage>> GetResidentVisibleOutagesAsync(
        Guid currentUserId,
        IQueryable<UtilityOutage> outages,
        CancellationToken cancellationToken)
    {
        var activeOccupancies = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.ResidentProfile.UserId == currentUserId
                && record.ResidentProfile.IsActive
                && record.OccupancyStatus == OccupancyStatus.Active)
            .Select(record => new
            {
                record.CompoundId,
                record.PropertyUnitId,
                record.PropertyUnit.BuildingId,
                record.PropertyUnit.FloorId
            })
            .ToArrayAsync(cancellationToken);

        if (activeOccupancies.Length == 0)
        {
            return outages.Where(_ => false);
        }

        var compoundIds = activeOccupancies.Select(item => item.CompoundId).Distinct().ToArray();
        var buildingIds = activeOccupancies.Where(item => item.BuildingId.HasValue).Select(item => item.BuildingId!.Value).Distinct().ToArray();
        var floorIds = activeOccupancies.Where(item => item.FloorId.HasValue).Select(item => item.FloorId!.Value).Distinct().ToArray();
        var unitIds = activeOccupancies.Select(item => item.PropertyUnitId).Distinct().ToArray();

        return outages.Where(outage =>
            compoundIds.Contains(outage.CompoundId)
            && (outage.AffectedScope == UtilityOutageAffectedScope.Compound
                || (outage.AffectedScope == UtilityOutageAffectedScope.Building && outage.BuildingId.HasValue && buildingIds.Contains(outage.BuildingId.Value))
                || (outage.AffectedScope == UtilityOutageAffectedScope.Floor && outage.FloorId.HasValue && floorIds.Contains(outage.FloorId.Value))
                || (outage.AffectedScope == UtilityOutageAffectedScope.Unit && outage.PropertyUnitId.HasValue && unitIds.Contains(outage.PropertyUnitId.Value))));
    }

    private async Task<Guid[]> GetResidentCompoundIdsAsync(Guid currentUserId, CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.ResidentProfile.UserId == currentUserId
                && record.ResidentProfile.IsActive
                && record.OccupancyStatus == OccupancyStatus.Active)
            .Select(record => record.CompoundId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private IQueryable<UtilityOutage> ApplyOutageFilters(IQueryable<UtilityOutage> query, UtilityOutageQueryRequest filters)
    {
        if (filters.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == filters.CompoundId.Value);
        }

        if (filters.BuildingId.HasValue)
        {
            query = query.Where(item => item.BuildingId == filters.BuildingId.Value);
        }

        if (filters.FloorId.HasValue)
        {
            query = query.Where(item => item.FloorId == filters.FloorId.Value);
        }

        if (filters.PropertyUnitId.HasValue)
        {
            query = query.Where(item => item.PropertyUnitId == filters.PropertyUnitId.Value);
        }

        if (filters.ServiceType.HasValue)
        {
            query = query.Where(item => item.ServiceType == filters.ServiceType.Value);
        }

        if (filters.Status.HasValue)
        {
            query = query.Where(item => item.Status == filters.Status.Value);
        }

        if (filters.Severity.HasValue)
        {
            query = query.Where(item => item.Severity == filters.Severity.Value);
        }

        if (filters.ActiveOnly)
        {
            query = query.Where(IsOpenOutageExpression());
        }

        return query;
    }

    private static System.Linq.Expressions.Expression<Func<UtilityOutage, bool>> IsOpenOutageExpression()
    {
        return item => item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active;
    }

    private async Task<PagedResult<UtilityOutageResponse>> ToPagedOutageResultAsync(
        IQueryable<UtilityOutage> query,
        UtilityOutageQueryRequest pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.Status == UtilityOutageStatus.Active)
            .ThenByDescending(item => item.Severity)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(item => ToOutageResponse(item))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<UtilityOutageResponse>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    private async Task<UtilityOutageDetailsResponse> ToDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var outage = await dbContext.UtilityOutages
            .AsNoTracking()
            .Include(item => item.Updates)
            .FirstAsync(item => item.Id == id, cancellationToken);

        return new UtilityOutageDetailsResponse(
            ToOutageResponse(outage),
            outage.Updates
                .OrderBy(item => item.CreatedAtUtc)
                .Select(ToUpdateResponse)
                .ToArray());
    }

    private static UtilityOutageResponse ToOutageResponse(UtilityOutage outage)
    {
        return new UtilityOutageResponse(
            outage.Id,
            outage.CompoundId,
            outage.BuildingId,
            outage.FloorId,
            outage.PropertyUnitId,
            outage.AnnouncementId,
            outage.ServiceType,
            outage.AffectedScope,
            outage.Status,
            outage.Severity,
            outage.Title,
            outage.Description,
            outage.EstimatedStartAtUtc,
            outage.EstimatedEndAtUtc,
            outage.PublishedAtUtc,
            outage.ResolvedAtUtc,
            outage.ResolutionNotes,
            outage.NotifyResidents,
            outage.RecipientCount,
            outage.OutboxItemCount,
            outage.CreatedByUserId,
            outage.ResolvedByUserId,
            outage.CreatedAtUtc,
            outage.UpdatedAtUtc,
            outage.Updates.Count);
    }

    private static UtilityOutageUpdateResponse ToUpdateResponse(UtilityOutageUpdate update)
    {
        return new UtilityOutageUpdateResponse(
            update.Id,
            update.UtilityOutageId,
            update.UpdateType,
            update.Message,
            update.NewEstimatedEndAtUtc,
            update.CreatedByUserId,
            update.CreatedAtUtc);
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<CompoundAccessScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            ? new CompoundAccessScope(true, true, [])
            : await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private static AnnouncementPriority MapAnnouncementPriority(UtilityOutageSeverity severity)
    {
        return severity switch
        {
            UtilityOutageSeverity.Low => AnnouncementPriority.Low,
            UtilityOutageSeverity.High => AnnouncementPriority.High,
            UtilityOutageSeverity.Critical => AnnouncementPriority.Critical,
            _ => AnnouncementPriority.Normal
        };
    }

    private static NotificationPriority MapNotificationPriority(UtilityOutageSeverity severity)
    {
        return severity switch
        {
            UtilityOutageSeverity.Low => NotificationPriority.Low,
            UtilityOutageSeverity.High => NotificationPriority.High,
            UtilityOutageSeverity.Critical => NotificationPriority.Urgent,
            _ => NotificationPriority.Normal
        };
    }

    private static ResidentNotificationSeverity MapResidentNotificationSeverity(NotificationPriority priority)
    {
        return priority switch
        {
            NotificationPriority.Urgent => ResidentNotificationSeverity.Critical,
            NotificationPriority.High => ResidentNotificationSeverity.Warning,
            _ => ResidentNotificationSeverity.Info
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record OutageResidentRecipient(
        Guid ResidentProfileId,
        Guid UserId,
        string FullName,
        string? PhoneNumber);
}
