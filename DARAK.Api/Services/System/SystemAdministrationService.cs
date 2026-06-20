using System.Reflection;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using DARAK.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DARAK.Api.Services;

public sealed class SystemAdministrationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService,
    IHostEnvironment hostEnvironment)
    : ISystemAdministrationService
{
    private const string MaintenanceEnabledKey = "system.maintenance.enabled";
    private const string MaintenanceMessageKey = "system.maintenance.message";
    private const int MaxKeyLength = 150;
    private const int MaxValueLength = 4000;

    public SystemVersionResponse GetVersion()
    {
        var assembly = typeof(SystemAdministrationService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        return new SystemVersionResponse(
            "DARAK",
            "v1",
            hostEnvironment.EnvironmentName,
            DateTime.UtcNow,
            informationalVersion);
    }

    public async Task<ServiceResult<PagedResult<SystemSettingResponse>>> SearchSettingsAsync(
        SystemSettingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<SystemSettingResponse>>.Forbidden("Current user cannot access system settings.");
        }

        if (query.CompoundId.HasValue && !CanAccessCompound(scope, query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<SystemSettingResponse>>.Success(new PagedResult<SystemSettingResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var settings = dbContext.SystemSettings.AsNoTracking().AsQueryable();
        settings = ApplySettingScope(settings, scope, query.CompoundId);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            settings = settings.Where(item => item.Key.ToLower().Contains(term) || (item.Description != null && item.Description.ToLower().Contains(term)));
        }

        var totalCount = await settings.CountAsync(cancellationToken);
        var items = await settings
            .OrderBy(item => item.CompoundId.HasValue)
            .ThenBy(item => item.Key)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => ToSettingResponse(item, query.IncludeSensitiveValues && scope.IsSuperAdmin))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<SystemSettingResponse>>.Success(new PagedResult<SystemSettingResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<SystemSettingResponse>> UpsertSettingAsync(
        Guid? currentUserId,
        UpsertSystemSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateSettingRequest(request);
        if (validation is not null)
        {
            return ServiceResult<SystemSettingResponse>.BadRequest(validation);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<SystemSettingResponse>.Forbidden("Current user cannot update system settings.");
        }

        if (!request.CompoundId.HasValue && !scope.IsSuperAdmin)
        {
            return ServiceResult<SystemSettingResponse>.Forbidden("Only SuperAdmin can manage global settings.");
        }

        if (request.CompoundId.HasValue && !CanAccessCompound(scope, request.CompoundId.Value))
        {
            return ServiceResult<SystemSettingResponse>.NotFound("System setting was not found.");
        }

        var normalizedKey = Normalize(request.Key, MaxKeyLength).ToLowerInvariant();
        var setting = await dbContext.SystemSettings
            .FirstOrDefaultAsync(item => item.CompoundId == request.CompoundId && item.Key == normalizedKey, cancellationToken);

        var now = DateTime.UtcNow;
        var actionType = AuditActionType.SystemSettingUpdated;
        if (setting is null)
        {
            actionType = AuditActionType.SystemSettingCreated;
            setting = new SystemSetting
            {
                Id = Guid.NewGuid(),
                CompoundId = request.CompoundId,
                Key = normalizedKey,
                CreatedAtUtc = now
            };
            dbContext.SystemSettings.Add(setting);
        }
        else if (setting.IsReadOnly && !scope.IsSuperAdmin)
        {
            return ServiceResult<SystemSettingResponse>.Conflict("This setting is read-only.");
        }

        var previousValue = setting.Value;
        setting.Value = Normalize(request.Value, MaxValueLength);
        setting.ValueType = request.ValueType;
        setting.Description = NormalizeOptional(request.Description, 1000);
        setting.IsSensitive = request.IsSensitive;
        setting.IsReadOnly = request.IsReadOnly;
        setting.UpdatedByUserId = currentUserId;
        setting.UpdatedAtUtc = now;

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            setting.CompoundId,
            null,
            currentUserId,
            null,
            actionType,
            AuditEntityType.SystemSetting,
            setting.Id,
            setting.IsSensitive ? AuditSeverity.Medium : AuditSeverity.Low,
            "System",
            $"System setting '{setting.Key}' was {(actionType == AuditActionType.SystemSettingCreated ? "created" : "updated")}.",
            Changes:
            [
                new AuditLogChangeRecord("Value", setting.IsSensitive ? "***" : previousValue, setting.IsSensitive ? "***" : setting.Value, setting.IsSensitive)
            ]),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<SystemSettingResponse>.Success(ToSettingResponse(setting, includeSensitiveValue: scope.IsSuperAdmin));
    }

    public async Task<ServiceResult<LicenseProfileResponse>> GetLicenseProfileAsync(CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsSuperAdmin)
        {
            return ServiceResult<LicenseProfileResponse>.Forbidden("Only SuperAdmin can access license profile.");
        }

        var license = await dbContext.LicenseProfiles.AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (license is null)
        {
            return ServiceResult<LicenseProfileResponse>.NotFound("License profile was not found.");
        }

        return ServiceResult<LicenseProfileResponse>.Success(ToLicenseResponse(license));
    }

    public async Task<ServiceResult<LicenseProfileResponse>> UpdateLicenseProfileAsync(
        Guid? currentUserId,
        UpdateLicenseProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsSuperAdmin)
        {
            return ServiceResult<LicenseProfileResponse>.Forbidden("Only SuperAdmin can update license profile.");
        }

        if (request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return ServiceResult<LicenseProfileResponse>.BadRequest("License expiry must be in the future.");
        }

        var license = await dbContext.LicenseProfiles
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var isCreated = license is null;
        var now = DateTime.UtcNow;
        if (license is null)
        {
            license = new LicenseProfile
            {
                Id = Guid.NewGuid(),
                IssuedAtUtc = now,
                CreatedAtUtc = now
            };
            dbContext.LicenseProfiles.Add(license);
        }

        license.LicensedTo = Normalize(request.LicensedTo, 200);
        license.LicenseKeyFingerprint = Normalize(request.LicenseKeyFingerprint, 128);
        license.Plan = request.Plan;
        license.Status = request.Status;
        license.MaxCompounds = request.MaxCompounds;
        license.MaxUnits = request.MaxUnits;
        license.ExpiresAtUtc = request.ExpiresAtUtc;
        license.Notes = NormalizeOptional(request.Notes, 1000);
        license.UpdatedByUserId = currentUserId;
        license.UpdatedAtUtc = now;

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            null,
            null,
            currentUserId,
            null,
            isCreated ? AuditActionType.LicenseProfileCreated : AuditActionType.LicenseProfileUpdated,
            AuditEntityType.LicenseProfile,
            license.Id,
            AuditSeverity.High,
            "System",
            $"License profile for '{license.LicensedTo}' was {(isCreated ? "created" : "updated")}.",
            MetadataJson: $"{{\"plan\":\"{license.Plan}\",\"status\":\"{license.Status}\"}}"),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<LicenseProfileResponse>.Success(ToLicenseResponse(license));
    }

    public async Task<ServiceResult<MaintenanceModeResponse>> GetMaintenanceModeAsync(CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<MaintenanceModeResponse>.Forbidden("Current user cannot access maintenance mode.");
        }

        return ServiceResult<MaintenanceModeResponse>.Success(await ReadMaintenanceModeAsync(cancellationToken));
    }

    public async Task<ServiceResult<MaintenanceModeResponse>> SetMaintenanceModeAsync(
        Guid? currentUserId,
        SetMaintenanceModeRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsSuperAdmin)
        {
            return ServiceResult<MaintenanceModeResponse>.Forbidden("Only SuperAdmin can update maintenance mode.");
        }

        var enabled = await UpsertGlobalSettingInternalAsync(currentUserId, MaintenanceEnabledKey, request.IsEnabled.ToString(), SystemSettingValueType.Boolean, "Global maintenance mode flag.", cancellationToken);
        await UpsertGlobalSettingInternalAsync(currentUserId, MaintenanceMessageKey, request.Message ?? string.Empty, SystemSettingValueType.String, "Global maintenance mode message.", cancellationToken);

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            null,
            null,
            currentUserId,
            null,
            AuditActionType.MaintenanceModeChanged,
            AuditEntityType.SystemSetting,
            enabled.Id,
            request.IsEnabled ? AuditSeverity.High : AuditSeverity.Medium,
            "System",
            request.IsEnabled ? "Maintenance mode was enabled." : "Maintenance mode was disabled."),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<MaintenanceModeResponse>.Success(await ReadMaintenanceModeAsync(cancellationToken));
    }

    public async Task<ServiceResult<DeploymentChecklistResponse>> GetDeploymentChecklistAsync(CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<DeploymentChecklistResponse>.Forbidden("Current user cannot access deployment checklist.");
        }

        var hasLicense = await dbContext.LicenseProfiles.AnyAsync(cancellationToken);
        var hasCompounds = await dbContext.Compounds.AnyAsync(cancellationToken);
        var hasAudit = await dbContext.AuditLogEntries.AnyAsync(cancellationToken);
        var hasSettings = await dbContext.SystemSettings.AnyAsync(cancellationToken);
        var hasNotificationTemplates = await dbContext.NotificationTemplates.AnyAsync(cancellationToken);
        var hasRecentHealthSnapshot = await dbContext.SystemHealthSnapshots.AnyAsync(item => item.CapturedAtUtc >= DateTime.UtcNow.AddDays(-1), cancellationToken);

        var items = new[]
        {
            new DeploymentChecklistItemResponse("license", "License profile configured", hasLicense, "Configure a real commercial license profile before handover."),
            new DeploymentChecklistItemResponse("compound", "At least one compound exists", hasCompounds, "Create the buyer's first compound and assignments."),
            new DeploymentChecklistItemResponse("audit", "Audit trail contains entries", hasAudit, "Keep audit trail enabled for legal traceability."),
            new DeploymentChecklistItemResponse("settings", "System settings configured", hasSettings, "Configure backup, support, and maintenance settings."),
            new DeploymentChecklistItemResponse("notifications", "Notification templates available", hasNotificationTemplates, "Prepare templates for billing, support, documents, and alerts."),
            new DeploymentChecklistItemResponse("health", "System health snapshot captured", hasRecentHealthSnapshot, "Capture health snapshots during deployment verification.")
        };

        var completed = items.Count(item => item.IsCompleted);
        return ServiceResult<DeploymentChecklistResponse>.Success(new DeploymentChecklistResponse(items, completed, items.Length, completed == items.Length));
    }

    public async Task<ServiceResult<SystemHealthDashboardResponse>> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<SystemHealthDashboardResponse>.Forbidden("Current user cannot access system health.");
        }

        var now = DateTime.UtcNow;
        var pendingNotifications = await dbContext.NotificationOutboxes.CountAsync(item => item.Status == NotificationStatus.Pending || item.Status == NotificationStatus.Processing, cancellationToken);
        var failedNotifications = await dbContext.NotificationOutboxes.CountAsync(item => item.Status == NotificationStatus.Failed, cancellationToken);
        var openFailures = await dbContext.IntegrationFailureEvents.CountAsync(item => item.Status == IntegrationFailureStatus.Open || item.Status == IntegrationFailureStatus.Acknowledged, cancellationToken);
        var failedJobs = await dbContext.BackgroundJobRuns.CountAsync(item => item.Status == BackgroundJobRunStatus.Failed && item.StartedAtUtc >= now.AddHours(-24), cancellationToken);

        var status = failedNotifications > 0 || openFailures > 0 || failedJobs > 0
            ? SystemHealthStatus.Degraded
            : SystemHealthStatus.Healthy;

        var summary = status == SystemHealthStatus.Healthy
            ? "System is healthy."
            : "System has operational warnings requiring review.";

        var snapshot = new SystemHealthSnapshot
        {
            Id = Guid.NewGuid(),
            Status = status,
            PendingNotifications = pendingNotifications,
            FailedNotifications = failedNotifications,
            OpenIntegrationFailures = openFailures,
            FailedBackgroundJobs24h = failedJobs,
            Summary = summary,
            CapturedAtUtc = now
        };
        dbContext.SystemHealthSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SystemHealthDashboardResponse>.Success(ToHealthResponse(snapshot));
    }

    public async Task<ServiceResult<PagedResult<BackgroundJobRunResponse>>> SearchBackgroundJobRunsAsync(
        BackgroundJobRunSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var runs = dbContext.BackgroundJobRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.JobName))
        {
            var jobName = query.JobName.Trim().ToLower();
            runs = runs.Where(item => item.JobName.ToLower().Contains(jobName));
        }
        if (query.Status.HasValue)
        {
            runs = runs.Where(item => item.Status == query.Status.Value);
        }

        var totalCount = await runs.CountAsync(cancellationToken);
        var items = await runs
            .OrderByDescending(item => item.StartedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => ToBackgroundJobRunResponse(item))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<BackgroundJobRunResponse>>.Success(new PagedResult<BackgroundJobRunResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<BackgroundJobRunResponse>> StartBackgroundJobRunAsync(
        StartBackgroundJobRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.JobName))
        {
            return ServiceResult<BackgroundJobRunResponse>.BadRequest("Job name is required.");
        }

        var run = new BackgroundJobRun
        {
            Id = Guid.NewGuid(),
            JobName = Normalize(request.JobName, 150),
            WorkerName = NormalizeOptional(request.WorkerName, 150),
            MetadataJson = NormalizeOptional(request.MetadataJson, 4000),
            Status = BackgroundJobRunStatus.Running,
            StartedAtUtc = DateTime.UtcNow
        };
        dbContext.BackgroundJobRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<BackgroundJobRunResponse>.Success(ToBackgroundJobRunResponse(run));
    }

    public async Task<ServiceResult<BackgroundJobRunResponse>> CompleteBackgroundJobRunAsync(
        Guid id,
        CompleteBackgroundJobRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var run = await dbContext.BackgroundJobRuns.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null)
        {
            return ServiceResult<BackgroundJobRunResponse>.NotFound("Background job run was not found.");
        }
        if (run.Status != BackgroundJobRunStatus.Running)
        {
            return ServiceResult<BackgroundJobRunResponse>.Conflict("Only running jobs can be completed.");
        }
        if (request.Status == BackgroundJobRunStatus.Running)
        {
            return ServiceResult<BackgroundJobRunResponse>.BadRequest("Completed job status cannot be Running.");
        }

        var now = DateTime.UtcNow;
        run.Status = request.Status;
        run.CompletedAtUtc = now;
        run.DurationMs = (int)Math.Min(int.MaxValue, Math.Max(0, (now - run.StartedAtUtc).TotalMilliseconds));
        run.ProcessedCount = request.ProcessedCount;
        run.FailedCount = request.FailedCount;
        run.ErrorMessage = NormalizeOptional(request.ErrorMessage, 1000);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BackgroundJobRunResponse>.Success(ToBackgroundJobRunResponse(run));
    }

    public async Task<ServiceResult<PagedResult<IntegrationFailureEventResponse>>> SearchIntegrationFailuresAsync(
        IntegrationFailureSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var failures = dbContext.IntegrationFailureEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.IntegrationName))
        {
            var name = query.IntegrationName.Trim().ToLower();
            failures = failures.Where(item => item.IntegrationName.ToLower().Contains(name));
        }
        if (query.Status.HasValue)
        {
            failures = failures.Where(item => item.Status == query.Status.Value);
        }

        var totalCount = await failures.CountAsync(cancellationToken);
        var items = await failures
            .OrderByDescending(item => item.LastOccurredAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => ToIntegrationFailureResponse(item))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<IntegrationFailureEventResponse>>.Success(new PagedResult<IntegrationFailureEventResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<IntegrationFailureEventResponse>> RecordIntegrationFailureAsync(
        RecordIntegrationFailureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IntegrationName) || string.IsNullOrWhiteSpace(request.OperationName))
        {
            return ServiceResult<IntegrationFailureEventResponse>.BadRequest("Integration and operation names are required.");
        }

        var now = DateTime.UtcNow;
        var integrationName = Normalize(request.IntegrationName, 150);
        var operationName = Normalize(request.OperationName, 150);
        var failure = await dbContext.IntegrationFailureEvents
            .FirstOrDefaultAsync(item => item.IntegrationName == integrationName && item.OperationName == operationName && item.Status != IntegrationFailureStatus.Resolved, cancellationToken);

        if (failure is null)
        {
            failure = new IntegrationFailureEvent
            {
                Id = Guid.NewGuid(),
                IntegrationName = integrationName,
                OperationName = operationName,
                FirstOccurredAtUtc = now
            };
            dbContext.IntegrationFailureEvents.Add(failure);
        }
        else
        {
            failure.OccurrenceCount++;
        }

        failure.Status = IntegrationFailureStatus.Open;
        failure.ErrorMessage = Normalize(request.ErrorMessage, 1000);
        failure.MetadataJson = NormalizeOptional(request.MetadataJson, 4000);
        failure.LastOccurredAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<IntegrationFailureEventResponse>.Success(ToIntegrationFailureResponse(failure));
    }

    public async Task<ServiceResult<IntegrationFailureEventResponse>> ResolveIntegrationFailureAsync(
        Guid? currentUserId,
        Guid id,
        ResolveIntegrationFailureRequest request,
        CancellationToken cancellationToken = default)
    {
        var failure = await dbContext.IntegrationFailureEvents.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (failure is null)
        {
            return ServiceResult<IntegrationFailureEventResponse>.NotFound("Integration failure was not found.");
        }
        if (failure.Status == IntegrationFailureStatus.Resolved)
        {
            return ServiceResult<IntegrationFailureEventResponse>.Conflict("Integration failure is already resolved.");
        }

        failure.Status = IntegrationFailureStatus.Resolved;
        failure.ResolvedAtUtc = DateTime.UtcNow;
        failure.ResolvedByUserId = currentUserId;
        failure.ResolutionNote = NormalizeOptional(request.ResolutionNote, 1000);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<IntegrationFailureEventResponse>.Success(ToIntegrationFailureResponse(failure));
    }

    private async Task<SystemSetting> UpsertGlobalSettingInternalAsync(
        Guid? currentUserId,
        string key,
        string value,
        SystemSettingValueType type,
        string description,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.SystemSettings.FirstOrDefaultAsync(item => item.CompoundId == null && item.Key == key, cancellationToken);
        var now = DateTime.UtcNow;
        if (setting is null)
        {
            setting = new SystemSetting { Id = Guid.NewGuid(), Key = key, CreatedAtUtc = now };
            dbContext.SystemSettings.Add(setting);
        }
        setting.Value = value;
        setting.ValueType = type;
        setting.Description = description;
        setting.IsSensitive = false;
        setting.IsReadOnly = true;
        setting.UpdatedByUserId = currentUserId;
        setting.UpdatedAtUtc = now;
        return setting;
    }

    private async Task<MaintenanceModeResponse> ReadMaintenanceModeAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.SystemSettings.AsNoTracking()
            .Where(item => item.CompoundId == null && (item.Key == MaintenanceEnabledKey || item.Key == MaintenanceMessageKey))
            .ToArrayAsync(cancellationToken);

        var enabledSetting = settings.SingleOrDefault(item => item.Key == MaintenanceEnabledKey);
        var messageSetting = settings.SingleOrDefault(item => item.Key == MaintenanceMessageKey);
        var enabled = bool.TryParse(enabledSetting?.Value, out var isEnabled) && isEnabled;
        var updated = new[] { enabledSetting?.UpdatedAtUtc, messageSetting?.UpdatedAtUtc }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max();

        return new MaintenanceModeResponse(enabled, string.IsNullOrWhiteSpace(messageSetting?.Value) ? null : messageSetting.Value, updated == default ? null : updated);
    }

    private static IQueryable<SystemSetting> ApplySettingScope(IQueryable<SystemSetting> query, CompoundAccessScope scope, Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.CompoundId == compoundId.Value);
        }
        if (scope.IsSuperAdmin)
        {
            return query;
        }
        return query.Where(item => item.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(item.CompoundId.Value));
    }

    private static bool CanAccessCompound(CompoundAccessScope scope, Guid compoundId)
    {
        return scope.IsSuperAdmin || scope.AllowedCompoundIds.Contains(compoundId);
    }

    private static string? ValidateSettingRequest(UpsertSystemSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return "Setting key is required.";
        }
        if (request.Key.Length > MaxKeyLength)
        {
            return $"Setting key cannot exceed {MaxKeyLength} characters.";
        }
        if (request.Value.Length > MaxValueLength)
        {
            return $"Setting value cannot exceed {MaxValueLength} characters.";
        }
        return request.ValueType switch
        {
            SystemSettingValueType.Boolean when !bool.TryParse(request.Value, out _) => "Boolean setting value must be true or false.",
            SystemSettingValueType.Integer when !int.TryParse(request.Value, out _) => "Integer setting value must be a valid integer.",
            SystemSettingValueType.Decimal when !decimal.TryParse(request.Value, out _) => "Decimal setting value must be a valid decimal number.",
            _ => null
        };
    }

    private static SystemSettingResponse ToSettingResponse(SystemSetting setting, bool includeSensitiveValue)
    {
        return new SystemSettingResponse(
            setting.Id,
            setting.CompoundId,
            setting.Key,
            setting.IsSensitive && !includeSensitiveValue ? "***" : setting.Value,
            setting.ValueType,
            setting.Scope,
            setting.Description,
            setting.IsSensitive,
            setting.IsReadOnly,
            setting.UpdatedByUserId,
            setting.CreatedAtUtc,
            setting.UpdatedAtUtc);
    }

    private static LicenseProfileResponse ToLicenseResponse(LicenseProfile license)
    {
        return new LicenseProfileResponse(
            license.Id,
            license.LicensedTo,
            license.LicenseKeyFingerprint,
            license.Plan,
            license.Status,
            license.MaxCompounds,
            license.MaxUnits,
            license.IssuedAtUtc,
            license.ExpiresAtUtc,
            license.Notes,
            license.UpdatedByUserId,
            license.CreatedAtUtc,
            license.UpdatedAtUtc);
    }

    private static SystemHealthDashboardResponse ToHealthResponse(SystemHealthSnapshot snapshot)
    {
        return new SystemHealthDashboardResponse(
            snapshot.Status,
            snapshot.PendingNotifications,
            snapshot.FailedNotifications,
            snapshot.OpenIntegrationFailures,
            snapshot.FailedBackgroundJobs24h,
            snapshot.CapturedAtUtc,
            snapshot.Summary);
    }

    private static BackgroundJobRunResponse ToBackgroundJobRunResponse(BackgroundJobRun run)
    {
        return new BackgroundJobRunResponse(run.Id, run.JobName, run.WorkerName, run.Status, run.StartedAtUtc, run.CompletedAtUtc, run.DurationMs, run.ProcessedCount, run.FailedCount, run.ErrorMessage, run.MetadataJson);
    }

    private static IntegrationFailureEventResponse ToIntegrationFailureResponse(IntegrationFailureEvent failure)
    {
        return new IntegrationFailureEventResponse(failure.Id, failure.IntegrationName, failure.OperationName, failure.Status, failure.ErrorMessage, failure.OccurrenceCount, failure.FirstOccurredAtUtc, failure.LastOccurredAtUtc, failure.ResolvedAtUtc, failure.ResolvedByUserId, failure.ResolutionNote, failure.MetadataJson);
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = value.Trim();
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
}
