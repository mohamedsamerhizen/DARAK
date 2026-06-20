using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CommercialCommunicationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : ICommercialCommunicationService
{
    private const int MaxTitleLength = 150;
    private const int MaxBodyLength = 4000;
    private const int MaxSuppressionReasonLength = 300;

    public async Task<ServiceResult<ResidentNotificationPreferenceResponse>> GetPreferencesAsync(
        Guid? currentUserId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentNotificationPreferenceResponse>.Forbidden("Authentication is required.");
        }

        var targetUserId = userId ?? currentUserId.Value;
        var targetValidation = await ValidatePreferenceTargetAccessAsync(currentUserId.Value, targetUserId, cancellationToken);
        if (targetValidation is not null)
        {
            return ToResult<ResidentNotificationPreferenceResponse>(targetValidation);
        }

        var preference = await GetOrCreatePreferenceAsync(targetUserId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ResidentNotificationPreferenceResponse>.Success(ToPreferenceResponse(preference));
    }

    public async Task<ServiceResult<ResidentNotificationPreferenceResponse>> UpdatePreferencesAsync(
        Guid? currentUserId,
        Guid? userId,
        UpdateResidentNotificationPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentNotificationPreferenceResponse>.Forbidden("Authentication is required.");
        }

        var targetUserId = userId ?? currentUserId.Value;
        var targetValidation = await ValidatePreferenceTargetAccessAsync(currentUserId.Value, targetUserId, cancellationToken);
        if (targetValidation is not null)
        {
            return ToResult<ResidentNotificationPreferenceResponse>(targetValidation);
        }

        if (request.DoNotDisturbEnabled
            && (!request.DoNotDisturbStartLocalTime.HasValue || !request.DoNotDisturbEndLocalTime.HasValue))
        {
            return ServiceResult<ResidentNotificationPreferenceResponse>.BadRequest("Do-not-disturb start and end times are required when do-not-disturb is enabled.");
        }

        var preference = await GetOrCreatePreferenceAsync(targetUserId, cancellationToken);
        preference.InAppEnabled = request.InAppEnabled;
        preference.EmailEnabled = request.EmailEnabled;
        preference.SmsEnabled = request.SmsEnabled;
        preference.BillNotificationsEnabled = request.BillNotificationsEnabled;
        preference.PaymentNotificationsEnabled = request.PaymentNotificationsEnabled;
        preference.MaintenanceNotificationsEnabled = request.MaintenanceNotificationsEnabled;
        preference.ComplaintNotificationsEnabled = request.ComplaintNotificationsEnabled;
        preference.ViolationNotificationsEnabled = request.ViolationNotificationsEnabled;
        preference.VisitorNotificationsEnabled = request.VisitorNotificationsEnabled;
        preference.DocumentNotificationsEnabled = request.DocumentNotificationsEnabled;
        preference.AnnouncementNotificationsEnabled = request.AnnouncementNotificationsEnabled;
        preference.CampaignNotificationsEnabled = request.CampaignNotificationsEnabled;
        preference.DoNotDisturbEnabled = request.DoNotDisturbEnabled;
        preference.DoNotDisturbStartLocalTime = request.DoNotDisturbEnabled ? request.DoNotDisturbStartLocalTime : null;
        preference.DoNotDisturbEndLocalTime = request.DoNotDisturbEnabled ? request.DoNotDisturbEndLocalTime : null;
        preference.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: null,
            ResidentProfileId: null,
            ActorUserId: currentUserId.Value,
            ActorRole: null,
            ActionType: AuditActionType.NotificationPreferenceUpdated,
            EntityType: AuditEntityType.ResidentNotificationPreference,
            EntityId: preference.Id,
            Severity: AuditSeverity.Low,
            SourceModule: "Communication",
            Description: "Resident notification preferences updated."), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ResidentNotificationPreferenceResponse>.Success(ToPreferenceResponse(preference));
    }

    public async Task<ServiceResult<PagedResult<CommunicationCampaignResponse>>> SearchCampaignsAsync(
        CommunicationCampaignSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query.FromUtc, query.ToUtc);
        if (validation is not null)
        {
            return ServiceResult<PagedResult<CommunicationCampaignResponse>>.BadRequest(validation);
        }

        var campaigns = ApplyCampaignFilters(
            await ApplyCampaignScopeAsync(dbContext.CommunicationCampaigns.AsNoTracking(), cancellationToken),
            query);

        var totalCount = await campaigns.CountAsync(cancellationToken);
        var items = await campaigns
            .OrderByDescending(campaign => campaign.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(campaign => ToCampaignResponse(campaign))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<CommunicationCampaignResponse>>.Success(
            new PagedResult<CommunicationCampaignResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<CommunicationCampaignDetailsResponse>> GetCampaignAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var campaign = await (await ApplyCampaignScopeAsync(dbContext.CommunicationCampaigns.AsNoTracking(), cancellationToken))
            .Include(item => item.Recipients.OrderBy(recipient => recipient.CreatedAtUtc))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (campaign is null)
        {
            return ServiceResult<CommunicationCampaignDetailsResponse>.NotFound("Communication campaign was not found.");
        }

        return ServiceResult<CommunicationCampaignDetailsResponse>.Success(ToCampaignDetailsResponse(campaign));
    }

    public async Task<ServiceResult<CommunicationCampaignResponse>> CreateCampaignAsync(
        Guid? currentUserId,
        CreateCommunicationCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<CommunicationCampaignResponse>.Forbidden("Authentication is required.");
        }

        var validation = await ValidateCampaignRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return ToResult<CommunicationCampaignResponse>(validation);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunicationCampaignResponse>.NotFound("Compound was not found.");
        }

        var campaign = new CommunicationCampaign
        {
            CompoundId = request.CompoundId,
            CreatedByUserId = currentUserId.Value,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            NotificationType = request.NotificationType,
            Severity = request.Severity,
            Priority = request.Priority,
            TargetType = request.TargetType,
            TargetBuildingId = request.TargetBuildingId,
            TargetFloorId = request.TargetFloorId,
            TargetPropertyUnitId = request.TargetPropertyUnitId,
            TargetResidentProfileId = request.TargetResidentProfileId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            Status = CommunicationCampaignStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.CommunicationCampaigns.Add(campaign);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<CommunicationCampaignResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: campaign.CompoundId,
            ResidentProfileId: null,
            ActorUserId: currentUserId.Value,
            ActorRole: null,
            ActionType: AuditActionType.CommunicationCampaignCreated,
            EntityType: AuditEntityType.CommunicationCampaign,
            EntityId: campaign.Id,
            Severity: AuditSeverity.Low,
            SourceModule: "Communication",
            Description: "Communication campaign created."), cancellationToken);
        var auditConcurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<CommunicationCampaignResponse>(cancellationToken);
        if (auditConcurrencyFailure is not null)
        {
            return auditConcurrencyFailure;
        }

        return ServiceResult<CommunicationCampaignResponse>.Success(ToCampaignResponse(campaign));
    }

    public async Task<ServiceResult<CommunicationCampaignDetailsResponse>> SendCampaignAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<CommunicationCampaignDetailsResponse>.Forbidden("Authentication is required.");
        }

        var campaign = await dbContext.CommunicationCampaigns
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (campaign is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(campaign.CompoundId, cancellationToken))
        {
            return ServiceResult<CommunicationCampaignDetailsResponse>.NotFound("Communication campaign was not found.");
        }

        if (campaign.Status is CommunicationCampaignStatus.Sent or CommunicationCampaignStatus.Cancelled)
        {
            return ServiceResult<CommunicationCampaignDetailsResponse>.Conflict("Only draft or queued campaigns can be sent.");
        }

        var recipients = await ResolveRecipientsAsync(campaign, cancellationToken);
        if (recipients.Count == 0)
        {
            return ServiceResult<CommunicationCampaignDetailsResponse>.Conflict("The campaign has no eligible recipients.");
        }

        var now = DateTime.UtcNow;
        var existingRecipients = await dbContext.CommunicationCampaignRecipients
            .Where(recipient => recipient.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);
        if (existingRecipients.Count > 0)
        {
            dbContext.CommunicationCampaignRecipients.RemoveRange(existingRecipients);
        }

        var createdRecipients = new List<CommunicationCampaignRecipient>();
        var outboxCount = 0;

        foreach (var recipient in recipients)
        {
            var preference = await GetOrCreatePreferenceAsync(recipient.UserId, cancellationToken);
            var suppressionReason = GetSuppressionReason(preference, campaign.ScheduledAtUtc ?? now);
            NotificationOutbox? outboxItem = null;

            if (suppressionReason is null)
            {
                var residentNotification = new ResidentNotification
                {
                    UserId = recipient.UserId,
                    Title = campaign.Title,
                    Message = campaign.Body,
                    Type = campaign.NotificationType,
                    Severity = campaign.Severity,
                    RelatedEntityType = nameof(CommunicationCampaign),
                    RelatedEntityId = campaign.Id,
                    CreatedAt = now
                };
                dbContext.ResidentNotifications.Add(residentNotification);

                outboxItem = new NotificationOutbox
                {
                    CompoundId = campaign.CompoundId,
                    ResidentProfileId = recipient.ResidentProfileId,
                    RecipientUserId = recipient.UserId,
                    Channel = NotificationChannel.InApp,
                    EventType = NotificationEventType.CommunicationCampaignSent,
                    Priority = campaign.Priority,
                    RecipientName = recipient.FullName,
                    Subject = campaign.Title,
                    Body = campaign.Body,
                    RelatedEntityType = NotificationRelatedEntityType.CommunicationCampaign,
                    RelatedEntityId = campaign.Id,
                    ScheduledAtUtc = campaign.ScheduledAtUtc ?? now,
                    CreatedByUserId = currentUserId.Value
                };
                dbContext.NotificationOutboxes.Add(outboxItem);
                outboxCount++;
            }

            var campaignRecipient = new CommunicationCampaignRecipient
            {
                CampaignId = campaign.Id,
                ResidentProfileId = recipient.ResidentProfileId,
                UserId = recipient.UserId,
                NotificationOutboxId = outboxItem?.Id,
                DeliverySuppressed = suppressionReason is not null,
                SuppressionReason = Truncate(suppressionReason, MaxSuppressionReasonLength),
                CreatedAtUtc = now
            };
            dbContext.CommunicationCampaignRecipients.Add(campaignRecipient);
            createdRecipients.Add(campaignRecipient);
        }

        campaign.Status = CommunicationCampaignStatus.Sent;
        campaign.SentAtUtc = now;
        campaign.RecipientCount = recipients.Count;
        campaign.OutboxItemCount = outboxCount;
        campaign.UpdatedAtUtc = now;

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: campaign.CompoundId,
            ResidentProfileId: null,
            ActorUserId: currentUserId.Value,
            ActorRole: null,
            ActionType: AuditActionType.CommunicationCampaignSent,
            EntityType: AuditEntityType.CommunicationCampaign,
            EntityId: campaign.Id,
            Severity: AuditSeverity.Medium,
            SourceModule: "Communication",
            Description: $"Communication campaign sent to {recipients.Count} recipient(s)."), cancellationToken);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<CommunicationCampaignDetailsResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        campaign.Recipients = createdRecipients;

        return ServiceResult<CommunicationCampaignDetailsResponse>.Success(ToCampaignDetailsResponse(campaign));
    }

    public async Task<ServiceResult<CommunicationDeliveryAnalyticsResponse>> GetDeliveryAnalyticsAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<CommunicationDeliveryAnalyticsResponse>.NotFound("Communication analytics were not found.");
        }

        var campaigns = await ApplyCampaignScopeAsync(dbContext.CommunicationCampaigns.AsNoTracking(), cancellationToken);
        var outboxes = await ApplyOutboxScopeAsync(dbContext.NotificationOutboxes.AsNoTracking(), cancellationToken);
        var recipients = dbContext.CommunicationCampaignRecipients.AsNoTracking();

        if (compoundId.HasValue)
        {
            campaigns = campaigns.Where(campaign => campaign.CompoundId == compoundId.Value);
            outboxes = outboxes.Where(outbox => outbox.CompoundId == compoundId.Value);
            recipients = recipients.Where(recipient => recipient.Campaign.CompoundId == compoundId.Value);
        }

        var response = new CommunicationDeliveryAnalyticsResponse(
            compoundId,
            await campaigns.CountAsync(cancellationToken),
            await campaigns.CountAsync(campaign => campaign.Status == CommunicationCampaignStatus.Sent, cancellationToken),
            await recipients.CountAsync(cancellationToken),
            await recipients.CountAsync(recipient => recipient.DeliverySuppressed, cancellationToken),
            await outboxes.CountAsync(outbox => outbox.RelatedEntityType == NotificationRelatedEntityType.CommunicationCampaign, cancellationToken),
            await outboxes.CountAsync(outbox => outbox.RelatedEntityType == NotificationRelatedEntityType.CommunicationCampaign && outbox.Status == NotificationStatus.Pending, cancellationToken),
            await outboxes.CountAsync(outbox => outbox.RelatedEntityType == NotificationRelatedEntityType.CommunicationCampaign && outbox.Status == NotificationStatus.Sent, cancellationToken),
            await outboxes.CountAsync(outbox => outbox.RelatedEntityType == NotificationRelatedEntityType.CommunicationCampaign && outbox.Status == NotificationStatus.Failed, cancellationToken));

        return ServiceResult<CommunicationDeliveryAnalyticsResponse>.Success(response);
    }

    private async Task<ResidentNotificationPreference> GetOrCreatePreferenceAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var preference = await dbContext.ResidentNotificationPreferences
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is not null)
        {
            return preference;
        }

        preference = new ResidentNotificationPreference
        {
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.ResidentNotificationPreferences.Add(preference);
        return preference;
    }

    private async Task<ValidationFailure?> ValidatePreferenceTargetAccessAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (targetUserId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Target user id is invalid.");
        }

        if (targetUserId == currentUserId)
        {
            return null;
        }

        var targetExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == targetUserId, cancellationToken);
        if (!targetExists)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Target user was not found.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return new ValidationFailure(ServiceResultStatus.Forbidden, "Authentication is required.");
        }

        if (scope.IsSuperAdmin)
        {
            return null;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return new ValidationFailure(ServiceResultStatus.Forbidden, "You do not have access to the target user.");
        }

        var allowedViaResidentProfile = await dbContext.ResidentProfiles
            .AsNoTracking()
            .AnyAsync(profile =>
                profile.UserId == targetUserId
                && scope.AllowedCompoundIds.Contains(profile.CompoundId),
                cancellationToken);
        if (allowedViaResidentProfile)
        {
            return null;
        }

        var allowedViaAssignment = await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .AnyAsync(assignment =>
                assignment.UserId == targetUserId
                && assignment.IsActive
                && scope.AllowedCompoundIds.Contains(assignment.CompoundId),
                cancellationToken);

        return allowedViaAssignment
            ? null
            : new ValidationFailure(ServiceResultStatus.Forbidden, "You do not have access to the target user.");
    }

    private async Task<ValidationFailure?> ValidateCampaignRequestAsync(
        CreateCommunicationCampaignRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitleLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Campaign title is required and cannot exceed 150 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > MaxBodyLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Campaign body is required and cannot exceed 4000 characters.");
        }

        if (!Enum.IsDefined(request.TargetType))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Campaign target type is invalid.");
        }

        if (!await dbContext.Compounds.AsNoTracking().AnyAsync(compound => compound.Id == request.CompoundId && compound.IsActive, cancellationToken))
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        return request.TargetType switch
        {
            CommunicationCampaignTargetType.Building when !request.TargetBuildingId.HasValue => new ValidationFailure(ServiceResultStatus.BadRequest, "Target building id is required."),
            CommunicationCampaignTargetType.Floor when !request.TargetFloorId.HasValue => new ValidationFailure(ServiceResultStatus.BadRequest, "Target floor id is required."),
            CommunicationCampaignTargetType.Unit when !request.TargetPropertyUnitId.HasValue => new ValidationFailure(ServiceResultStatus.BadRequest, "Target property unit id is required."),
            CommunicationCampaignTargetType.Resident when !request.TargetResidentProfileId.HasValue => new ValidationFailure(ServiceResultStatus.BadRequest, "Target resident profile id is required."),
            CommunicationCampaignTargetType.Building => await ValidateTargetBuildingAsync(request.CompoundId, request.TargetBuildingId!.Value, cancellationToken),
            CommunicationCampaignTargetType.Floor => await ValidateTargetFloorAsync(request.CompoundId, request.TargetFloorId!.Value, cancellationToken),
            CommunicationCampaignTargetType.Unit => await ValidateTargetPropertyUnitAsync(request.CompoundId, request.TargetPropertyUnitId!.Value, cancellationToken),
            CommunicationCampaignTargetType.Resident => await ValidateTargetResidentAsync(request.CompoundId, request.TargetResidentProfileId!.Value, cancellationToken),
            _ => null
        };
    }

    private async Task<ValidationFailure?> ValidateTargetBuildingAsync(
        Guid compoundId,
        Guid buildingId,
        CancellationToken cancellationToken)
    {
        var targetCompoundId = await dbContext.Buildings
            .AsNoTracking()
            .Where(building => building.Id == buildingId)
            .Select(building => (Guid?)building.CompoundId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!targetCompoundId.HasValue)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Target building was not found.");
        }

        return targetCompoundId.Value == compoundId
            ? null
            : new ValidationFailure(ServiceResultStatus.BadRequest, "Target building must belong to the campaign compound.");
    }

    private async Task<ValidationFailure?> ValidateTargetFloorAsync(
        Guid compoundId,
        Guid floorId,
        CancellationToken cancellationToken)
    {
        var targetCompoundId = await dbContext.Floors
            .AsNoTracking()
            .Where(floor => floor.Id == floorId)
            .Select(floor => (Guid?)floor.CompoundId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!targetCompoundId.HasValue)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Target floor was not found.");
        }

        return targetCompoundId.Value == compoundId
            ? null
            : new ValidationFailure(ServiceResultStatus.BadRequest, "Target floor must belong to the campaign compound.");
    }

    private async Task<ValidationFailure?> ValidateTargetPropertyUnitAsync(
        Guid compoundId,
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        var targetCompoundId = await dbContext.PropertyUnits
            .AsNoTracking()
            .Where(unit => unit.Id == propertyUnitId)
            .Select(unit => (Guid?)unit.CompoundId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!targetCompoundId.HasValue)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Target property unit was not found.");
        }

        return targetCompoundId.Value == compoundId
            ? null
            : new ValidationFailure(ServiceResultStatus.BadRequest, "Target property unit must belong to the campaign compound.");
    }

    private async Task<ValidationFailure?> ValidateTargetResidentAsync(
        Guid compoundId,
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        var targetCompoundId = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(resident => resident.Id == residentProfileId)
            .Select(resident => (Guid?)resident.CompoundId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!targetCompoundId.HasValue)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Target resident profile was not found.");
        }

        return targetCompoundId.Value == compoundId
            ? null
            : new ValidationFailure(ServiceResultStatus.BadRequest, "Target resident profile must belong to the campaign compound.");
    }

    private async Task<List<CampaignRecipientCandidate>> ResolveRecipientsAsync(
        CommunicationCampaign campaign,
        CancellationToken cancellationToken)
    {
        var residents = dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(resident => resident.CompoundId == campaign.CompoundId && resident.IsActive);

        switch (campaign.TargetType)
        {
            case CommunicationCampaignTargetType.Building:
                residents = residents.Where(resident => resident.OccupancyRecords.Any(record =>
                    record.OccupancyStatus == OccupancyStatus.Active
                    && record.PropertyUnit.BuildingId == campaign.TargetBuildingId));
                break;
            case CommunicationCampaignTargetType.Floor:
                residents = residents.Where(resident => resident.OccupancyRecords.Any(record =>
                    record.OccupancyStatus == OccupancyStatus.Active
                    && record.PropertyUnit.FloorId == campaign.TargetFloorId));
                break;
            case CommunicationCampaignTargetType.Unit:
                residents = residents.Where(resident => resident.OccupancyRecords.Any(record =>
                    record.OccupancyStatus == OccupancyStatus.Active
                    && record.PropertyUnitId == campaign.TargetPropertyUnitId));
                break;
            case CommunicationCampaignTargetType.Resident:
                residents = residents.Where(resident => resident.Id == campaign.TargetResidentProfileId);
                break;
            case CommunicationCampaignTargetType.OverdueResidents:
                residents = residents.Where(resident =>
                    dbContext.UtilityBills.AsNoTracking().Any(bill =>
                        bill.ResidentProfileId == resident.Id && bill.BillStatus == BillStatus.Overdue)
                    || dbContext.RentInvoices.AsNoTracking().Any(invoice =>
                        invoice.ResidentProfileId == resident.Id && invoice.RentInvoiceStatus == RentInvoiceStatus.Overdue)
                    || dbContext.InstallmentScheduleItems.AsNoTracking().Any(item =>
                        item.ResidentProfileId == resident.Id && item.InstallmentStatus == InstallmentStatus.Overdue));
                break;
            case CommunicationCampaignTargetType.RiskFlagResidents:
                residents = residents.Where(resident => dbContext.ResidentRiskFlags.AsNoTracking().Any(flag =>
                    flag.ResidentProfileId == resident.Id
                    && (flag.Status == ResidentRiskFlagStatus.Active || flag.Status == ResidentRiskFlagStatus.Monitoring)));
                break;
        }

        return await residents
            .OrderBy(resident => resident.FullName)
            .Select(resident => new CampaignRecipientCandidate(resident.Id, resident.UserId, resident.FullName))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<IQueryable<CommunicationCampaign>> ApplyCampaignScopeAsync(
        IQueryable<CommunicationCampaign> campaigns,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return campaigns.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return campaigns;
        }

        return scope.AllowedCompoundIds.Length == 0
            ? campaigns.Where(_ => false)
            : campaigns.Where(campaign => scope.AllowedCompoundIds.Contains(campaign.CompoundId));
    }

    private async Task<IQueryable<NotificationOutbox>> ApplyOutboxScopeAsync(
        IQueryable<NotificationOutbox> outboxes,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return outboxes.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return outboxes;
        }

        return scope.AllowedCompoundIds.Length == 0
            ? outboxes.Where(_ => false)
            : outboxes.Where(outbox => outbox.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(outbox.CompoundId.Value));
    }

    private static IQueryable<CommunicationCampaign> ApplyCampaignFilters(
        IQueryable<CommunicationCampaign> campaigns,
        CommunicationCampaignSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            campaigns = campaigns.Where(campaign => campaign.CompoundId == query.CompoundId.Value);
        }

        if (query.Status.HasValue)
        {
            campaigns = campaigns.Where(campaign => campaign.Status == query.Status.Value);
        }

        if (query.TargetType.HasValue)
        {
            campaigns = campaigns.Where(campaign => campaign.TargetType == query.TargetType.Value);
        }

        if (query.FromUtc.HasValue)
        {
            campaigns = campaigns.Where(campaign => campaign.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            campaigns = campaigns.Where(campaign => campaign.CreatedAtUtc <= query.ToUtc.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            campaigns = campaigns.Where(campaign => campaign.Title.Contains(searchTerm) || campaign.Body.Contains(searchTerm));
        }

        return campaigns;
    }

    private static string? GetSuppressionReason(ResidentNotificationPreference preference, DateTime scheduledAtUtc)
    {
        if (!preference.InAppEnabled)
        {
            return "In-app notifications are disabled.";
        }

        if (!preference.CampaignNotificationsEnabled)
        {
            return "Campaign notifications are disabled.";
        }

        if (!preference.DoNotDisturbEnabled
            || !preference.DoNotDisturbStartLocalTime.HasValue
            || !preference.DoNotDisturbEndLocalTime.HasValue)
        {
            return null;
        }

        var localTime = scheduledAtUtc.TimeOfDay;
        var start = preference.DoNotDisturbStartLocalTime.Value;
        var end = preference.DoNotDisturbEndLocalTime.Value;
        var isInsideWindow = start <= end
            ? localTime >= start && localTime <= end
            : localTime >= start || localTime <= end;

        return isInsideWindow ? "Do-not-disturb window is active." : null;
    }


    private async Task<ServiceResult<T>?> SaveChangesWithConcurrencyGuardAsync<T>(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<T>.Conflict("The communication campaign was updated by another operation. Reload and try again.");
        }
    }

    private static string? ValidateDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        return fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value
            ? "Start date cannot be after end date."
            : null;
    }

    private static CommunicationCampaignResponse ToCampaignResponse(CommunicationCampaign campaign)
    {
        return new CommunicationCampaignResponse(
            campaign.Id,
            campaign.CompoundId,
            campaign.CreatedByUserId,
            campaign.Title,
            campaign.Body,
            campaign.NotificationType,
            campaign.Severity,
            campaign.Priority,
            campaign.TargetType,
            campaign.TargetBuildingId,
            campaign.TargetFloorId,
            campaign.TargetPropertyUnitId,
            campaign.TargetResidentProfileId,
            campaign.Status,
            campaign.ScheduledAtUtc,
            campaign.SentAtUtc,
            campaign.RecipientCount,
            campaign.OutboxItemCount,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc);
    }

    private static CommunicationCampaignDetailsResponse ToCampaignDetailsResponse(CommunicationCampaign campaign)
    {
        return new CommunicationCampaignDetailsResponse(
            campaign.Id,
            campaign.CompoundId,
            campaign.CreatedByUserId,
            campaign.Title,
            campaign.Body,
            campaign.NotificationType,
            campaign.Severity,
            campaign.Priority,
            campaign.TargetType,
            campaign.TargetBuildingId,
            campaign.TargetFloorId,
            campaign.TargetPropertyUnitId,
            campaign.TargetResidentProfileId,
            campaign.Status,
            campaign.ScheduledAtUtc,
            campaign.SentAtUtc,
            campaign.RecipientCount,
            campaign.OutboxItemCount,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.Recipients.Select(ToRecipientResponse).ToArray());
    }

    private static CommunicationCampaignRecipientResponse ToRecipientResponse(CommunicationCampaignRecipient recipient)
    {
        return new CommunicationCampaignRecipientResponse(
            recipient.Id,
            recipient.ResidentProfileId,
            recipient.UserId,
            recipient.NotificationOutboxId,
            recipient.DeliverySuppressed,
            recipient.SuppressionReason,
            recipient.CreatedAtUtc);
    }

    private static ResidentNotificationPreferenceResponse ToPreferenceResponse(ResidentNotificationPreference preference)
    {
        return new ResidentNotificationPreferenceResponse(
            preference.Id,
            preference.UserId,
            preference.InAppEnabled,
            preference.EmailEnabled,
            preference.SmsEnabled,
            preference.BillNotificationsEnabled,
            preference.PaymentNotificationsEnabled,
            preference.MaintenanceNotificationsEnabled,
            preference.ComplaintNotificationsEnabled,
            preference.ViolationNotificationsEnabled,
            preference.VisitorNotificationsEnabled,
            preference.DocumentNotificationsEnabled,
            preference.AnnouncementNotificationsEnabled,
            preference.CampaignNotificationsEnabled,
            preference.DoNotDisturbEnabled,
            preference.DoNotDisturbStartLocalTime,
            preference.DoNotDisturbEndLocalTime,
            preference.CreatedAtUtc,
            preference.UpdatedAtUtc);
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validation)
    {
        return validation.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validation.Message),
            ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(validation.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validation.Message),
            _ => ServiceResult<T>.BadRequest(validation.Message)
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);

    private sealed record CampaignRecipientCandidate(Guid ResidentProfileId, Guid UserId, string FullName);
}


