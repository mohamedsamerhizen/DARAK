using System.Text.Json;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.RiskFlags;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentRiskFlagService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    UserManager<ApplicationUser> userManager)
    : IResidentRiskFlagService
{
    private static readonly UserRole[] ReaderRoles =
    [
        UserRole.SuperAdmin,
        UserRole.CompoundAdmin,
        UserRole.Accountant
    ];

    private static readonly UserRole[] ManagerRoles =
    [
        UserRole.SuperAdmin,
        UserRole.CompoundAdmin,
        UserRole.Accountant
    ];

    private static readonly UserRole[] ClosureRoles =
    [
        UserRole.SuperAdmin,
        UserRole.CompoundAdmin
    ];

    public async Task<ServiceResult<ResidentRiskFlagResponse>> CreateFlagAsync(
        Guid? currentUserId,
        CreateResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagResponse>(currentUserId, ManagerRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Compound id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.CanAccess(request.CompoundId))
        {
            return ServiceResult<ResidentRiskFlagResponse>.Forbidden("You do not have access to this compound.");
        }

        var validation = await ValidateCreateRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        if (request.AssignedToUserId.HasValue)
        {
            var assignmentValidation = await ValidateAssignableUserAsync(
                request.AssignedToUserId.Value,
                request.CompoundId,
                cancellationToken);
            if (assignmentValidation is not null)
            {
                return ServiceResult<ResidentRiskFlagResponse>.BadRequest(assignmentValidation);
            }
        }

        var now = DateTime.UtcNow;
        var riskFlag = new ResidentRiskFlag
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            PropertyUnitId = request.PropertyUnitId,
            CreatedByUserId = currentUserId!.Value,
            AssignedToUserId = request.AssignedToUserId,
            FlagType = request.FlagType,
            Severity = request.Severity,
            Status = ResidentRiskFlagStatus.Active,
            Source = request.Source,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            RecommendedAction = NormalizeOptional(request.RecommendedAction),
            InternalNotes = NormalizeOptional(request.InternalNotes),
            MetadataJson = NormalizeOptional(request.MetadataJson),
            RequiresSupervisorReview = request.RequiresSupervisorReview,
            CreatedAtUtc = now,
            AssignedAtUtc = request.AssignedToUserId.HasValue ? now : null,
            NextReviewAtUtc = request.NextReviewAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc
        };

        dbContext.ResidentRiskFlags.Add(riskFlag);
        AddAction(
            riskFlag,
            currentUserId.Value,
            ResidentRiskFlagActionType.Created,
            null,
            ResidentRiskFlagStatus.Active,
            null,
            request.Severity,
            "Risk flag created.");
        AddActivityEvent(
            riskFlag,
            currentUserId.Value,
            ActivityEventType.RiskFlagCreated,
            "Resident risk flag created",
            $"Risk flag {request.FlagType} was created with severity {request.Severity}.");
        AddNotification(
            riskFlag,
            currentUserId.Value,
            request.AssignedToUserId,
            request.AssignedToUserId.HasValue ? "Assigned admin" : "Risk flag watcher",
            NotificationEventType.RiskFlagCreated,
            "Resident risk flag created",
            $"{request.Severity} risk flag created: {riskFlag.Title}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentRiskFlagResponse>.Success(ToResponse(riskFlag));
    }

    public async Task<ServiceResult<PagedResult<ResidentRiskFlagResponse>>> SearchFlagsAsync(
        Guid? currentUserId,
        ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<PagedResult<ResidentRiskFlagResponse>>(currentUserId, ReaderRoles);
        if (auth is not null)
        {
            return auth;
        }

        var validation = ValidateSearchQuery(query);
        if (validation is not null)
        {
            return ServiceResult<PagedResult<ResidentRiskFlagResponse>>.BadRequest(validation);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var riskFlags = ApplySearchFilters(
            dbContext.ResidentRiskFlags
                .AsNoTracking()
                .ApplyCompoundAccess(scope, riskFlag => riskFlag.CompoundId),
            query);

        return ServiceResult<PagedResult<ResidentRiskFlagResponse>>.Success(
            await ToPagedResultAsync(riskFlags, query, cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<ResidentRiskFlagResponse>>> GetResidentFlagsAsync(
        Guid? currentUserId,
        Guid residentProfileId,
        ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<PagedResult<ResidentRiskFlagResponse>>(currentUserId, ReaderRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (residentProfileId == Guid.Empty)
        {
            return ServiceResult<PagedResult<ResidentRiskFlagResponse>>.BadRequest("Resident profile id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .ApplyCompoundAccess(scope, profile => profile.CompoundId)
            .SingleOrDefaultAsync(profile => profile.Id == residentProfileId, cancellationToken);
        if (resident is null)
        {
            return ServiceResult<PagedResult<ResidentRiskFlagResponse>>.NotFound("Resident profile was not found.");
        }

        if (query.ResidentProfileId.HasValue && query.ResidentProfileId.Value != residentProfileId)
        {
            return ServiceResult<PagedResult<ResidentRiskFlagResponse>>.BadRequest("Resident filter conflicts with the route resident id.");
        }

        var scopedQuery = new ResidentRiskFlagSearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            CompoundId = query.CompoundId ?? resident.CompoundId,
            ResidentProfileId = residentProfileId,
            PropertyUnitId = query.PropertyUnitId,
            AssignedToUserId = query.AssignedToUserId,
            FlagType = query.FlagType,
            Severity = query.Severity,
            Status = query.Status,
            Source = query.Source,
            RequiresSupervisorReview = query.RequiresSupervisorReview,
            OverdueReviewOnly = query.OverdueReviewOnly,
            ActiveOnly = query.ActiveOnly,
            SearchTerm = query.SearchTerm
        };

        return await SearchFlagsAsync(currentUserId, scopedQuery, cancellationToken);
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> GetDetailsAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDetailsResponse>(currentUserId, ReaderRoles);
        if (auth is not null)
        {
            return auth;
        }

        var riskFlag = await GetScopedFlagWithActionsAsync(id, cancellationToken);
        if (riskFlag is null)
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.NotFound("Resident risk flag was not found.");
        }

        await ExpireIfNeededAsync(riskFlag, currentUserId!.Value, cancellationToken);
        return ServiceResult<ResidentRiskFlagDetailsResponse>.Success(ToDetailsResponse(riskFlag));
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> AssignAsync(
        Guid? currentUserId,
        Guid id,
        AssignResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDetailsResponse>(currentUserId, ManagerRoles);
        if (auth is not null)
        {
            return auth;
        }

        var riskFlag = await GetScopedFlagWithActionsAsync(id, cancellationToken);
        if (riskFlag is null)
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.NotFound("Resident risk flag was not found.");
        }

        await ExpireIfNeededAsync(riskFlag, currentUserId!.Value, cancellationToken);
        if (!IsOpen(riskFlag.Status))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.Conflict("Only open risk flags can be assigned.");
        }

        if (request.AssignedToUserId.HasValue)
        {
            var assignmentValidation = await ValidateAssignableUserAsync(
                request.AssignedToUserId.Value,
                riskFlag.CompoundId,
                cancellationToken);
            if (assignmentValidation is not null)
            {
                return ServiceResult<ResidentRiskFlagDetailsResponse>.BadRequest(assignmentValidation);
            }
        }

        var now = DateTime.UtcNow;
        riskFlag.AssignedToUserId = request.AssignedToUserId;
        riskFlag.AssignedAtUtc = request.AssignedToUserId.HasValue ? now : null;
        riskFlag.UpdatedAtUtc = now;

        AddAction(
            riskFlag,
            currentUserId.Value,
            ResidentRiskFlagActionType.Assigned,
            riskFlag.Status,
            riskFlag.Status,
            null,
            null,
            string.IsNullOrWhiteSpace(request.Notes) ? "Risk flag assignment updated." : request.Notes.Trim());
        AddActivityEvent(
            riskFlag,
            currentUserId.Value,
            ActivityEventType.RiskFlagAssigned,
            "Resident risk flag assigned",
            request.AssignedToUserId.HasValue ? "Risk flag assignment was updated." : "Risk flag was unassigned.");
        AddNotification(
            riskFlag,
            currentUserId.Value,
            request.AssignedToUserId,
            "Assigned admin",
            NotificationEventType.RiskFlagAssigned,
            "Resident risk flag assigned",
            $"Risk flag assigned: {riskFlag.Title}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentRiskFlagDetailsResponse>.Success(ToDetailsResponse(riskFlag));
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> ChangeSeverityAsync(
        Guid? currentUserId,
        Guid id,
        ChangeResidentRiskFlagSeverityRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDetailsResponse>(currentUserId, ManagerRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.BadRequest("Severity change notes are required.");
        }

        var riskFlag = await GetScopedFlagWithActionsAsync(id, cancellationToken);
        if (riskFlag is null)
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.NotFound("Resident risk flag was not found.");
        }

        await ExpireIfNeededAsync(riskFlag, currentUserId!.Value, cancellationToken);
        if (!IsOpen(riskFlag.Status))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.Conflict("Only open risk flags can have severity changes.");
        }

        var previousSeverity = riskFlag.Severity;
        riskFlag.Severity = request.Severity;
        riskFlag.UpdatedAtUtc = DateTime.UtcNow;

        AddAction(
            riskFlag,
            currentUserId.Value,
            ResidentRiskFlagActionType.SeverityChanged,
            riskFlag.Status,
            riskFlag.Status,
            previousSeverity,
            request.Severity,
            request.Notes.Trim());
        AddActivityEvent(
            riskFlag,
            currentUserId.Value,
            ActivityEventType.RiskFlagSeverityChanged,
            "Resident risk flag severity changed",
            $"Risk flag severity changed from {previousSeverity} to {request.Severity}.");
        AddNotification(
            riskFlag,
            currentUserId.Value,
            riskFlag.AssignedToUserId,
            "Assigned admin",
            NotificationEventType.RiskFlagSeverityChanged,
            "Resident risk flag severity changed",
            $"Risk flag severity changed to {request.Severity}: {riskFlag.Title}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentRiskFlagDetailsResponse>.Success(ToDetailsResponse(riskFlag));
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> MarkReviewedAsync(
        Guid? currentUserId,
        Guid id,
        ReviewResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDetailsResponse>(currentUserId, ManagerRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.BadRequest("Review notes are required.");
        }

        var riskFlag = await GetScopedFlagWithActionsAsync(id, cancellationToken);
        if (riskFlag is null)
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.NotFound("Resident risk flag was not found.");
        }

        await ExpireIfNeededAsync(riskFlag, currentUserId!.Value, cancellationToken);
        if (!IsOpen(riskFlag.Status))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.Conflict("Only open risk flags can be reviewed.");
        }

        var now = DateTime.UtcNow;
        var previousStatus = riskFlag.Status;
        riskFlag.Status = ResidentRiskFlagStatus.Monitoring;
        riskFlag.LastReviewedByUserId = currentUserId.Value;
        riskFlag.LastReviewedAtUtc = now;
        riskFlag.NextReviewAtUtc = request.NextReviewAtUtc;
        riskFlag.UpdatedAtUtc = now;

        AddAction(
            riskFlag,
            currentUserId.Value,
            ResidentRiskFlagActionType.Reviewed,
            previousStatus,
            ResidentRiskFlagStatus.Monitoring,
            null,
            null,
            request.Notes.Trim());
        AddActivityEvent(
            riskFlag,
            currentUserId.Value,
            ActivityEventType.RiskFlagReviewed,
            "Resident risk flag reviewed",
            "Risk flag was reviewed and moved to monitoring.");

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentRiskFlagDetailsResponse>.Success(ToDetailsResponse(riskFlag));
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> ResolveAsync(
        Guid? currentUserId,
        Guid id,
        CloseResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CloseAsync(
            currentUserId,
            id,
            request,
            ResidentRiskFlagStatus.Resolved,
            ResidentRiskFlagActionType.Resolved,
            ActivityEventType.RiskFlagResolved,
            NotificationEventType.RiskFlagResolved,
            cancellationToken);
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> DismissAsync(
        Guid? currentUserId,
        Guid id,
        CloseResidentRiskFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CloseAsync(
            currentUserId,
            id,
            request,
            ResidentRiskFlagStatus.Dismissed,
            ResidentRiskFlagActionType.Dismissed,
            ActivityEventType.RiskFlagDismissed,
            NotificationEventType.RiskFlagDismissed,
            cancellationToken);
    }

    public async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> AddNoteAsync(
        Guid? currentUserId,
        Guid id,
        AddResidentRiskFlagNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDetailsResponse>(currentUserId, ManagerRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.BadRequest("Note text is required.");
        }

        var riskFlag = await GetScopedFlagWithActionsAsync(id, cancellationToken);
        if (riskFlag is null)
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.NotFound("Resident risk flag was not found.");
        }

        await ExpireIfNeededAsync(riskFlag, currentUserId!.Value, cancellationToken);
        riskFlag.UpdatedAtUtc = DateTime.UtcNow;

        AddAction(
            riskFlag,
            currentUserId.Value,
            ResidentRiskFlagActionType.NoteAdded,
            riskFlag.Status,
            riskFlag.Status,
            null,
            null,
            request.Notes.Trim());
        AddActivityEvent(
            riskFlag,
            currentUserId.Value,
            ActivityEventType.RiskFlagNoteAdded,
            "Resident risk flag note added",
            "Internal note was added to a resident risk flag.");

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentRiskFlagDetailsResponse>.Success(ToDetailsResponse(riskFlag));
    }

    public async Task<ServiceResult<ResidentRiskFlagDashboardResponse>> GetDashboardAsync(
        Guid? currentUserId,
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDashboardResponse>(currentUserId, ReaderRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (compoundId == Guid.Empty)
        {
            return ServiceResult<ResidentRiskFlagDashboardResponse>.BadRequest("Compound id is invalid.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<ResidentRiskFlagDashboardResponse>.Forbidden("You do not have access to this compound.");
        }

        var now = DateTime.UtcNow;
        var expiringSoonThreshold = now.AddDays(7);
        var riskFlags = dbContext.ResidentRiskFlags
            .AsNoTracking()
            .ApplyCompoundAccess(scope, riskFlag => riskFlag.CompoundId);

        if (compoundId.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.CompoundId == compoundId.Value);
        }

        var activeCount = await riskFlags.CountAsync(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Active, cancellationToken);
        var monitoringCount = await riskFlags.CountAsync(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Monitoring, cancellationToken);
        var resolvedCount = await riskFlags.CountAsync(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Resolved, cancellationToken);
        var dismissedCount = await riskFlags.CountAsync(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Dismissed, cancellationToken);
        var expiredCount = await riskFlags.CountAsync(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Expired, cancellationToken);
        var highOrCriticalActiveCount = await riskFlags.CountAsync(riskFlag =>
            (riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            && riskFlag.Severity >= ResidentRiskFlagSeverity.High,
            cancellationToken);
        var criticalActiveCount = await riskFlags.CountAsync(riskFlag =>
            (riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            && riskFlag.Severity == ResidentRiskFlagSeverity.Critical,
            cancellationToken);
        var overdueReviewCount = await riskFlags.CountAsync(riskFlag =>
            (riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            && riskFlag.NextReviewAtUtc.HasValue
            && riskFlag.NextReviewAtUtc < now,
            cancellationToken);
        var expiringSoonCount = await riskFlags.CountAsync(riskFlag =>
            (riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            && riskFlag.ExpiresAtUtc.HasValue
            && riskFlag.ExpiresAtUtc >= now
            && riskFlag.ExpiresAtUtc <= expiringSoonThreshold,
            cancellationToken);
        var unassignedActiveCount = await riskFlags.CountAsync(riskFlag =>
            (riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            && !riskFlag.AssignedToUserId.HasValue,
            cancellationToken);
        var oldestActiveCreatedAtUtc = await riskFlags
            .Where(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            .OrderBy(riskFlag => riskFlag.CreatedAtUtc)
            .Select(riskFlag => (DateTime?)riskFlag.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var openBySeverityRows = await riskFlags
            .Where(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
            .GroupBy(riskFlag => riskFlag.Severity)
            .Select(group => new
            {
                Severity = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var openBySeverity = openBySeverityRows
            .OrderByDescending(item => item.Severity)
            .Select(item => new ResidentRiskFlagSeverityCountResponse(item.Severity, item.Count))
            .ToArray();

        return ServiceResult<ResidentRiskFlagDashboardResponse>.Success(new ResidentRiskFlagDashboardResponse(
            activeCount,
            monitoringCount,
            resolvedCount,
            dismissedCount,
            expiredCount,
            highOrCriticalActiveCount,
            criticalActiveCount,
            overdueReviewCount,
            expiringSoonCount,
            unassignedActiveCount,
            oldestActiveCreatedAtUtc,
            openBySeverity));
    }

    private async Task<ServiceResult<ResidentRiskFlagDetailsResponse>> CloseAsync(
        Guid? currentUserId,
        Guid id,
        CloseResidentRiskFlagRequest request,
        ResidentRiskFlagStatus finalStatus,
        ResidentRiskFlagActionType actionType,
        ActivityEventType activityEventType,
        NotificationEventType notificationEventType,
        CancellationToken cancellationToken)
    {
        var auth = await ValidateCurrentUserAsync<ResidentRiskFlagDetailsResponse>(currentUserId, ClosureRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.BadRequest("Close reason is required.");
        }

        var riskFlag = await GetScopedFlagWithActionsAsync(id, cancellationToken);
        if (riskFlag is null)
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.NotFound("Resident risk flag was not found.");
        }

        await ExpireIfNeededAsync(riskFlag, currentUserId!.Value, cancellationToken);
        if (!IsOpen(riskFlag.Status))
        {
            return ServiceResult<ResidentRiskFlagDetailsResponse>.Conflict("Risk flag is already closed.");
        }

        var previousStatus = riskFlag.Status;
        var now = DateTime.UtcNow;
        riskFlag.Status = finalStatus;
        riskFlag.ClosedByUserId = currentUserId.Value;
        riskFlag.UpdatedAtUtc = now;

        if (finalStatus == ResidentRiskFlagStatus.Resolved)
        {
            riskFlag.ResolvedAtUtc = now;
            riskFlag.ResolutionNotes = request.Reason.Trim();
        }
        else
        {
            riskFlag.DismissedAtUtc = now;
            riskFlag.DismissalReason = request.Reason.Trim();
        }

        AddAction(
            riskFlag,
            currentUserId.Value,
            actionType,
            previousStatus,
            finalStatus,
            null,
            null,
            request.Reason.Trim());
        AddActivityEvent(
            riskFlag,
            currentUserId.Value,
            activityEventType,
            finalStatus == ResidentRiskFlagStatus.Resolved
                ? "Resident risk flag resolved"
                : "Resident risk flag dismissed",
            finalStatus == ResidentRiskFlagStatus.Resolved
                ? "Risk flag was resolved."
                : "Risk flag was dismissed.");
        AddNotification(
            riskFlag,
            currentUserId.Value,
            riskFlag.AssignedToUserId,
            "Assigned admin",
            notificationEventType,
            finalStatus == ResidentRiskFlagStatus.Resolved
                ? "Resident risk flag resolved"
                : "Resident risk flag dismissed",
            $"Risk flag closed: {riskFlag.Title}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentRiskFlagDetailsResponse>.Success(ToDetailsResponse(riskFlag));
    }

    private async Task<ServiceResult<ResidentRiskFlagResponse>?> ValidateCreateRequestAsync(
        CreateResidentRiskFlagRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Compound id is required.");
        }

        if (request.ResidentProfileId == Guid.Empty)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Resident profile id is required.");
        }

        if (request.PropertyUnitId == Guid.Empty)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Property unit id is invalid.");
        }

        if (request.SourceEntityId == Guid.Empty)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Source entity id is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Risk flag title is required.");
        }

        if (request.Title.Length > 200)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Risk flag title cannot exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Risk flag description is required.");
        }

        if (request.Description.Length > 1500)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Risk flag description cannot exceed 1500 characters.");
        }

        if (request.RecommendedAction?.Length > 1000)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Recommended action cannot exceed 1000 characters.");
        }

        if (request.InternalNotes?.Length > 2000)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Internal notes cannot exceed 2000 characters.");
        }

        var metadataValidation = ValidateMetadata(request.MetadataJson);
        if (metadataValidation is not null)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest(metadataValidation);
        }

        if (request.NextReviewAtUtc.HasValue && request.NextReviewAtUtc.Value <= DateTime.UtcNow.AddMinutes(-1))
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Next review time must be in the future.");
        }

        if (request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value <= DateTime.UtcNow.AddMinutes(-1))
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Expiry time must be in the future.");
        }

        if (request.SourceEntityType == ResidentRiskFlagSourceEntityType.None && request.SourceEntityId.HasValue)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Source entity id cannot be provided when source entity type is None.");
        }

        if (request.SourceEntityType != ResidentRiskFlagSourceEntityType.None && !request.SourceEntityId.HasValue)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Source entity id is required for the selected source entity type.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == request.CompoundId && compound.IsActive, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<ResidentRiskFlagResponse>.NotFound("Compound was not found.");
        }

        var residentMatchesCompound = await dbContext.ResidentProfiles
            .AsNoTracking()
            .AnyAsync(resident => resident.Id == request.ResidentProfileId
                && resident.CompoundId == request.CompoundId
                && resident.IsActive,
                cancellationToken);
        if (!residentMatchesCompound)
        {
            return ServiceResult<ResidentRiskFlagResponse>.NotFound("Resident profile was not found in the selected compound.");
        }

        if (request.PropertyUnitId.HasValue)
        {
            var unitMatchesCompound = await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(unit => unit.Id == request.PropertyUnitId.Value
                    && unit.CompoundId == request.CompoundId,
                    cancellationToken);
            if (!unitMatchesCompound)
            {
                return ServiceResult<ResidentRiskFlagResponse>.BadRequest("Property unit must belong to the selected compound.");
            }
        }

        var sourceValidation = await ValidateSourceEntityScopeAsync(
            request.CompoundId,
            request.ResidentProfileId,
            request.SourceEntityType,
            request.SourceEntityId,
            cancellationToken);
        if (sourceValidation is not null)
        {
            return ServiceResult<ResidentRiskFlagResponse>.BadRequest(sourceValidation);
        }

        return null;
    }

    private async Task<ResidentRiskFlag?> GetScopedFlagWithActionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return await dbContext.ResidentRiskFlags
            .ApplyCompoundAccess(scope, riskFlag => riskFlag.CompoundId)
            .Include(riskFlag => riskFlag.Actions.OrderByDescending(action => action.CreatedAtUtc))
            .SingleOrDefaultAsync(riskFlag => riskFlag.Id == id, cancellationToken);
    }

    private async Task<string?> ValidateSourceEntityScopeAsync(
        Guid compoundId,
        Guid residentProfileId,
        ResidentRiskFlagSourceEntityType sourceEntityType,
        Guid? sourceEntityId,
        CancellationToken cancellationToken)
    {
        if (!sourceEntityId.HasValue || sourceEntityType == ResidentRiskFlagSourceEntityType.None)
        {
            return null;
        }

        var id = sourceEntityId.Value;
        return sourceEntityType switch
        {
            ResidentRiskFlagSourceEntityType.ResidentProfile => await dbContext.ResidentProfiles
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId && entity.Id == residentProfileId, cancellationToken)
                    ? null
                    : "Source resident profile must match the selected resident and compound.",
            ResidentRiskFlagSourceEntityType.PropertyUnit => await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken)
                    ? null
                    : "Source property unit must belong to the selected compound.",
            ResidentRiskFlagSourceEntityType.UtilityBill => await dbContext.UtilityBills
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id
                    && entity.CompoundId == compoundId
                    && (!entity.ResidentProfileId.HasValue || entity.ResidentProfileId == residentProfileId),
                    cancellationToken)
                    ? null
                    : "Source utility bill must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.Payment => await dbContext.Payments
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id
                    && entity.CompoundId == compoundId
                    && (!entity.ResidentProfileId.HasValue || entity.ResidentProfileId == residentProfileId),
                    cancellationToken)
                    ? null
                    : "Source payment must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.Complaint => await dbContext.Complaints
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId && entity.ResidentProfileId == residentProfileId, cancellationToken)
                    ? null
                    : "Source complaint must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.Violation => await dbContext.Violations
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id
                    && entity.CompoundId == compoundId
                    && (!entity.ResidentProfileId.HasValue || entity.ResidentProfileId == residentProfileId),
                    cancellationToken)
                    ? null
                    : "Source violation must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.ViolationFine => await dbContext.ViolationFines
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken)
                    ? null
                    : "Source violation fine must belong to the selected compound.",
            ResidentRiskFlagSourceEntityType.MaintenanceRequest => await dbContext.MaintenanceRequests
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId && entity.ResidentProfileId == residentProfileId, cancellationToken)
                    ? null
                    : "Source maintenance request must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.Conversation => await dbContext.Conversations
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken)
                    ? null
                    : "Source conversation must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.Document => await dbContext.DocumentFiles
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken)
                    ? null
                    : "Source document must belong to the selected compound.",
            ResidentRiskFlagSourceEntityType.RentContract => await dbContext.RentContracts
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId && entity.ResidentProfileId == residentProfileId, cancellationToken)
                    ? null
                    : "Source rent contract must belong to the selected compound and resident.",
            ResidentRiskFlagSourceEntityType.ApprovalRequest => await dbContext.ApprovalRequests
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken)
                    ? null
                    : "Source approval request must belong to the selected compound.",
            ResidentRiskFlagSourceEntityType.WorkOrder => await dbContext.WorkOrders
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken)
                    ? null
                    : "Source work order must belong to the selected compound.",
            _ => "Unsupported source entity type."
        };
    }

    private async Task<string?> ValidateAssignableUserAsync(
        Guid userId,
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return "Assigned user id is invalid.";
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return "Assigned user was not found.";
        }

        if (await userManager.IsInRoleAsync(user, UserRole.SuperAdmin.ToString()))
        {
            return null;
        }

        if (await userManager.IsInRoleAsync(user, UserRole.CompoundAdmin.ToString())
            && await compoundAccessService.CanUserAccessCompoundAsync(userId, compoundId, UserRole.CompoundAdmin, cancellationToken))
        {
            return null;
        }

        if (await userManager.IsInRoleAsync(user, UserRole.Accountant.ToString())
            && await compoundAccessService.CanUserAccessCompoundAsync(userId, compoundId, UserRole.Accountant, cancellationToken))
        {
            return null;
        }

        return "Assigned user must be a SuperAdmin or assigned admin/accountant for the selected compound.";
    }

    private static string? ValidateSearchQuery(ResidentRiskFlagSearchQuery query)
    {
        if (query.PageNumber <= 0)
        {
            return "Page number must be greater than zero.";
        }

        if (query.PageSize <= 0 || query.PageSize > 100)
        {
            return "Page size must be between 1 and 100.";
        }

        if (query.CompoundId == Guid.Empty)
        {
            return "Compound id is invalid.";
        }

        if (query.ResidentProfileId == Guid.Empty)
        {
            return "Resident profile id is invalid.";
        }

        if (query.PropertyUnitId == Guid.Empty)
        {
            return "Property unit id is invalid.";
        }

        if (query.AssignedToUserId == Guid.Empty)
        {
            return "Assigned user id is invalid.";
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm) && query.SearchTerm.Length > 200)
        {
            return "Search term cannot exceed 200 characters.";
        }

        return null;
    }

    private static IQueryable<ResidentRiskFlag> ApplySearchFilters(
        IQueryable<ResidentRiskFlag> riskFlags,
        ResidentRiskFlagSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.AssignedToUserId.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.AssignedToUserId == query.AssignedToUserId.Value);
        }

        if (query.FlagType.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.FlagType == query.FlagType.Value);
        }

        if (query.Severity.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.Severity == query.Severity.Value);
        }

        if (query.Status.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.Status == query.Status.Value);
        }

        if (query.Source.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.Source == query.Source.Value);
        }

        if (query.RequiresSupervisorReview.HasValue)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.RequiresSupervisorReview == query.RequiresSupervisorReview.Value);
        }

        if (query.ActiveOnly == true)
        {
            riskFlags = riskFlags.Where(riskFlag => riskFlag.Status == ResidentRiskFlagStatus.Active
                || riskFlag.Status == ResidentRiskFlagStatus.Monitoring);
        }

        if (query.OverdueReviewOnly == true)
        {
            var now = DateTime.UtcNow;
            riskFlags = riskFlags.Where(riskFlag =>
                (riskFlag.Status == ResidentRiskFlagStatus.Active || riskFlag.Status == ResidentRiskFlagStatus.Monitoring)
                && riskFlag.NextReviewAtUtc.HasValue
                && riskFlag.NextReviewAtUtc < now);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            riskFlags = riskFlags.Where(riskFlag =>
                riskFlag.Title.Contains(term)
                || riskFlag.Description.Contains(term)
                || (riskFlag.RecommendedAction != null && riskFlag.RecommendedAction.Contains(term)));
        }

        return riskFlags;
    }

    private async Task ExpireIfNeededAsync(
        ResidentRiskFlag riskFlag,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!IsOpen(riskFlag.Status)
            || !riskFlag.ExpiresAtUtc.HasValue
            || riskFlag.ExpiresAtUtc.Value >= DateTime.UtcNow)
        {
            return;
        }

        var previousStatus = riskFlag.Status;
        riskFlag.Status = ResidentRiskFlagStatus.Expired;
        riskFlag.UpdatedAtUtc = DateTime.UtcNow;

        AddAction(
            riskFlag,
            actorUserId,
            ResidentRiskFlagActionType.Expired,
            previousStatus,
            ResidentRiskFlagStatus.Expired,
            null,
            null,
            "Risk flag expired automatically.");
        AddActivityEvent(
            riskFlag,
            actorUserId,
            ActivityEventType.RiskFlagDismissed,
            "Resident risk flag expired",
            "Risk flag expired automatically.");

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ServiceResult<T>?> ValidateCurrentUserAsync<T>(
        Guid? currentUserId,
        IReadOnlyCollection<UserRole> allowedRoles)
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
        {
            return ServiceResult<T>.Forbidden("Authentication is required.");
        }

        var user = await userManager.FindByIdAsync(currentUserId.Value.ToString());
        if (user is null)
        {
            return ServiceResult<T>.Forbidden("Authenticated user was not found.");
        }

        foreach (var role in allowedRoles)
        {
            if (await userManager.IsInRoleAsync(user, role.ToString()))
            {
                return null;
            }
        }

        return ServiceResult<T>.Forbidden("Current user is not allowed to perform resident risk flag operations.");
    }

    private static string? ValidateMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        if (metadataJson.Length > 4000)
        {
            return "Metadata JSON cannot exceed 4000 characters.";
        }

        try
        {
            using var _ = JsonDocument.Parse(metadataJson);
            return null;
        }
        catch (JsonException)
        {
            return "Metadata must be valid JSON.";
        }
    }

    private static bool IsOpen(ResidentRiskFlagStatus status)
    {
        return status is ResidentRiskFlagStatus.Active or ResidentRiskFlagStatus.Monitoring;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddAction(
        ResidentRiskFlag riskFlag,
        Guid actorUserId,
        ResidentRiskFlagActionType actionType,
        ResidentRiskFlagStatus? previousStatus,
        ResidentRiskFlagStatus? newStatus,
        ResidentRiskFlagSeverity? previousSeverity,
        ResidentRiskFlagSeverity? newSeverity,
        string notes)
    {
        riskFlag.Actions.Add(new ResidentRiskFlagAction
        {
            ResidentRiskFlagId = riskFlag.Id,
            ActorUserId = actorUserId,
            ActionType = actionType,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            PreviousSeverity = previousSeverity,
            NewSeverity = newSeverity,
            Notes = string.IsNullOrWhiteSpace(notes) ? actionType.ToString() : notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private void AddActivityEvent(
        ResidentRiskFlag riskFlag,
        Guid actorUserId,
        ActivityEventType eventType,
        string title,
        string description)
    {
        dbContext.ActivityEvents.Add(new ActivityEvent
        {
            CompoundId = riskFlag.CompoundId,
            ResidentProfileId = riskFlag.ResidentProfileId,
            PropertyUnitId = riskFlag.PropertyUnitId,
            ActorUserId = actorUserId,
            EventType = eventType,
            Title = title,
            Description = description,
            EntityType = ActivityEntityType.ResidentRiskFlag,
            EntityId = riskFlag.Id,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new
            {
                residentRiskFlagId = riskFlag.Id,
                riskFlag.FlagType,
                riskFlag.Severity,
                riskFlag.Status,
                riskFlag.RequiresSupervisorReview
            })
        });
    }

    private void AddNotification(
        ResidentRiskFlag riskFlag,
        Guid actorUserId,
        Guid? recipientUserId,
        string recipientName,
        NotificationEventType eventType,
        string subject,
        string body)
    {
        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            CompoundId = riskFlag.CompoundId,
            ResidentProfileId = riskFlag.ResidentProfileId,
            RecipientUserId = recipientUserId,
            Channel = NotificationChannel.InApp,
            EventType = eventType,
            Priority = MapPriority(riskFlag.Severity),
            RecipientName = recipientName,
            Subject = subject,
            Body = body,
            RelatedEntityType = NotificationRelatedEntityType.ResidentRiskFlag,
            RelatedEntityId = riskFlag.Id,
            MetadataJson = JsonSerializer.Serialize(new
            {
                residentRiskFlagId = riskFlag.Id,
                residentProfileId = riskFlag.ResidentProfileId,
                riskFlag.FlagType,
                riskFlag.Severity,
                riskFlag.Status,
                actorUserId
            }),
            ScheduledAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        });
    }

    private static NotificationPriority MapPriority(ResidentRiskFlagSeverity severity)
    {
        return severity switch
        {
            ResidentRiskFlagSeverity.Low => NotificationPriority.Low,
            ResidentRiskFlagSeverity.High => NotificationPriority.High,
            ResidentRiskFlagSeverity.Critical => NotificationPriority.Urgent,
            _ => NotificationPriority.Normal
        };
    }

    private static async Task<PagedResult<ResidentRiskFlagResponse>> ToPagedResultAsync(
        IQueryable<ResidentRiskFlag> riskFlags,
        ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken)
    {
        var totalCount = await riskFlags.CountAsync(cancellationToken);
        var items = await riskFlags
            .OrderByDescending(riskFlag => riskFlag.Severity)
            .ThenByDescending(riskFlag => riskFlag.CreatedAtUtc)
            .ThenByDescending(riskFlag => riskFlag.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(riskFlag => ToResponse(riskFlag))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ResidentRiskFlagResponse>(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    private static ResidentRiskFlagResponse ToResponse(ResidentRiskFlag riskFlag)
    {
        return new ResidentRiskFlagResponse(
            riskFlag.Id,
            riskFlag.CompoundId,
            riskFlag.ResidentProfileId,
            riskFlag.PropertyUnitId,
            riskFlag.CreatedByUserId,
            riskFlag.AssignedToUserId,
            riskFlag.FlagType,
            riskFlag.Severity,
            riskFlag.Status,
            riskFlag.Source,
            riskFlag.SourceEntityType,
            riskFlag.SourceEntityId,
            riskFlag.Title,
            riskFlag.Description,
            riskFlag.RecommendedAction,
            !string.IsNullOrWhiteSpace(riskFlag.InternalNotes),
            riskFlag.RequiresSupervisorReview,
            riskFlag.CreatedAtUtc,
            riskFlag.UpdatedAtUtc,
            riskFlag.AssignedAtUtc,
            riskFlag.LastReviewedAtUtc,
            riskFlag.NextReviewAtUtc,
            riskFlag.ExpiresAtUtc,
            riskFlag.ResolvedAtUtc,
            riskFlag.DismissedAtUtc);
    }

    private static ResidentRiskFlagDetailsResponse ToDetailsResponse(ResidentRiskFlag riskFlag)
    {
        return new ResidentRiskFlagDetailsResponse(
            riskFlag.Id,
            riskFlag.CompoundId,
            riskFlag.ResidentProfileId,
            riskFlag.PropertyUnitId,
            riskFlag.CreatedByUserId,
            riskFlag.AssignedToUserId,
            riskFlag.LastReviewedByUserId,
            riskFlag.ClosedByUserId,
            riskFlag.FlagType,
            riskFlag.Severity,
            riskFlag.Status,
            riskFlag.Source,
            riskFlag.SourceEntityType,
            riskFlag.SourceEntityId,
            riskFlag.Title,
            riskFlag.Description,
            riskFlag.RecommendedAction,
            riskFlag.InternalNotes,
            riskFlag.ResolutionNotes,
            riskFlag.DismissalReason,
            riskFlag.MetadataJson,
            riskFlag.RequiresSupervisorReview,
            riskFlag.CreatedAtUtc,
            riskFlag.UpdatedAtUtc,
            riskFlag.AssignedAtUtc,
            riskFlag.LastReviewedAtUtc,
            riskFlag.NextReviewAtUtc,
            riskFlag.ExpiresAtUtc,
            riskFlag.ResolvedAtUtc,
            riskFlag.DismissedAtUtc,
            riskFlag.Actions
                .OrderByDescending(action => action.CreatedAtUtc)
                .Select(action => new ResidentRiskFlagActionResponse(
                    action.Id,
                    action.ActorUserId,
                    action.ActionType,
                    action.PreviousStatus,
                    action.NewStatus,
                    action.PreviousSeverity,
                    action.NewSeverity,
                    action.Notes,
                    action.CreatedAtUtc))
                .ToArray());
    }
}

