using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Notifications;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using DARAK.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DARAK.Api.Services;

public sealed class NotificationOutboxService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IEmailSender emailSender,
    ISmsSender smsSender,
    IOptions<NotificationOptions> options)
    : INotificationOutboxService
{
    public async Task<ServiceResult<NotificationOutboxResponse>> EnqueueAsync(
        Guid? currentUserId,
        EnqueueNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateEnqueueRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var notification = new NotificationOutbox
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            RecipientUserId = request.RecipientUserId,
            Channel = request.Channel,
            EventType = request.EventType,
            Priority = request.Priority,
            RecipientName = request.RecipientName?.Trim() ?? string.Empty,
            RecipientEmail = NormalizeOptional(request.RecipientEmail),
            RecipientPhoneNumber = NormalizeOptional(request.RecipientPhoneNumber),
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            MetadataJson = NormalizeOptional(request.MetadataJson),
            ScheduledAtUtc = request.ScheduledAtUtc ?? DateTime.UtcNow,
            MaxRetryCount = request.MaxRetryCount,
            CreatedByUserId = currentUserId
        };

        dbContext.NotificationOutboxes.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<NotificationOutboxResponse>.Success(ToResponse(notification, []));
    }

    public async Task<ServiceResult<NotificationOutboxResponse>> EnqueueManualAsync(
        Guid? currentUserId,
        ManualNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<NotificationOutboxResponse>.Forbidden("Authentication is required.");
        }

        var resolvedScope = await ResolveManualScopeAsync(request, cancellationToken);
        if (resolvedScope.Error is not null)
        {
            return resolvedScope.Error;
        }

        var enqueueRequest = new EnqueueNotificationRequest
        {
            CompoundId = resolvedScope.CompoundId,
            ResidentProfileId = resolvedScope.ResidentProfileId,
            RecipientUserId = resolvedScope.RecipientUserId,
            Channel = request.Channel,
            EventType = NotificationEventType.General,
            Priority = request.Priority,
            RecipientName = resolvedScope.RecipientName,
            RecipientEmail = resolvedScope.RecipientEmail,
            RecipientPhoneNumber = resolvedScope.RecipientPhoneNumber,
            Subject = request.Subject,
            Body = request.Body,
            ScheduledAtUtc = request.ScheduledAtUtc,
            MaxRetryCount = request.MaxRetryCount
        };

        return await EnqueueAsync(currentUserId, enqueueRequest, cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<NotificationOutboxResponse>>> SearchAsync(
        Guid? currentUserId,
        NotificationSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<NotificationOutboxResponse>>.Forbidden("Authentication is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<NotificationOutboxResponse>>.Forbidden("Authentication is required.");
        }

        var notifications = ApplySearchFilters(
            ApplyScope(dbContext.NotificationOutboxes.AsNoTracking(), scope),
            query);

        var totalCount = await notifications.CountAsync(cancellationToken);
        var items = await notifications
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(notification => notification.DeliveryAttempts.OrderByDescending(attempt => attempt.StartedAtUtc))
            .ToListAsync(cancellationToken);

        var response = new PagedResult<NotificationOutboxResponse>(
            items.Select(notification => ToResponse(notification, notification.DeliveryAttempts)).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);

        return ServiceResult<PagedResult<NotificationOutboxResponse>>.Success(response);
    }

    public async Task<ServiceResult<NotificationOutboxResponse>> GetAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<NotificationOutboxResponse>.Forbidden("Authentication is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var notification = await ApplyScope(dbContext.NotificationOutboxes.AsNoTracking(), scope)
            .Include(item => item.DeliveryAttempts.OrderByDescending(attempt => attempt.StartedAtUtc))
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (notification is null)
        {
            return ServiceResult<NotificationOutboxResponse>.NotFound("Notification was not found.");
        }

        return ServiceResult<NotificationOutboxResponse>.Success(
            ToResponse(notification, notification.DeliveryAttempts));
    }

    public async Task<ServiceResult<NotificationOutboxResponse>> MarkForRetryAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<NotificationOutboxResponse>.Forbidden("Authentication is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var notification = await ApplyScope(dbContext.NotificationOutboxes, scope)
            .Include(item => item.DeliveryAttempts)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (notification is null)
        {
            return ServiceResult<NotificationOutboxResponse>.NotFound("Notification was not found.");
        }

        if (notification.Status == NotificationStatus.Sent)
        {
            return ServiceResult<NotificationOutboxResponse>.Conflict("Sent notifications cannot be retried.");
        }

        if (notification.Status == NotificationStatus.Processing)
        {
            return ServiceResult<NotificationOutboxResponse>.Conflict("Processing notifications cannot be manually retried.");
        }

        notification.Status = NotificationStatus.Pending;
        notification.NextRetryAtUtc = DateTime.UtcNow;
        notification.RetryCount = 0;
        notification.LastError = null;
        notification.FailedAtUtc = null;
        notification.ProcessingStartedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<NotificationOutboxResponse>.Success(
            ToResponse(notification, notification.DeliveryAttempts));
    }

    public async Task<ServiceResult<NotificationDashboardSummaryResponse>> GetDashboardSummaryAsync(
        Guid? currentUserId,
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<NotificationDashboardSummaryResponse>.Forbidden("Authentication is required.");
        }

        if (compoundId == Guid.Empty)
        {
            return ServiceResult<NotificationDashboardSummaryResponse>.BadRequest("Compound id is invalid.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<NotificationDashboardSummaryResponse>.Forbidden("You do not have access to this compound.");
        }

        var now = DateTime.UtcNow;
        var since = now.AddHours(-24);
        var notifications = ApplyScope(dbContext.NotificationOutboxes.AsNoTracking(), scope);

        if (compoundId.HasValue)
        {
            notifications = notifications.Where(notification => notification.CompoundId == compoundId.Value);
        }

        var pendingCount = await notifications.CountAsync(
            notification => notification.Status == NotificationStatus.Pending,
            cancellationToken);

        var processingCount = await notifications.CountAsync(
            notification => notification.Status == NotificationStatus.Processing,
            cancellationToken);

        var sentLast24Hours = await notifications.CountAsync(
            notification =>
                notification.Status == NotificationStatus.Sent
                && notification.SentAtUtc >= since,
            cancellationToken);

        var failedCount = await notifications.CountAsync(
            notification => notification.Status == NotificationStatus.Failed,
            cancellationToken);

        var dueForRetryCount = await notifications.CountAsync(
            notification =>
                notification.Status == NotificationStatus.Pending
                && notification.ScheduledAtUtc <= now
                && (!notification.NextRetryAtUtc.HasValue || notification.NextRetryAtUtc <= now),
            cancellationToken);

        var oldestPendingScheduledAtUtc = await notifications
            .Where(notification => notification.Status == NotificationStatus.Pending)
            .OrderBy(notification => notification.ScheduledAtUtc)
            .Select(notification => (DateTime?)notification.ScheduledAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var response = new NotificationDashboardSummaryResponse(
            pendingCount,
            processingCount,
            sentLast24Hours,
            failedCount,
            dueForRetryCount,
            oldestPendingScheduledAtUtc);

        return ServiceResult<NotificationDashboardSummaryResponse>.Success(response);
    }

    public async Task<int> ProcessDueNotificationsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        // Load only ids first. This prevents stale tracked entities from previous service/test
        // operations from being updated during notification delivery processing.
        dbContext.ChangeTracker.Clear();

        var hasNotifications = await dbContext.NotificationOutboxes
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (!hasNotifications)
        {
            return 0;
        }

        await RecoverStaleProcessingNotificationsAsync(now, cancellationToken);
        dbContext.ChangeTracker.Clear();

        var notificationIds = await dbContext.NotificationOutboxes
            .AsNoTracking()
            .Where(notification =>
                notification.Status == NotificationStatus.Pending
                && notification.ScheduledAtUtc <= now
                && notification.RetryCount < notification.MaxRetryCount
                && (!notification.NextRetryAtUtc.HasValue || notification.NextRetryAtUtc <= now))
            .OrderByDescending(notification => notification.Priority)
            .ThenBy(notification => notification.ScheduledAtUtc)
            .Select(notification => notification.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var notificationId in notificationIds)
        {
            var claimedNotification = await TryClaimNotificationAsync(notificationId, now, cancellationToken);
            if (claimedNotification is null)
            {
                continue;
            }

            if (await ProcessNotificationAsync(claimedNotification, cancellationToken))
            {
                processed++;
            }
        }

        dbContext.ChangeTracker.Clear();

        return processed;
    }

    private async Task RecoverStaleProcessingNotificationsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var timeoutMinutes = Math.Max(1, options.Value.ProcessingTimeoutMinutes);
        var staleProcessingThreshold = now.AddMinutes(-timeoutMinutes);

        if (!dbContext.Database.IsRelational())
        {
            var staleNotifications = await dbContext.NotificationOutboxes
                .Where(notification =>
                    notification.Status == NotificationStatus.Processing
                    && notification.ProcessingStartedAtUtc.HasValue
                    && notification.ProcessingStartedAtUtc <= staleProcessingThreshold)
                .ToListAsync(cancellationToken);

            foreach (var notification in staleNotifications)
            {
                RecoverStaleProcessingNotification(notification, now);
            }

            if (staleNotifications.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        await dbContext.NotificationOutboxes
            .Where(notification =>
                notification.Status == NotificationStatus.Processing
                && notification.ProcessingStartedAtUtc.HasValue
                && notification.ProcessingStartedAtUtc <= staleProcessingThreshold
                && notification.RetryCount < notification.MaxRetryCount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.Status, NotificationStatus.Pending)
                .SetProperty(notification => notification.ProcessingStartedAtUtc, (DateTime?)null)
                .SetProperty(notification => notification.NextRetryAtUtc, (DateTime?)now)
                .SetProperty(notification => notification.LastError, "Notification processing timed out and was re-queued."),
                cancellationToken);

        await dbContext.NotificationOutboxes
            .Where(notification =>
                notification.Status == NotificationStatus.Processing
                && notification.ProcessingStartedAtUtc.HasValue
                && notification.ProcessingStartedAtUtc <= staleProcessingThreshold
                && notification.RetryCount >= notification.MaxRetryCount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.Status, NotificationStatus.Failed)
                .SetProperty(notification => notification.ProcessingStartedAtUtc, (DateTime?)null)
                .SetProperty(notification => notification.FailedAtUtc, (DateTime?)now)
                .SetProperty(notification => notification.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(notification => notification.LastError, "Notification processing timed out after exhausting retry attempts."),
                cancellationToken);
    }

    private static void RecoverStaleProcessingNotification(NotificationOutbox notification, DateTime now)
    {
        notification.ProcessingStartedAtUtc = null;

        if (notification.RetryCount < notification.MaxRetryCount)
        {
            notification.Status = NotificationStatus.Pending;
            notification.NextRetryAtUtc = now;
            notification.LastError = "Notification processing timed out and was re-queued.";
            return;
        }

        notification.Status = NotificationStatus.Failed;
        notification.FailedAtUtc = now;
        notification.NextRetryAtUtc = null;
        notification.LastError = "Notification processing timed out after exhausting retry attempts.";
    }

    private async Task<NotificationOutbox?> TryClaimNotificationAsync(
        Guid notificationId,
        DateTime claimStartedAtUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        if (!dbContext.Database.IsRelational())
        {
            var pendingNotification = await dbContext.NotificationOutboxes
                .SingleOrDefaultAsync(notification =>
                    notification.Id == notificationId
                    && notification.Status == NotificationStatus.Pending
                    && notification.ScheduledAtUtc <= claimStartedAtUtc
                    && notification.RetryCount < notification.MaxRetryCount
                    && (!notification.NextRetryAtUtc.HasValue || notification.NextRetryAtUtc <= claimStartedAtUtc),
                    cancellationToken);

            if (pendingNotification is null)
            {
                return null;
            }

            pendingNotification.Status = NotificationStatus.Processing;
            pendingNotification.ProcessingStartedAtUtc = claimStartedAtUtc;
            pendingNotification.RetryCount++;
            await dbContext.SaveChangesAsync(cancellationToken);

            return pendingNotification;
        }

        var affectedRows = await dbContext.NotificationOutboxes
            .Where(notification =>
                notification.Id == notificationId
                && notification.Status == NotificationStatus.Pending
                && notification.ScheduledAtUtc <= claimStartedAtUtc
                && notification.RetryCount < notification.MaxRetryCount
                && (!notification.NextRetryAtUtc.HasValue || notification.NextRetryAtUtc <= claimStartedAtUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.Status, NotificationStatus.Processing)
                .SetProperty(notification => notification.ProcessingStartedAtUtc, claimStartedAtUtc)
                .SetProperty(notification => notification.RetryCount, notification => notification.RetryCount + 1),
                cancellationToken);

        if (affectedRows != 1)
        {
            return null;
        }

        return await dbContext.NotificationOutboxes
            .SingleAsync(notification => notification.Id == notificationId, cancellationToken);
    }

    private async Task<bool> ProcessNotificationAsync(
        NotificationOutbox notification,
        CancellationToken cancellationToken)
    {
        var now = notification.ProcessingStartedAtUtc ?? DateTime.UtcNow;

        var attempt = new NotificationDeliveryAttempt
        {
            NotificationOutboxId = notification.Id,
            AttemptNumber = notification.RetryCount,
            Status = NotificationDeliveryAttemptStatus.Processing,
            ProviderName = GetProviderName(notification.Channel),
            StartedAtUtc = now
        };

        dbContext.NotificationDeliveryAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await DispatchAsync(notification, cancellationToken);
        attempt.CompletedAtUtc = DateTime.UtcNow;
        attempt.ProviderName = result.ProviderName;
        attempt.ProviderMessageId = result.ProviderMessageId;
        attempt.ErrorMessage = result.ErrorMessage;

        notification.ProviderName = result.ProviderName;
        notification.ProviderMessageId = result.ProviderMessageId;

        if (result.Succeeded)
        {
            attempt.Status = NotificationDeliveryAttemptStatus.Succeeded;
            notification.Status = NotificationStatus.Sent;
            notification.SentAtUtc = attempt.CompletedAtUtc;
            notification.ProcessingStartedAtUtc = null;
            notification.FailedAtUtc = null;
            notification.NextRetryAtUtc = null;
            notification.LastError = null;
        }
        else
        {
            attempt.Status = result.WasSkipped
                ? NotificationDeliveryAttemptStatus.Skipped
                : NotificationDeliveryAttemptStatus.Failed;

            notification.LastError = result.ErrorMessage;
            notification.FailedAtUtc = attempt.CompletedAtUtc;
            notification.ProcessingStartedAtUtc = null;

            if (!result.WasSkipped && notification.RetryCount < notification.MaxRetryCount)
            {
                notification.Status = NotificationStatus.Pending;
                notification.NextRetryAtUtc = attempt.CompletedAtUtc!.Value.AddMinutes(GetRetryDelayMinutes(notification.RetryCount));
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
                notification.NextRetryAtUtc = null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        return true;
    }

    private int GetRetryDelayMinutes(int retryCount)
    {
        var baseDelayMinutes = Math.Max(1, options.Value.RetryDelayMinutes);
        var retryBackoffMultiplier = Math.Max(1, options.Value.RetryBackoffMultiplier);
        var maxRetryDelayMinutes = Math.Max(baseDelayMinutes, options.Value.MaxRetryDelayMinutes);
        var exponent = Math.Max(0, retryCount - 1);
        var calculatedDelay = baseDelayMinutes * Math.Pow(retryBackoffMultiplier, exponent);

        return Math.Min(maxRetryDelayMinutes, Math.Max(1, (int)Math.Ceiling(calculatedDelay)));
    }

    private Task<NotificationDeliveryResult> DispatchAsync(
        NotificationOutbox notification,
        CancellationToken cancellationToken)
    {
        return notification.Channel switch
        {
            NotificationChannel.InApp => Task.FromResult(NotificationDeliveryResult.Success("InApp")),
            NotificationChannel.Email => emailSender.SendAsync(
                new EmailNotificationMessage(
                    notification.RecipientEmail ?? string.Empty,
                    notification.RecipientName,
                    notification.Subject,
                    notification.Body),
                cancellationToken),
            NotificationChannel.Sms => smsSender.SendAsync(
                new SmsNotificationMessage(
                    notification.RecipientPhoneNumber ?? string.Empty,
                    notification.Body),
                cancellationToken),
            _ => Task.FromResult(NotificationDeliveryResult.Failed("Unknown", "Unsupported notification channel."))
        };
    }

    private async Task<ManualNotificationScope> ResolveManualScopeAsync(
        ManualNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ManualNotificationScope.Failed(
                ServiceResult<NotificationOutboxResponse>.Forbidden("Authentication is required."));
        }

        Guid? targetCompoundId = request.CompoundId;
        Guid? residentProfileId = request.ResidentProfileId;
        Guid? recipientUserId = request.RecipientUserId;
        var recipientName = request.RecipientName;
        var recipientEmail = request.RecipientEmail;
        var recipientPhoneNumber = request.RecipientPhoneNumber;

        if (request.ResidentProfileId.HasValue)
        {
            var resident = await dbContext.ResidentProfiles
                .AsNoTracking()
                .Where(profile => profile.Id == request.ResidentProfileId.Value)
                .Select(profile => new
                {
                    profile.Id,
                    profile.CompoundId,
                    profile.UserId,
                    profile.FullName,
                    profile.PhoneNumber
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (resident is null)
            {
                return ManualNotificationScope.Failed(
                    ServiceResult<NotificationOutboxResponse>.NotFound("Resident profile was not found."));
            }

            if (request.CompoundId.HasValue && resident.CompoundId != request.CompoundId.Value)
            {
                return ManualNotificationScope.Failed(
                    ServiceResult<NotificationOutboxResponse>.BadRequest("Resident profile does not belong to the selected compound."));
            }

            if (request.RecipientUserId.HasValue && request.RecipientUserId.Value != resident.UserId)
            {
                return ManualNotificationScope.Failed(
                    ServiceResult<NotificationOutboxResponse>.BadRequest("Recipient user does not match the selected resident profile."));
            }

            var residentUserEmail = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == resident.UserId)
                .Select(user => user.Email)
                .SingleOrDefaultAsync(cancellationToken);

            targetCompoundId = resident.CompoundId;
            residentProfileId = resident.Id;
            recipientUserId = request.RecipientUserId ?? resident.UserId;
            recipientName = string.IsNullOrWhiteSpace(request.RecipientName) ? resident.FullName : request.RecipientName;
            recipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail) ? residentUserEmail : request.RecipientEmail;
            recipientPhoneNumber = string.IsNullOrWhiteSpace(request.RecipientPhoneNumber) ? resident.PhoneNumber : request.RecipientPhoneNumber;
        }

        if (!scope.IsSuperAdmin)
        {
            if (!targetCompoundId.HasValue)
            {
                return ManualNotificationScope.Failed(
                    ServiceResult<NotificationOutboxResponse>.Forbidden("Manual notifications require an accessible compound."));
            }

            if (!scope.CanAccess(targetCompoundId.Value))
            {
                return ManualNotificationScope.Failed(
                    ServiceResult<NotificationOutboxResponse>.Forbidden("You do not have access to this compound."));
            }
        }

        return ManualNotificationScope.Success(
            targetCompoundId,
            residentProfileId,
            recipientUserId,
            recipientName,
            recipientEmail,
            recipientPhoneNumber);
    }

    private async Task<ServiceResult<NotificationOutboxResponse>?> ValidateEnqueueRequestAsync(
        EnqueueNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompoundId == Guid.Empty
            || request.ResidentProfileId == Guid.Empty
            || request.RecipientUserId == Guid.Empty
            || request.RelatedEntityId == Guid.Empty)
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("One or more identifiers are invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Notification subject is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Notification body is required.");
        }

        if (request.RelatedEntityType == NotificationRelatedEntityType.None && request.RelatedEntityId.HasValue)
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Related entity id is not allowed when related entity type is None.");
        }

        if (request.RelatedEntityType != NotificationRelatedEntityType.None && !request.RelatedEntityId.HasValue)
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Related entity id is required for the selected related entity type.");
        }

        if (request.MaxRetryCount < 0)
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Max retry count cannot be negative.");
        }

        if (request.Channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Recipient email is required for email notifications.");
        }

        if (request.Channel == NotificationChannel.Sms && string.IsNullOrWhiteSpace(request.RecipientPhoneNumber))
        {
            return ServiceResult<NotificationOutboxResponse>.BadRequest("Recipient phone number is required for SMS notifications.");
        }

        if (request.CompoundId.HasValue
            && !await dbContext.Compounds.AsNoTracking().AnyAsync(
                compound => compound.Id == request.CompoundId.Value,
                cancellationToken))
        {
            return ServiceResult<NotificationOutboxResponse>.NotFound("Compound was not found.");
        }

        if (request.ResidentProfileId.HasValue)
        {
            var resident = await dbContext.ResidentProfiles
                .AsNoTracking()
                .Where(profile => profile.Id == request.ResidentProfileId.Value)
                .Select(profile => new { profile.Id, profile.CompoundId, profile.UserId, profile.FullName, profile.PhoneNumber })
                .SingleOrDefaultAsync(cancellationToken);

            if (resident is null)
            {
                return ServiceResult<NotificationOutboxResponse>.NotFound("Resident profile was not found.");
            }

            if (request.CompoundId.HasValue && resident.CompoundId != request.CompoundId.Value)
            {
                return ServiceResult<NotificationOutboxResponse>.BadRequest("Resident profile does not belong to the selected compound.");
            }

            if (request.RecipientUserId.HasValue && request.RecipientUserId.Value != resident.UserId)
            {
                return ServiceResult<NotificationOutboxResponse>.BadRequest("Recipient user does not match the selected resident profile.");
            }
        }

        if (request.RecipientUserId.HasValue
            && !await dbContext.Users.AsNoTracking().AnyAsync(
                user => user.Id == request.RecipientUserId.Value,
                cancellationToken))
        {
            return ServiceResult<NotificationOutboxResponse>.NotFound("Recipient user was not found.");
        }

        return null;
    }

    private static IQueryable<NotificationOutbox> ApplyScope(
        IQueryable<NotificationOutbox> notifications,
        CompoundAccessScope scope)
    {
        if (!scope.IsAuthenticated)
        {
            return notifications.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return notifications;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return notifications.Where(_ => false);
        }

        return notifications.Where(notification =>
            notification.CompoundId.HasValue
            && scope.AllowedCompoundIds.Contains(notification.CompoundId.Value));
    }

    private static IQueryable<NotificationOutbox> ApplySearchFilters(
        IQueryable<NotificationOutbox> notifications,
        NotificationSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            notifications = notifications.Where(notification => notification.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            notifications = notifications.Where(notification => notification.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.RecipientUserId.HasValue)
        {
            notifications = notifications.Where(notification => notification.RecipientUserId == query.RecipientUserId.Value);
        }

        if (query.Channel.HasValue)
        {
            notifications = notifications.Where(notification => notification.Channel == query.Channel.Value);
        }

        if (query.EventType.HasValue)
        {
            notifications = notifications.Where(notification => notification.EventType == query.EventType.Value);
        }

        if (query.Status.HasValue)
        {
            notifications = notifications.Where(notification => notification.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            notifications = notifications.Where(notification => notification.Priority == query.Priority.Value);
        }

        if (query.RelatedEntityType.HasValue)
        {
            notifications = notifications.Where(notification => notification.RelatedEntityType == query.RelatedEntityType.Value);
        }

        if (query.RelatedEntityId.HasValue)
        {
            notifications = notifications.Where(notification => notification.RelatedEntityId == query.RelatedEntityId.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            notifications = notifications.Where(notification => notification.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            notifications = notifications.Where(notification => notification.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            notifications = notifications.Where(notification =>
                notification.Subject.Contains(term)
                || notification.Body.Contains(term)
                || notification.RecipientName.Contains(term)
                || (notification.RecipientEmail != null && notification.RecipientEmail.Contains(term))
                || (notification.RecipientPhoneNumber != null && notification.RecipientPhoneNumber.Contains(term)));
        }

        return notifications;
    }

    private static NotificationOutboxResponse ToResponse(
        NotificationOutbox notification,
        IEnumerable<NotificationDeliveryAttempt> attempts)
    {
        return new NotificationOutboxResponse(
            notification.Id,
            notification.CompoundId,
            notification.ResidentProfileId,
            notification.RecipientUserId,
            notification.Channel,
            notification.EventType,
            notification.Priority,
            notification.Status,
            notification.RecipientName,
            notification.RecipientEmail,
            notification.RecipientPhoneNumber,
            notification.Subject,
            notification.Body,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.MetadataJson,
            notification.CreatedAtUtc,
            notification.ScheduledAtUtc,
            notification.ProcessingStartedAtUtc,
            notification.SentAtUtc,
            notification.FailedAtUtc,
            notification.CancelledAtUtc,
            notification.NextRetryAtUtc,
            notification.RetryCount,
            notification.MaxRetryCount,
            notification.LastError,
            notification.CreatedByUserId,
            notification.ProviderName,
            notification.ProviderMessageId,
            attempts.Select(ToAttemptResponse).ToArray());
    }

    private static NotificationDeliveryAttemptResponse ToAttemptResponse(NotificationDeliveryAttempt attempt)
    {
        return new NotificationDeliveryAttemptResponse(
            attempt.Id,
            attempt.NotificationOutboxId,
            attempt.AttemptNumber,
            attempt.Status,
            attempt.ProviderName,
            attempt.ProviderMessageId,
            attempt.ErrorMessage,
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc);
    }

    private static string GetProviderName(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.InApp => "InApp",
            NotificationChannel.Email => "SMTP",
            NotificationChannel.Sms => "HTTP-SMS",
            _ => "Unknown"
        };
    }

    private sealed record ManualNotificationScope(
        ServiceResult<NotificationOutboxResponse>? Error,
        Guid? CompoundId,
        Guid? ResidentProfileId,
        Guid? RecipientUserId,
        string? RecipientName,
        string? RecipientEmail,
        string? RecipientPhoneNumber)
    {
        public static ManualNotificationScope Failed(ServiceResult<NotificationOutboxResponse> error)
        {
            return new ManualNotificationScope(error, null, null, null, null, null, null);
        }

        public static ManualNotificationScope Success(
            Guid? compoundId,
            Guid? residentProfileId,
            Guid? recipientUserId,
            string? recipientName,
            string? recipientEmail,
            string? recipientPhoneNumber)
        {
            return new ManualNotificationScope(
                null,
                compoundId,
                residentProfileId,
                recipientUserId,
                recipientName,
                recipientEmail,
                recipientPhoneNumber);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

