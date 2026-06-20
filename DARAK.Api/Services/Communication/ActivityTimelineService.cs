using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ActivityTimelineService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IActivityTimelineService
{
    public async Task<ServiceResult<ActivityEventResponse>> RecordAsync(
        RecordActivityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<ActivityEventResponse>.BadRequest("Compound id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ServiceResult<ActivityEventResponse>.BadRequest("Activity title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return ServiceResult<ActivityEventResponse>.BadRequest("Activity description is required.");
        }

        if (request.EntityType == ActivityEntityType.None && request.EntityId.HasValue)
        {
            return ServiceResult<ActivityEventResponse>.BadRequest("Entity id cannot be provided when entity type is None.");
        }

        if (request.EntityType != ActivityEntityType.None && !request.EntityId.HasValue)
        {
            return ServiceResult<ActivityEventResponse>.BadRequest("Entity id is required when entity type is provided.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == request.CompoundId, cancellationToken);

        if (!compoundExists)
        {
            return ServiceResult<ActivityEventResponse>.NotFound("Compound was not found.");
        }

        var scopeValidation = await ValidateActivityScopeAsync(request, cancellationToken);
        if (scopeValidation is not null)
        {
            return scopeValidation;
        }

        var activityEvent = new ActivityEvent
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            PropertyUnitId = request.PropertyUnitId,
            ActorUserId = request.ActorUserId,
            EventType = request.EventType,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ActivityEvents.Add(activityEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ActivityEventResponse>.Success(ToResponse(activityEvent));
    }

    public async Task<ServiceResult<IReadOnlyList<ActivityEventResponse>>> GetRecentForCompoundAsync(
        Guid compoundId,
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        if (compoundId == Guid.Empty)
        {
            return ServiceResult<IReadOnlyList<ActivityEventResponse>>.BadRequest("Compound id is required.");
        }

        if (count <= 0 || count > 100)
        {
            return ServiceResult<IReadOnlyList<ActivityEventResponse>>.BadRequest("Count must be between 1 and 100.");
        }

        if (compoundAccessService is not null
            && !await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken))
        {
            return ServiceResult<IReadOnlyList<ActivityEventResponse>>.Forbidden("Current user cannot access this compound.");
        }

        var events = await dbContext.ActivityEvents
            .AsNoTracking()
            .Where(activityEvent => activityEvent.CompoundId == compoundId)
            .OrderByDescending(activityEvent => activityEvent.CreatedAtUtc)
            .ThenByDescending(activityEvent => activityEvent.Id)
            .Take(count)
            .Select(activityEvent => ToResponse(activityEvent))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ActivityEventResponse>>.Success(events);
    }

    public async Task<ServiceResult<PagedResult<ActivityEventResponse>>> SearchRecentActivityAsync(
        ActivityTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateQuery(query);
        if (validation is not null)
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.BadRequest(validation);
        }

        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.Forbidden("Current user cannot access activity timeline.");
        }

        var activities = ApplyActivityFilters(
            dbContext.ActivityEvents
                .AsNoTracking()
                .ApplyCompoundAccess(scope, activityEvent => activityEvent.CompoundId),
            query);

        return ServiceResult<PagedResult<ActivityEventResponse>>.Success(
            await ToPagedResultAsync(activities, query, cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<ActivityEventResponse>>> GetResidentTimelineAsync(
        Guid residentProfileId,
        ActivityTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        if (residentProfileId == Guid.Empty)
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.BadRequest("Resident profile id is required.");
        }

        var validation = ValidateQuery(query);
        if (validation is not null)
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.BadRequest(validation);
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.Id == residentProfileId, cancellationToken);

        if (resident is null || !await CanAccessCompoundAsync(resident.CompoundId, cancellationToken))
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.NotFound("Resident timeline was not found.");
        }

        var activities = ApplyActivityFilters(
            dbContext.ActivityEvents
                .AsNoTracking()
                .Where(activityEvent => activityEvent.CompoundId == resident.CompoundId)
                .Where(activityEvent => activityEvent.ResidentProfileId == residentProfileId
                    || (activityEvent.EntityType == ActivityEntityType.ResidentProfile
                        && activityEvent.EntityId == residentProfileId)),
            query);

        return ServiceResult<PagedResult<ActivityEventResponse>>.Success(
            await ToPagedResultAsync(activities, query, cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<ActivityEventResponse>>> GetUnitTimelineAsync(
        Guid propertyUnitId,
        ActivityTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        if (propertyUnitId == Guid.Empty)
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.BadRequest("Property unit id is required.");
        }

        var validation = ValidateQuery(query);
        if (validation is not null)
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.BadRequest(validation);
        }

        var unit = await dbContext.PropertyUnits
            .AsNoTracking()
            .SingleOrDefaultAsync(propertyUnit => propertyUnit.Id == propertyUnitId, cancellationToken);

        if (unit is null || !await CanAccessCompoundAsync(unit.CompoundId, cancellationToken))
        {
            return ServiceResult<PagedResult<ActivityEventResponse>>.NotFound("Unit timeline was not found.");
        }

        var activities = ApplyActivityFilters(
            dbContext.ActivityEvents
                .AsNoTracking()
                .Where(activityEvent => activityEvent.CompoundId == unit.CompoundId)
                .Where(activityEvent => activityEvent.PropertyUnitId == propertyUnitId
                    || (activityEvent.EntityType == ActivityEntityType.PropertyUnit
                        && activityEvent.EntityId == propertyUnitId)),
            query);

        return ServiceResult<PagedResult<ActivityEventResponse>>.Success(
            await ToPagedResultAsync(activities, query, cancellationToken));
    }

    private async Task<ServiceResult<ActivityEventResponse>?> ValidateActivityScopeAsync(
        RecordActivityEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ResidentProfileId.HasValue)
        {
            var residentMatchesCompound = await dbContext.ResidentProfiles
                .AsNoTracking()
                .AnyAsync(resident =>
                    resident.Id == request.ResidentProfileId.Value
                    && resident.CompoundId == request.CompoundId,
                    cancellationToken);
            if (!residentMatchesCompound)
            {
                return ServiceResult<ActivityEventResponse>.BadRequest("Activity resident must belong to the selected compound.");
            }
        }

        if (request.PropertyUnitId.HasValue)
        {
            var unitMatchesCompound = await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(unit =>
                    unit.Id == request.PropertyUnitId.Value
                    && unit.CompoundId == request.CompoundId,
                    cancellationToken);
            if (!unitMatchesCompound)
            {
                return ServiceResult<ActivityEventResponse>.BadRequest("Activity property unit must belong to the selected compound.");
            }
        }

        if (!request.EntityId.HasValue)
        {
            return null;
        }

        var entityId = request.EntityId.Value;
        var entityMatchesCompound = request.EntityType switch
        {
            ActivityEntityType.None => true,
            ActivityEntityType.Conversation => await dbContext.Conversations
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.ConversationMessage => await dbContext.ConversationMessages
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && dbContext.Conversations.Any(conversation =>
                        conversation.Id == entity.ConversationId
                        && conversation.CompoundId == request.CompoundId),
                    cancellationToken),
            ActivityEntityType.UtilityBill => await dbContext.UtilityBills
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.Payment => await dbContext.Payments
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.MaintenanceRequest => await dbContext.MaintenanceRequests
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.VisitorPass => await dbContext.VisitorPasses
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.Violation => await dbContext.Violations
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.ViolationFine => await dbContext.ViolationFines
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.Document => await dbContext.DocumentFiles
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.RentContract => await dbContext.RentContracts
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.PropertyUnit => await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.ResidentProfile => await dbContext.ResidentProfiles
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.ApprovalRequest => await dbContext.ApprovalRequests
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.ResidentRiskFlag => await dbContext.ResidentRiskFlags
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            ActivityEntityType.FinancialAdjustment => await dbContext.FinancialAdjustments
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == entityId && entity.CompoundId == request.CompoundId, cancellationToken),
            _ => false
        };

        return entityMatchesCompound
            ? null
            : ServiceResult<ActivityEventResponse>.BadRequest("Activity entity must belong to the selected compound.");
    }

    private async Task<CompoundAccessScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            ? new CompoundAccessScope(true, true, [])
            : await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return true;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static string? ValidateQuery(ActivityTimelineQuery query)
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

        if (query.EntityId == Guid.Empty)
        {
            return "Entity id is invalid.";
        }

        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value > query.ToUtc.Value)
        {
            return "FromUtc must be before ToUtc.";
        }

        return null;
    }

    private static IQueryable<ActivityEvent> ApplyActivityFilters(
        IQueryable<ActivityEvent> activities,
        ActivityTimelineQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            activities = activities.Where(activityEvent => activityEvent.CompoundId == query.CompoundId.Value);
        }

        if (query.EventType.HasValue)
        {
            activities = activities.Where(activityEvent => activityEvent.EventType == query.EventType.Value);
        }

        if (query.EntityType.HasValue)
        {
            activities = activities.Where(activityEvent => activityEvent.EntityType == query.EntityType.Value);
        }

        if (query.EntityId.HasValue)
        {
            activities = activities.Where(activityEvent => activityEvent.EntityId == query.EntityId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            activities = activities.Where(activityEvent => activityEvent.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            activities = activities.Where(activityEvent => activityEvent.CreatedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            activities = activities.Where(activityEvent =>
                activityEvent.Title.Contains(term)
                || activityEvent.Description.Contains(term));
        }

        return activities;
    }

    private static async Task<PagedResult<ActivityEventResponse>> ToPagedResultAsync(
        IQueryable<ActivityEvent> activities,
        ActivityTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var totalCount = await activities.CountAsync(cancellationToken);
        var items = await activities
            .OrderByDescending(activityEvent => activityEvent.CreatedAtUtc)
            .ThenByDescending(activityEvent => activityEvent.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(activityEvent => ToResponse(activityEvent))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ActivityEventResponse>(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    private static ActivityEventResponse ToResponse(ActivityEvent activityEvent)
    {
        return new ActivityEventResponse(
            activityEvent.Id,
            activityEvent.CompoundId,
            activityEvent.ResidentProfileId,
            activityEvent.PropertyUnitId,
            activityEvent.ActorUserId,
            activityEvent.EventType,
            activityEvent.Title,
            activityEvent.Description,
            activityEvent.EntityType,
            activityEvent.EntityId,
            activityEvent.CreatedAtUtc,
            activityEvent.MetadataJson);
    }
}
