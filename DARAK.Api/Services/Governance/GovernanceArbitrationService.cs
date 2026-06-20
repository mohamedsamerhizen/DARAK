using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Governance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class GovernanceArbitrationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IGovernanceArbitrationService
{
    private const int MaxTitleLength = 150;
    private const int MaxTextLength = 4000;
    private const int MaxDecisionLength = 2000;

    public async Task<ServiceResult<ArbitrationCaseSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<ArbitrationCaseSummaryResponse>.Forbidden("Current user cannot access arbitration cases.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<ArbitrationCaseSummaryResponse>.NotFound("Compound was not found.");
        }

        var cases = dbContext.ArbitrationCases
            .AsNoTracking()
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        if (compoundId.HasValue)
        {
            cases = cases.Where(item => item.CompoundId == compoundId.Value);
        }

        var response = new ArbitrationCaseSummaryResponse(
            await cases.CountAsync(item => item.Status == ArbitrationCaseStatus.Open, cancellationToken),
            await cases.CountAsync(item => item.Status == ArbitrationCaseStatus.UnderReview, cancellationToken),
            await cases.CountAsync(item => item.Priority == ArbitrationCasePriority.Critical
                && item.Status != ArbitrationCaseStatus.FinalDecisionIssued
                && item.Status != ArbitrationCaseStatus.Cancelled, cancellationToken),
            await cases.CountAsync(item => item.Status == ArbitrationCaseStatus.FinalDecisionIssued, cancellationToken),
            await cases.CountAsync(item => item.Status == ArbitrationCaseStatus.Cancelled, cancellationToken));

        return ServiceResult<ArbitrationCaseSummaryResponse>.Success(response);
    }

    public async Task<PagedResult<ArbitrationCaseResponse>> SearchCasesAsync(
        ArbitrationCaseQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var cases = ApplyFilters(
            dbContext.ArbitrationCases
                .AsNoTracking()
                .Include(item => item.Compound)
                .Include(item => item.ResidentProfile)
                .Include(item => item.Events)
                .ApplyCompoundAccess(scope, item => item.CompoundId),
            query);

        return await ToPagedAsync(
            cases.OrderByDescending(item => item.CreatedAtUtc),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<ArbitrationCaseResponse>> GetCaseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var arbitrationCase = await GetDetailsQuery(asNoTracking: true)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return arbitrationCase is null
            ? ServiceResult<ArbitrationCaseResponse>.NotFound("Arbitration case was not found.")
            : ServiceResult<ArbitrationCaseResponse>.Success(ToResponse(arbitrationCase));
    }

    public async Task<ServiceResult<ArbitrationCaseResponse>> CreateCaseAsync(
        Guid? currentUserId,
        CreateArbitrationCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = TrimOrNull(request.Title);
        var reason = TrimOrNull(request.Reason);

        if (title is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Arbitration case title is required.");
        }

        if (reason is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Arbitration case reason is required.");
        }

        if (title.Length > MaxTitleLength || reason.Length > MaxTextLength)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Arbitration case title or reason is too long.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ArbitrationCaseResponse>.NotFound("Compound was not found.");
        }

        var source = await ResolveSourceAsync(request.SourceType, request.SourceId, cancellationToken);
        if (source is null || source.CompoundId != request.CompoundId)
        {
            return ServiceResult<ArbitrationCaseResponse>.NotFound("Arbitration source was not found.");
        }

        var residentProfileId = request.ResidentProfileId ?? source.ResidentProfileId;
        if (request.ResidentProfileId.HasValue
            && source.ResidentProfileId.HasValue
            && request.ResidentProfileId.Value != source.ResidentProfileId.Value)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Resident profile does not match arbitration source.");
        }

        if (residentProfileId.HasValue)
        {
            var residentExists = await dbContext.ResidentProfiles.AnyAsync(profile =>
                profile.Id == residentProfileId.Value
                && profile.CompoundId == request.CompoundId
                && profile.IsActive,
                cancellationToken);
            if (!residentExists)
            {
                return ServiceResult<ArbitrationCaseResponse>.NotFound("Resident profile was not found.");
            }
        }

        var duplicate = await dbContext.ArbitrationCases.AnyAsync(item =>
            item.CompoundId == request.CompoundId
            && item.SourceType == request.SourceType
            && item.SourceId == request.SourceId
            && item.Status != ArbitrationCaseStatus.FinalDecisionIssued
            && item.Status != ArbitrationCaseStatus.Cancelled,
            cancellationToken);
        if (duplicate)
        {
            return ServiceResult<ArbitrationCaseResponse>.Conflict("An active arbitration case already exists for this source.");
        }

        var arbitrationCase = new ArbitrationCase
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = residentProfileId,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            Priority = request.Priority,
            Title = title,
            Reason = reason,
            CreatedByUserId = currentUserId,
            Status = ArbitrationCaseStatus.Open
        };

        arbitrationCase.Events.Add(new ArbitrationCaseEvent
        {
            EventType = ArbitrationCaseEventType.Created,
            Message = $"Arbitration case opened for {request.SourceType}.",
            CreatedByUserId = currentUserId
        });

        dbContext.ArbitrationCases.Add(arbitrationCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetCaseAsync(arbitrationCase.Id, cancellationToken);
    }

    public async Task<ServiceResult<ArbitrationCaseResponse>> AddEventAsync(
        Guid id,
        Guid? currentUserId,
        AddArbitrationCaseEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = TrimOrNull(request.Message);
        if (message is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Arbitration event message is required.");
        }

        if (message.Length > MaxTextLength)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Arbitration event message is too long.");
        }

        var arbitrationCase = await GetEditableCaseAsync(id, cancellationToken);
        if (arbitrationCase is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.NotFound("Arbitration case was not found.");
        }

        if (arbitrationCase.Status is ArbitrationCaseStatus.FinalDecisionIssued or ArbitrationCaseStatus.Cancelled)
        {
            return ServiceResult<ArbitrationCaseResponse>.Conflict("Closed arbitration cases cannot receive new events.");
        }

        arbitrationCase.Status = ArbitrationCaseStatus.UnderReview;
        arbitrationCase.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.ArbitrationCaseEvents.Add(new ArbitrationCaseEvent
        {
            ArbitrationCaseId = arbitrationCase.Id,
            EventType = request.EventType,
            Message = message,
            CreatedByUserId = currentUserId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetCaseAsync(arbitrationCase.Id, cancellationToken);
    }

    public async Task<ServiceResult<ArbitrationCaseResponse>> IssueFinalDecisionAsync(
        Guid id,
        Guid? currentUserId,
        IssueArbitrationFinalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var decision = TrimOrNull(request.Decision);
        if (decision is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Final decision is required.");
        }

        if (decision.Length > MaxDecisionLength || (request.DecisionSummary is not null && request.DecisionSummary.Length > MaxTextLength))
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Final decision is too long.");
        }

        var arbitrationCase = await GetEditableCaseAsync(id, cancellationToken);
        if (arbitrationCase is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.NotFound("Arbitration case was not found.");
        }

        if (arbitrationCase.Status is ArbitrationCaseStatus.FinalDecisionIssued or ArbitrationCaseStatus.Cancelled)
        {
            return ServiceResult<ArbitrationCaseResponse>.Conflict("Arbitration case already has a terminal status.");
        }

        arbitrationCase.Status = ArbitrationCaseStatus.FinalDecisionIssued;
        arbitrationCase.FinalDecision = decision;
        arbitrationCase.FinalDecisionSummary = TrimOrNull(request.DecisionSummary);
        arbitrationCase.DecidedByUserId = currentUserId;
        arbitrationCase.DecisionIssuedAtUtc = DateTime.UtcNow;
        arbitrationCase.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.ArbitrationCaseEvents.Add(new ArbitrationCaseEvent
        {
            ArbitrationCaseId = arbitrationCase.Id,
            EventType = ArbitrationCaseEventType.FinalDecisionIssued,
            Message = decision,
            CreatedByUserId = currentUserId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetCaseAsync(arbitrationCase.Id, cancellationToken);
    }

    public async Task<ServiceResult<ArbitrationCaseResponse>> CancelCaseAsync(
        Guid id,
        Guid? currentUserId,
        CancelArbitrationCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.BadRequest("Cancellation reason is required.");
        }

        var arbitrationCase = await GetEditableCaseAsync(id, cancellationToken);
        if (arbitrationCase is null)
        {
            return ServiceResult<ArbitrationCaseResponse>.NotFound("Arbitration case was not found.");
        }

        if (arbitrationCase.Status == ArbitrationCaseStatus.FinalDecisionIssued)
        {
            return ServiceResult<ArbitrationCaseResponse>.Conflict("Finalized arbitration cases cannot be cancelled.");
        }

        if (arbitrationCase.Status == ArbitrationCaseStatus.Cancelled)
        {
            return ServiceResult<ArbitrationCaseResponse>.Conflict("Arbitration case is already cancelled.");
        }

        arbitrationCase.Status = ArbitrationCaseStatus.Cancelled;
        arbitrationCase.CancelledByUserId = currentUserId;
        arbitrationCase.CancelledAtUtc = DateTime.UtcNow;
        arbitrationCase.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.ArbitrationCaseEvents.Add(new ArbitrationCaseEvent
        {
            ArbitrationCaseId = arbitrationCase.Id,
            EventType = ArbitrationCaseEventType.Cancelled,
            Message = reason,
            CreatedByUserId = currentUserId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetCaseAsync(arbitrationCase.Id, cancellationToken);
    }

    private IQueryable<ArbitrationCase> GetDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.ArbitrationCases
            .Include(item => item.Compound)
            .Include(item => item.ResidentProfile)
            .Include(item => item.Events);

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<ArbitrationCase?> GetEditableCaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(cancellationToken);
        return await dbContext.ArbitrationCases
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    private static IQueryable<ArbitrationCase> ApplyFilters(
        IQueryable<ArbitrationCase> query,
        ArbitrationCaseQueryRequest filter)
    {
        if (filter.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == filter.CompoundId.Value);
        }

        if (filter.ResidentProfileId.HasValue)
        {
            query = query.Where(item => item.ResidentProfileId == filter.ResidentProfileId.Value);
        }

        if (filter.SourceType.HasValue)
        {
            query = query.Where(item => item.SourceType == filter.SourceType.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(item => item.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(item => item.Priority == filter.Priority.Value);
        }

        return query;
    }

    private async Task<ArbitrationSource?> ResolveSourceAsync(
        ArbitrationCaseSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        return sourceType switch
        {
            ArbitrationCaseSourceType.Complaint => await dbContext.Complaints
                .AsNoTracking()
                .Where(item => item.Id == sourceId)
                .Select(item => new ArbitrationSource(item.CompoundId, item.ResidentProfileId))
                .FirstOrDefaultAsync(cancellationToken),

            ArbitrationCaseSourceType.FinancialDispute => await dbContext.FinancialDisputes
                .AsNoTracking()
                .Where(item => item.Id == sourceId)
                .Select(item => new ArbitrationSource(item.CompoundId, item.ResidentProfileId))
                .FirstOrDefaultAsync(cancellationToken),

            ArbitrationCaseSourceType.ViolationAppeal => await dbContext.ViolationAppeals
                .AsNoTracking()
                .Where(item => item.Id == sourceId)
                .Select(item => new ArbitrationSource(item.CompoundId, item.ResidentProfileId))
                .FirstOrDefaultAsync(cancellationToken),

            ArbitrationCaseSourceType.CollectionCase => await dbContext.CollectionCases
                .AsNoTracking()
                .Where(item => item.Id == sourceId)
                .Select(item => new ArbitrationSource(item.CompoundId, item.ResidentProfileId))
                .FirstOrDefaultAsync(cancellationToken),

            ArbitrationCaseSourceType.LegalNotice => await dbContext.LegalNotices
                .AsNoTracking()
                .Where(item => item.Id == sourceId)
                .Select(item => new ArbitrationSource(item.CompoundId, item.ResidentProfileId))
                .FirstOrDefaultAsync(cancellationToken),

            _ => null
        };
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        if (compoundId == Guid.Empty)
        {
            return false;
        }

        if (compoundAccessService is null)
        {
            return true;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<CompoundAccessScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return new CompoundAccessScope(true, true, []);
        }

        return await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private static async Task<PagedResult<ArbitrationCaseResponse>> ToPagedAsync(
        IQueryable<ArbitrationCase> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, pagination.PageNumber);
        var pageSize = Math.Clamp(pagination.PageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ArbitrationCaseResponse>(
            items.Select(ToResponse).ToArray(),
            pageNumber,
            pageSize,
            totalCount);
    }

    private static ArbitrationCaseResponse ToResponse(ArbitrationCase arbitrationCase)
    {
        return new ArbitrationCaseResponse(
            arbitrationCase.Id,
            arbitrationCase.CompoundId,
            arbitrationCase.Compound.Name,
            arbitrationCase.ResidentProfileId,
            arbitrationCase.ResidentProfile?.FullName,
            arbitrationCase.SourceType,
            arbitrationCase.SourceId,
            arbitrationCase.Status,
            arbitrationCase.Priority,
            arbitrationCase.Title,
            arbitrationCase.Reason,
            arbitrationCase.FinalDecision,
            arbitrationCase.FinalDecisionSummary,
            arbitrationCase.CreatedByUserId,
            arbitrationCase.DecidedByUserId,
            arbitrationCase.CancelledByUserId,
            arbitrationCase.CreatedAtUtc,
            arbitrationCase.UpdatedAtUtc,
            arbitrationCase.DecisionIssuedAtUtc,
            arbitrationCase.CancelledAtUtc,
            arbitrationCase.Events
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new ArbitrationCaseEventResponse(
                    item.Id,
                    item.EventType,
                    item.Message,
                    item.CreatedByUserId,
                    item.CreatedAtUtc))
                .ToArray());
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ArbitrationSource(Guid CompoundId, Guid? ResidentProfileId);
}
