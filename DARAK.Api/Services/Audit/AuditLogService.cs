using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class AuditLogService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IHttpContextAccessor httpContextAccessor)
    : IAuditLogService
{
    public Task<Guid> AppendEntryAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var now = DateTime.UtcNow;
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            CompoundId = record.CompoundId,
            ResidentProfileId = record.ResidentProfileId,
            ActorUserId = record.ActorUserId,
            ActorRole = Normalize(record.ActorRole, maxLength: 120),
            ActionType = record.ActionType,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Severity = record.Severity,
            SourceModule = Normalize(record.SourceModule, maxLength: 120, fallback: "System"),
            Description = Normalize(record.Description, maxLength: 1000, fallback: "Audit event recorded."),
            Reason = NormalizeOptional(record.Reason, maxLength: 1000),
            IpAddress = NormalizeOptional(GetIpAddress(), maxLength: 64),
            UserAgent = NormalizeOptional(GetUserAgent(), maxLength: 512),
            CorrelationId = NormalizeOptional(record.CorrelationId ?? GetCorrelationId(), maxLength: 120),
            BeforeValuesJson = NormalizeOptional(record.BeforeValuesJson, maxLength: 8000),
            AfterValuesJson = NormalizeOptional(record.AfterValuesJson, maxLength: 8000),
            MetadataJson = NormalizeOptional(record.MetadataJson, maxLength: 8000),
            IsSystemGenerated = record.IsSystemGenerated,
            CreatedAtUtc = now
        };

        foreach (var change in record.Changes ?? [])
        {
            if (string.IsNullOrWhiteSpace(change.PropertyName))
            {
                continue;
            }

            entry.Changes.Add(new AuditLogChange
            {
                Id = Guid.NewGuid(),
                PropertyName = Normalize(change.PropertyName, maxLength: 200),
                OldValue = NormalizeOptional(change.OldValue, maxLength: 2000),
                NewValue = NormalizeOptional(change.NewValue, maxLength: 2000),
                IsSensitive = change.IsSensitive,
                CreatedAtUtc = now
            });
        }

        dbContext.AuditLogEntries.Add(entry);
        return Task.FromResult(entry.Id);
    }

    public async Task<ServiceResult<PagedResult<AuditLogResponse>>> SearchAsync(
        AuditSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query.FromUtc, query.ToUtc);
        if (validation is not null)
        {
            return ServiceResult<PagedResult<AuditLogResponse>>.BadRequest(validation);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<AuditLogResponse>>.Forbidden("Current user cannot access audit logs.");
        }

        if (query.CompoundId.HasValue && !CanAccessNullableCompound(scope, query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<AuditLogResponse>>.Success(
                new PagedResult<AuditLogResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var logs = ApplySearchFilters(ApplyScope(dbContext.AuditLogEntries.AsNoTracking(), scope), query);
        var totalCount = await logs.CountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(log => log.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(log => ToResponse(log))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<AuditLogResponse>>.Success(
            new PagedResult<AuditLogResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<AuditLogDetailsResponse>> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<AuditLogDetailsResponse>.BadRequest("Audit log id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<AuditLogDetailsResponse>.Forbidden("Current user cannot access audit logs.");
        }

        var entry = await ApplyScope(dbContext.AuditLogEntries.AsNoTracking(), scope)
            .Include(log => log.Changes.OrderBy(change => change.CreatedAtUtc))
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
        if (entry is null)
        {
            return ServiceResult<AuditLogDetailsResponse>.NotFound("Audit log was not found.");
        }

        return ServiceResult<AuditLogDetailsResponse>.Success(
            ToDetailsResponse(entry, includeRawJson: scope.IsSuperAdmin));
    }

    public async Task<ServiceResult<PagedResult<AuditLogResponse>>> GetEntityTrailAsync(
        AuditEntityType entityType,
        Guid entityId,
        AuditEntityTrailQuery query,
        CancellationToken cancellationToken = default)
    {
        if (entityType == AuditEntityType.None || entityId == Guid.Empty)
        {
            return ServiceResult<PagedResult<AuditLogResponse>>.BadRequest("Entity type and id are required.");
        }

        return await SearchAsync(new AuditSearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            CompoundId = query.CompoundId,
            EntityType = entityType,
            EntityId = entityId,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc
        }, cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<AuditLogResponse>>> GetResidentTrailAsync(
        Guid residentProfileId,
        AuditEntityTrailQuery query,
        CancellationToken cancellationToken = default)
    {
        if (residentProfileId == Guid.Empty)
        {
            return ServiceResult<PagedResult<AuditLogResponse>>.BadRequest("Resident profile id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentProfileId, cancellationToken);
        if (resident is null || !CanAccessNullableCompound(scope, resident.CompoundId))
        {
            return ServiceResult<PagedResult<AuditLogResponse>>.NotFound("Resident audit trail was not found.");
        }

        return await SearchAsync(new AuditSearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            CompoundId = query.CompoundId ?? resident.CompoundId,
            ResidentProfileId = residentProfileId,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc
        }, cancellationToken);
    }

    public async Task<ServiceResult<AuditDashboardResponse>> GetDashboardAsync(
        AuditDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateRange = NormalizeDashboardDateRange(query.FromUtc, query.ToUtc);
        if (dateRange.Error is not null)
        {
            return ServiceResult<AuditDashboardResponse>.BadRequest(dateRange.Error);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<AuditDashboardResponse>.Forbidden("Current user cannot access audit dashboard.");
        }

        if (query.CompoundId.HasValue && !CanAccessNullableCompound(scope, query.CompoundId.Value))
        {
            return ServiceResult<AuditDashboardResponse>.NotFound("Audit dashboard was not found.");
        }

        var logs = ApplyScope(dbContext.AuditLogEntries.AsNoTracking(), scope)
            .Where(log => log.CreatedAtUtc >= dateRange.FromUtc && log.CreatedAtUtc < dateRange.ToExclusiveUtc);

        if (query.CompoundId.HasValue)
        {
            logs = logs.Where(log => log.CompoundId == query.CompoundId.Value);
        }

        var severityCounts = await logs
            .GroupBy(log => log.Severity)
            .Select(group => new
            {
                Severity = group.Key,
                Count = group.Count()
            })
            .ToArrayAsync(cancellationToken);

        var latestCriticalAtUtc = await logs
            .Where(log => log.Severity == AuditSeverity.Critical)
            .OrderByDescending(log => log.CreatedAtUtc)
            .Select(log => (DateTime?)log.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var actionCounts = await logs
            .GroupBy(log => log.ActionType)
            .Select(group => new
            {
                ActionType = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.ActionType)
            .ToArrayAsync(cancellationToken);

        var byAction = actionCounts
            .Select(item => new AuditCountByActionResponse(item.ActionType, item.Count))
            .ToArray();

        var entityCounts = await logs
            .GroupBy(log => log.EntityType)
            .Select(group => new
            {
                EntityType = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.EntityType)
            .ToArrayAsync(cancellationToken);

        var byEntity = entityCounts
            .Select(item => new AuditCountByEntityResponse(item.EntityType, item.Count))
            .ToArray();

        var sourceModuleCounts = await logs
            .GroupBy(log => log.SourceModule)
            .Select(group => new
            {
                SourceModule = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.SourceModule)
            .ToArrayAsync(cancellationToken);

        var bySourceModule = sourceModuleCounts
            .Select(item => new AuditCountBySourceModuleResponse(item.SourceModule, item.Count))
            .ToArray();

        var response = new AuditDashboardResponse(
            query.CompoundId,
            dateRange.FromUtc,
            dateRange.ToExclusiveUtc.AddTicks(-1),
            severityCounts.Sum(row => row.Count),
            severityCounts.FirstOrDefault(row => row.Severity == AuditSeverity.Critical)?.Count ?? 0,
            severityCounts.FirstOrDefault(row => row.Severity == AuditSeverity.High)?.Count ?? 0,
            severityCounts.FirstOrDefault(row => row.Severity == AuditSeverity.Medium)?.Count ?? 0,
            severityCounts.FirstOrDefault(row => row.Severity == AuditSeverity.Low)?.Count ?? 0,
            latestCriticalAtUtc,
            byAction,
            byEntity,
            bySourceModule);

        return ServiceResult<AuditDashboardResponse>.Success(response);
    }

    private IQueryable<AuditLogEntry> ApplyScope(IQueryable<AuditLogEntry> query, CompoundAccessScope scope)
    {
        if (!scope.IsAuthenticated)
        {
            return query.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(log => log.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(log.CompoundId.Value));
    }

    private static IQueryable<AuditLogEntry> ApplySearchFilters(
        IQueryable<AuditLogEntry> logs,
        AuditSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            logs = logs.Where(log => log.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            logs = logs.Where(log => log.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.ActorUserId.HasValue)
        {
            logs = logs.Where(log => log.ActorUserId == query.ActorUserId.Value);
        }

        if (query.ActionType.HasValue)
        {
            logs = logs.Where(log => log.ActionType == query.ActionType.Value);
        }

        if (query.EntityType.HasValue)
        {
            logs = logs.Where(log => log.EntityType == query.EntityType.Value);
        }

        if (query.EntityId.HasValue)
        {
            logs = logs.Where(log => log.EntityId == query.EntityId.Value);
        }

        if (query.Severity.HasValue)
        {
            logs = logs.Where(log => log.Severity == query.Severity.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceModule))
        {
            var sourceModule = query.SourceModule.Trim();
            logs = logs.Where(log => log.SourceModule == sourceModule);
        }

        if (query.FromUtc.HasValue)
        {
            logs = logs.Where(log => log.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            logs = logs.Where(log => log.CreatedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            logs = logs.Where(log => log.Description.Contains(term) || (log.Reason != null && log.Reason.Contains(term)));
        }

        return logs;
    }

    private bool CanAccessNullableCompound(CompoundAccessScope scope, Guid compoundId)
    {
        return scope.IsAuthenticated
            && (scope.IsSuperAdmin || scope.AllowedCompoundIds.Contains(compoundId));
    }

    private static AuditLogResponse ToResponse(AuditLogEntry entry)
    {
        return new AuditLogResponse(
            entry.Id,
            entry.CompoundId,
            entry.ResidentProfileId,
            entry.ActorUserId,
            entry.ActorRole,
            entry.ActionType,
            entry.EntityType,
            entry.EntityId,
            entry.Severity,
            entry.SourceModule,
            entry.Description,
            entry.Reason,
            entry.CorrelationId,
            entry.IsSystemGenerated,
            entry.CreatedAtUtc);
    }

    private static AuditLogDetailsResponse ToDetailsResponse(AuditLogEntry entry, bool includeRawJson)
    {
        return new AuditLogDetailsResponse(
            entry.Id,
            entry.CompoundId,
            entry.ResidentProfileId,
            entry.ActorUserId,
            entry.ActorRole,
            entry.ActionType,
            entry.EntityType,
            entry.EntityId,
            entry.Severity,
            entry.SourceModule,
            entry.Description,
            entry.Reason,
            entry.IpAddress,
            entry.UserAgent,
            entry.CorrelationId,
            RedactRawJson(entry.BeforeValuesJson, includeRawJson),
            RedactRawJson(entry.AfterValuesJson, includeRawJson),
            RedactRawJson(entry.MetadataJson, includeRawJson),
            entry.IsSystemGenerated,
            entry.CreatedAtUtc,
            entry.Changes
                .OrderBy(change => change.CreatedAtUtc)
                .Select(change => new AuditLogChangeResponse(
                    change.Id,
                    change.PropertyName,
                    change.IsSensitive ? null : change.OldValue,
                    change.IsSensitive ? "***" : change.NewValue,
                    change.IsSensitive,
                    change.CreatedAtUtc))
                .ToArray());
    }

    private static string? RedactRawJson(string? value, bool includeRawJson)
    {
        return includeRawJson || string.IsNullOrWhiteSpace(value) ? value : "***";
    }

    private static string? ValidateDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        return fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value
            ? "To date cannot be earlier than from date."
            : null;
    }

    private static AuditDateRange NormalizeDashboardDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var now = DateTime.UtcNow;
        var from = fromUtc ?? now.AddDays(-30);
        var to = toUtc ?? now;

        if (to < from)
        {
            return new AuditDateRange(from, to, "To date cannot be earlier than from date.");
        }

        return new AuditDateRange(from, to.AddTicks(1), null);
    }

    private string? GetIpAddress()
    {
        return httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
    }

    private string? GetCorrelationId()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId)
            && !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return context.TraceIdentifier;
    }

    private static string Normalize(string? value, int maxLength, string fallback = "")
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record AuditDateRange(DateTime FromUtc, DateTime ToExclusiveUtc, string? Error);
}
