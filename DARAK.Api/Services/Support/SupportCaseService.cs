using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Support;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class SupportCaseService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : ISupportCaseService
{
    private static readonly SupportCaseStatus[] OpenStatuses =
    [
        SupportCaseStatus.Open,
        SupportCaseStatus.Assigned,
        SupportCaseStatus.InProgress,
        SupportCaseStatus.Escalated
    ];

    public async Task<ServiceResult<SupportCaseResponse>> CreateCaseAsync(
        Guid? currentUserId,
        CreateSupportCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Compound id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Title and description are required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!CanAccess(scope, request.CompoundId))
        {
            return ServiceResult<SupportCaseResponse>.NotFound("Support case target was not found.");
        }

        if (!await ValidateOptionalResidentAsync(request.CompoundId, request.ResidentProfileId, cancellationToken)
            || !await ValidateOptionalUnitAsync(request.CompoundId, request.PropertyUnitId, cancellationToken))
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Resident or unit does not belong to the selected compound.");
        }

        var now = DateTime.UtcNow;
        var supportCase = new SupportCase
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            PropertyUnitId = request.PropertyUnitId,
            CreatedByUserId = currentUserId,
            SourceType = request.SourceType,
            SourceEntityId = request.SourceEntityId,
            Category = request.Category,
            Priority = request.Priority,
            Status = SupportCaseStatus.Open,
            Title = Truncate(request.Title.Trim(), 200)!,
            Description = Truncate(request.Description.Trim(), 4000)!,
            DueAtUtc = request.DueAtUtc ?? CalculateDefaultDueAt(now, request.Priority),
            CreatedAtUtc = now
        };

        dbContext.SupportCases.Add(supportCase);
        dbContext.SupportCaseEvents.Add(CreateEvent(
            supportCase.Id,
            currentUserId,
            SupportCaseEventType.Created,
            null,
            SupportCaseStatus.Open,
            "Support case created.",
            null,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            supportCase.CompoundId,
            supportCase.ResidentProfileId,
            currentUserId,
            null,
            AuditActionType.SupportCaseCreated,
            AuditEntityType.SupportCase,
            supportCase.Id,
            supportCase.Priority >= SupportCasePriority.Urgent ? AuditSeverity.High : AuditSeverity.Medium,
            "Support",
            $"Support case '{supportCase.Title}' was created.",
            MetadataJson: $"{{\"category\":\"{supportCase.Category}\",\"priority\":\"{supportCase.Priority}\"}}"),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SupportCaseResponse>.Success(ToResponse(supportCase));
    }

    public async Task<ServiceResult<PagedResult<SupportCaseResponse>>> SearchCasesAsync(
        SupportCaseSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<SupportCaseResponse>>.Forbidden("Current user cannot access support cases.");
        }

        if (query.CompoundId.HasValue && !CanAccess(scope, query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<SupportCaseResponse>>.NotFound("Support cases were not found.");
        }

        var cases = dbContext.SupportCases.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        if (query.CompoundId.HasValue)
        {
            cases = cases.Where(item => item.CompoundId == query.CompoundId.Value);
        }
        if (query.ResidentProfileId.HasValue)
        {
            cases = cases.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }
        if (query.AssignedToUserId.HasValue)
        {
            cases = cases.Where(item => item.AssignedToUserId == query.AssignedToUserId.Value);
        }
        if (query.Status.HasValue)
        {
            cases = cases.Where(item => item.Status == query.Status.Value);
        }
        if (query.Priority.HasValue)
        {
            cases = cases.Where(item => item.Priority == query.Priority.Value);
        }
        if (query.Category.HasValue)
        {
            cases = cases.Where(item => item.Category == query.Category.Value);
        }
        if (query.OverdueOnly == true)
        {
            var now = DateTime.UtcNow;
            cases = cases.Where(item => OpenStatuses.Contains(item.Status) && item.DueAtUtc < now);
        }
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            cases = cases.Where(item => item.Title.Contains(term) || item.Description.Contains(term));
        }

        var total = await cases.CountAsync(cancellationToken);
        var rows = await cases
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToResponse).ToArray();

        return ServiceResult<PagedResult<SupportCaseResponse>>.Success(
            new PagedResult<SupportCaseResponse>(items, query.PageNumber, query.PageSize, total));
    }

    public async Task<ServiceResult<SupportCaseDetailsResponse>> GetCaseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<SupportCaseDetailsResponse>.BadRequest("Support case id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var supportCase = await dbContext.SupportCases
            .AsNoTracking()
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .Include(item => item.Events.OrderBy(e => e.CreatedAtUtc))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supportCase is null)
        {
            return ServiceResult<SupportCaseDetailsResponse>.NotFound("Support case was not found.");
        }

        return ServiceResult<SupportCaseDetailsResponse>.Success(ToDetailsResponse(supportCase));
    }

    public async Task<ServiceResult<SupportCaseResponse>> AssignCaseAsync(
        Guid? currentUserId,
        Guid id,
        AssignSupportCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AssignedToUserId == Guid.Empty)
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Assigned user id is required.");
        }

        var result = await LoadCaseForUpdateAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ServiceResult<SupportCaseResponse>.NotFound("Support case was not found.");
        }

        var supportCase = result.Value!;
        if (supportCase.Status is SupportCaseStatus.Resolved or SupportCaseStatus.Closed or SupportCaseStatus.Cancelled)
        {
            return ServiceResult<SupportCaseResponse>.Conflict("Closed support cases cannot be assigned.");
        }

        var assignmentValidation = await ValidateAssignmentTargetAsync(
            currentUserId,
            supportCase,
            request.AssignedToUserId,
            cancellationToken);
        if (assignmentValidation is not null)
        {
            return assignmentValidation;
        }

        var now = DateTime.UtcNow;
        var previous = supportCase.Status;
        supportCase.AssignedToUserId = request.AssignedToUserId;
        supportCase.AssignmentNote = Truncate(request.Note, 1000);
        supportCase.AssignedAtUtc = now;
        supportCase.UpdatedAtUtc = now;
        supportCase.Status = SupportCaseStatus.Assigned;
        dbContext.SupportCaseEvents.Add(CreateEvent(supportCase.Id, currentUserId, SupportCaseEventType.Assigned, previous, supportCase.Status, "Support case assigned.", request.Note, now));
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<SupportCaseResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        await AuditAsync(supportCase, currentUserId, AuditActionType.SupportCaseAssigned, "Support case assigned.", AuditSeverity.Medium, cancellationToken);
        return ServiceResult<SupportCaseResponse>.Success(ToResponse(supportCase));
    }

    public async Task<ServiceResult<SupportCaseResponse>> EscalateCaseAsync(
        Guid? currentUserId,
        Guid id,
        EscalateSupportCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Escalation reason is required.");
        }

        var result = await LoadCaseForUpdateAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ServiceResult<SupportCaseResponse>.NotFound("Support case was not found.");
        }

        var supportCase = result.Value!;
        if (supportCase.Status is SupportCaseStatus.Resolved or SupportCaseStatus.Closed or SupportCaseStatus.Cancelled)
        {
            return ServiceResult<SupportCaseResponse>.Conflict("Closed support cases cannot be escalated.");
        }

        var now = DateTime.UtcNow;
        var previous = supportCase.Status;
        supportCase.Priority = request.Priority < SupportCasePriority.High ? SupportCasePriority.High : request.Priority;
        supportCase.Status = SupportCaseStatus.Escalated;
        supportCase.EscalationReason = Truncate(request.Reason.Trim(), 1000);
        supportCase.EscalatedAtUtc = now;
        supportCase.UpdatedAtUtc = now;
        dbContext.SupportCaseEvents.Add(CreateEvent(supportCase.Id, currentUserId, SupportCaseEventType.Escalated, previous, supportCase.Status, "Support case escalated.", request.Reason, now));
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<SupportCaseResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        await AuditAsync(supportCase, currentUserId, AuditActionType.SupportCaseEscalated, "Support case escalated.", AuditSeverity.High, cancellationToken);
        return ServiceResult<SupportCaseResponse>.Success(ToResponse(supportCase));
    }

    public async Task<ServiceResult<SupportCaseResponse>> ResolveCaseAsync(
        Guid? currentUserId,
        Guid id,
        ResolveSupportCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionSummary))
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Resolution summary is required.");
        }

        var result = await LoadCaseForUpdateAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ServiceResult<SupportCaseResponse>.NotFound("Support case was not found.");
        }

        var supportCase = result.Value!;
        if (supportCase.Status is SupportCaseStatus.Closed or SupportCaseStatus.Cancelled)
        {
            return ServiceResult<SupportCaseResponse>.Conflict("Closed support cases cannot be resolved again.");
        }

        var now = DateTime.UtcNow;
        var previous = supportCase.Status;
        supportCase.Status = request.CloseImmediately ? SupportCaseStatus.Closed : SupportCaseStatus.Resolved;
        supportCase.ResolutionSummary = Truncate(request.ResolutionSummary.Trim(), 2000);
        supportCase.ResolvedAtUtc = now;
        supportCase.ClosedAtUtc = request.CloseImmediately ? now : supportCase.ClosedAtUtc;
        supportCase.UpdatedAtUtc = now;
        dbContext.SupportCaseEvents.Add(CreateEvent(supportCase.Id, currentUserId, SupportCaseEventType.Resolved, previous, supportCase.Status, "Support case resolved.", request.ResolutionSummary, now));
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<SupportCaseResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        await AuditAsync(supportCase, currentUserId, AuditActionType.SupportCaseResolved, "Support case resolved.", AuditSeverity.Medium, cancellationToken);
        return ServiceResult<SupportCaseResponse>.Success(ToResponse(supportCase));
    }

    public async Task<ServiceResult<SupportCaseResponse>> ReopenCaseAsync(
        Guid? currentUserId,
        Guid id,
        ReopenSupportCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Reopen reason is required.");
        }

        var result = await LoadCaseForUpdateAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ServiceResult<SupportCaseResponse>.NotFound("Support case was not found.");
        }

        var supportCase = result.Value!;
        if (supportCase.Status is not (SupportCaseStatus.Resolved or SupportCaseStatus.Closed))
        {
            return ServiceResult<SupportCaseResponse>.Conflict("Only resolved or closed support cases can be reopened.");
        }

        var now = DateTime.UtcNow;
        var previous = supportCase.Status;
        supportCase.Status = SupportCaseStatus.Open;
        supportCase.ReopenCount++;
        supportCase.ClosedAtUtc = null;
        supportCase.ResolvedAtUtc = null;
        supportCase.UpdatedAtUtc = now;
        supportCase.DueAtUtc = CalculateDefaultDueAt(now, supportCase.Priority);
        dbContext.SupportCaseEvents.Add(CreateEvent(supportCase.Id, currentUserId, SupportCaseEventType.Reopened, previous, supportCase.Status, "Support case reopened.", request.Reason, now));
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<SupportCaseResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        await AuditAsync(supportCase, currentUserId, AuditActionType.SupportCaseReopened, "Support case reopened.", AuditSeverity.Medium, cancellationToken);
        return ServiceResult<SupportCaseResponse>.Success(ToResponse(supportCase));
    }

    public async Task<ServiceResult<SupportCaseDetailsResponse>> AddNoteAsync(
        Guid? currentUserId,
        Guid id,
        AddSupportCaseNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return ServiceResult<SupportCaseDetailsResponse>.BadRequest("Note is required.");
        }

        var result = await LoadCaseForUpdateAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ServiceResult<SupportCaseDetailsResponse>.NotFound("Support case was not found.");
        }

        var supportCase = result.Value!;
        dbContext.SupportCaseEvents.Add(CreateEvent(supportCase.Id, currentUserId, SupportCaseEventType.NoteAdded, supportCase.Status, supportCase.Status, "Internal note added.", request.Note, DateTime.UtcNow));
        supportCase.UpdatedAtUtc = DateTime.UtcNow;
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<SupportCaseDetailsResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        await AuditAsync(supportCase, currentUserId, AuditActionType.SupportCaseNoteAdded, "Support case note added.", AuditSeverity.Low, cancellationToken);
        return ServiceResult<SupportCaseDetailsResponse>.Success(ToDetailsResponse(supportCase));
    }

    public async Task<ServiceResult<SupportDashboardResponse>> GetDashboardAsync(
        SupportDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<SupportDashboardResponse>.Forbidden("Current user cannot access support dashboard.");
        }
        if (query.CompoundId.HasValue && !CanAccess(scope, query.CompoundId.Value))
        {
            return ServiceResult<SupportDashboardResponse>.NotFound("Support dashboard was not found.");
        }

        var now = DateTime.UtcNow;
        var cases = dbContext.SupportCases.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        if (query.CompoundId.HasValue)
        {
            cases = cases.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        var openCount = await cases.CountAsync(item => item.Status == SupportCaseStatus.Open, cancellationToken);
        var assignedCount = await cases.CountAsync(item => item.Status == SupportCaseStatus.Assigned, cancellationToken);
        var inProgressCount = await cases.CountAsync(item => item.Status == SupportCaseStatus.InProgress, cancellationToken);
        var escalatedCount = await cases.CountAsync(item => item.Status == SupportCaseStatus.Escalated, cancellationToken);
        var overdueCount = await cases.CountAsync(item => OpenStatuses.Contains(item.Status) && item.DueAtUtc < now, cancellationToken);
        var criticalCount = await cases.CountAsync(item => OpenStatuses.Contains(item.Status) && item.Priority == SupportCasePriority.Critical, cancellationToken);
        var resolvedTodayCount = await cases.CountAsync(item => item.ResolvedAtUtc.HasValue && item.ResolvedAtUtc.Value.Date == now.Date, cancellationToken);
        var reopenedCount = await cases.CountAsync(item => item.ReopenCount > 0, cancellationToken);
        var resolvedOrClosed = await cases.CountAsync(item => item.Status == SupportCaseStatus.Resolved || item.Status == SupportCaseStatus.Closed, cancellationToken);
        var total = await cases.CountAsync(cancellationToken);
        var resolutionRate = total == 0 ? 0 : Math.Round(resolvedOrClosed * 100.0 / total, 2);

        var priorityRows = await cases
            .Where(item => OpenStatuses.Contains(item.Status))
            .GroupBy(item => item.Priority)
            .Select(group => new { Priority = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var categoryRows = await cases
            .Where(item => OpenStatuses.Contains(item.Status))
            .GroupBy(item => item.Category)
            .Select(group => new { Category = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return ServiceResult<SupportDashboardResponse>.Success(new SupportDashboardResponse(
            query.CompoundId,
            openCount,
            assignedCount,
            inProgressCount,
            escalatedCount,
            overdueCount,
            criticalCount,
            resolvedTodayCount,
            reopenedCount,
            resolutionRate,
            priorityRows.OrderByDescending(item => item.Priority).Select(item => new SupportCasePriorityCountResponse(item.Priority, item.Count)).ToArray(),
            categoryRows.OrderBy(item => item.Category).Select(item => new SupportCaseCategoryCountResponse(item.Category, item.Count)).ToArray(),
            now));
    }

    private async Task<ServiceResult<SupportCaseResponse>?> ValidateAssignmentTargetAsync(
        Guid? currentUserId,
        SupportCase supportCase,
        Guid assignedToUserId,
        CancellationToken cancellationToken)
    {
        var assignedUserExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == assignedToUserId, cancellationToken);
        if (!assignedUserExists)
        {
            return ServiceResult<SupportCaseResponse>.NotFound("Assigned user was not found.");
        }

        var assignedRoles = await GetUserRolesAsync(assignedToUserId, cancellationToken);
        if (IsResidentOnly(assignedRoles))
        {
            return ServiceResult<SupportCaseResponse>.BadRequest("Resident-only users cannot be assigned to support cases.");
        }

        if (await IsUserInRoleAsync(currentUserId, UserRole.SuperAdmin, cancellationToken))
        {
            return null;
        }

        if (!await AssignedUserCanAccessCompoundAsync(assignedToUserId, supportCase.CompoundId, assignedRoles, cancellationToken))
        {
            return ServiceResult<SupportCaseResponse>.Forbidden("Assigned user is outside the support case compound scope.");
        }

        return null;
    }

    private async Task<UserRole[]> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleNames = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Name!)
            .Where(roleName => roleName != null)
            .ToArrayAsync(cancellationToken);

        return roleNames
            .Select(roleName => Enum.TryParse<UserRole>(roleName, out var parsedRole) ? parsedRole : (UserRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .ToArray();
    }

    private async Task<bool> IsUserInRoleAsync(
        Guid? userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return false;
        }

        var roleName = role.ToString();
        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId.Value)
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                identityRole => identityRole.Id,
                (_, identityRole) => identityRole.Name)
            .AnyAsync(identityRoleName => identityRoleName == roleName, cancellationToken);
    }

    private async Task<bool> AssignedUserCanAccessCompoundAsync(
        Guid assignedToUserId,
        Guid compoundId,
        IReadOnlyCollection<UserRole> assignedRoles,
        CancellationToken cancellationToken)
    {
        if (assignedRoles.Contains(UserRole.SuperAdmin))
        {
            return true;
        }

        return await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .AnyAsync(assignment =>
                assignment.UserId == assignedToUserId
                && assignment.CompoundId == compoundId
                && assignment.IsActive
                && assignment.Role != UserRole.Resident
                && assignedRoles.Contains(assignment.Role),
                cancellationToken);
    }

    private static bool IsResidentOnly(IReadOnlyCollection<UserRole> roles)
    {
        return roles.Count == 1 && roles.Contains(UserRole.Resident);
    }

    private async Task<ServiceResult<SupportCase>> LoadCaseForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<SupportCase>.BadRequest("Support case id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var supportCase = await dbContext.SupportCases
            .Include(item => item.Events.OrderBy(e => e.CreatedAtUtc))
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return supportCase is null
            ? ServiceResult<SupportCase>.NotFound("Support case was not found.")
            : ServiceResult<SupportCase>.Success(supportCase);
    }

    private async Task<bool> ValidateOptionalResidentAsync(Guid compoundId, Guid? residentProfileId, CancellationToken cancellationToken)
    {
        return !residentProfileId.HasValue
            || await dbContext.ResidentProfiles.AsNoTracking().AnyAsync(item => item.Id == residentProfileId.Value && item.CompoundId == compoundId, cancellationToken);
    }

    private async Task<bool> ValidateOptionalUnitAsync(Guid compoundId, Guid? propertyUnitId, CancellationToken cancellationToken)
    {
        return !propertyUnitId.HasValue
            || await dbContext.PropertyUnits.AsNoTracking().AnyAsync(item => item.Id == propertyUnitId.Value && item.CompoundId == compoundId, cancellationToken);
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
            return ServiceResult<T>.Conflict("The support case was updated by another operation. Reload and try again.");
        }
    }

    private async Task AuditAsync(
        SupportCase supportCase,
        Guid? actorUserId,
        AuditActionType actionType,
        string description,
        AuditSeverity severity,
        CancellationToken cancellationToken)
    {
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            supportCase.CompoundId,
            supportCase.ResidentProfileId,
            actorUserId,
            null,
            actionType,
            AuditEntityType.SupportCase,
            supportCase.Id,
            severity,
            "Support",
            description),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SupportCaseEvent CreateEvent(
        Guid supportCaseId,
        Guid? actorUserId,
        SupportCaseEventType eventType,
        SupportCaseStatus? fromStatus,
        SupportCaseStatus? toStatus,
        string description,
        string? note,
        DateTime now)
    {
        return new SupportCaseEvent
        {
            SupportCaseId = supportCaseId,
            ActorUserId = actorUserId,
            EventType = eventType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Description = description,
            InternalNote = Truncate(note, 2000),
            CreatedAtUtc = now
        };
    }

    private static bool CanAccess(CompoundAccessScope scope, Guid compoundId)
    {
        return scope.IsAuthenticated && scope.CanAccess(compoundId);
    }

    private static DateTime CalculateDefaultDueAt(DateTime now, SupportCasePriority priority)
    {
        var hours = priority switch
        {
            SupportCasePriority.Critical => 4,
            SupportCasePriority.Urgent => 8,
            SupportCasePriority.High => 24,
            SupportCasePriority.Normal => 72,
            _ => 120
        };
        return now.AddHours(hours);
    }

    private static SupportCaseResponse ToResponse(SupportCase supportCase)
    {
        return new SupportCaseResponse(
            supportCase.Id,
            supportCase.CompoundId,
            supportCase.ResidentProfileId,
            supportCase.PropertyUnitId,
            supportCase.AssignedToUserId,
            supportCase.SourceType,
            supportCase.SourceEntityId,
            supportCase.Category,
            supportCase.Priority,
            supportCase.Status,
            supportCase.Title,
            supportCase.Description,
            supportCase.ResolutionSummary,
            supportCase.ReopenCount,
            supportCase.DueAtUtc,
            OpenStatuses.Contains(supportCase.Status) && supportCase.DueAtUtc < DateTime.UtcNow,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.AssignedAtUtc,
            supportCase.EscalatedAtUtc,
            supportCase.ResolvedAtUtc,
            supportCase.ClosedAtUtc);
    }

    private static SupportCaseDetailsResponse ToDetailsResponse(SupportCase supportCase)
    {
        return new SupportCaseDetailsResponse(
            supportCase.Id,
            supportCase.CompoundId,
            supportCase.ResidentProfileId,
            supportCase.PropertyUnitId,
            supportCase.AssignedToUserId,
            supportCase.CreatedByUserId,
            supportCase.SourceType,
            supportCase.SourceEntityId,
            supportCase.Category,
            supportCase.Priority,
            supportCase.Status,
            supportCase.Title,
            supportCase.Description,
            supportCase.AssignmentNote,
            supportCase.EscalationReason,
            supportCase.ResolutionSummary,
            supportCase.ReopenCount,
            supportCase.DueAtUtc,
            OpenStatuses.Contains(supportCase.Status) && supportCase.DueAtUtc < DateTime.UtcNow,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.Events.OrderBy(item => item.CreatedAtUtc).Select(ToEventResponse).ToArray());
    }

    private static SupportCaseEventResponse ToEventResponse(SupportCaseEvent item)
    {
        return new SupportCaseEventResponse(
            item.Id,
            item.ActorUserId,
            item.EventType,
            item.FromStatus,
            item.ToStatus,
            item.Description,
            item.InternalNote,
            item.CreatedAtUtc);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}


