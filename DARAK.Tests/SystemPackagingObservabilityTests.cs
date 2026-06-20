using DARAK.Api.Data;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace DARAK.Tests;

public sealed class SystemPackagingObservabilityTests
{
    [Fact]
    public async Task UpsertSettingAsync_CreatesCompoundSettingAndMasksSensitiveValueForScopedAdmin()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P22-SET1");
        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.UpsertSettingAsync(Guid.NewGuid(), new UpsertSystemSettingRequest
        {
            CompoundId = compound.Id,
            Key = "billing.gateway.secret",
            Value = "secret-value",
            ValueType = SystemSettingValueType.String,
            IsSensitive = true,
            Description = "Gateway secret"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Value.Should().Be("***");
        dbContext.SystemSettings.Should().ContainSingle(item => item.Key == "billing.gateway.secret" && item.CompoundId == compound.Id);
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.SystemSettingCreated);
    }

    [Fact]
    public async Task UpsertSettingAsync_RejectsGlobalSettingForNonSuperAdmin()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P22-SET2");
        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.UpsertSettingAsync(Guid.NewGuid(), new UpsertSystemSettingRequest
        {
            Key = "global.backup.strategy",
            Value = "daily",
            ValueType = SystemSettingValueType.String
        });

        result.IsSuccess.Should().BeFalse();
        dbContext.SystemSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateLicenseProfileAsync_SuperAdminCreatesLicenseAndAudit()
    {
        await using var dbContext = TestDb.Create();
        var service = CreateSuperAdminService(dbContext);

        var result = await service.UpdateLicenseProfileAsync(Guid.NewGuid(), new UpdateLicenseProfileRequest
        {
            LicensedTo = "Darak Commercial Buyer",
            LicenseKeyFingerprint = "SHA256:buyer-key",
            Plan = LicensePlan.Enterprise,
            Status = LicenseStatus.Active,
            MaxCompounds = 10,
            MaxUnits = 2500,
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Plan.Should().Be(LicensePlan.Enterprise);
        dbContext.LicenseProfiles.Should().ContainSingle(item => item.LicensedTo == "Darak Commercial Buyer");
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.LicenseProfileCreated);
    }

    [Fact]
    public async Task SetMaintenanceModeAsync_SuperAdminPersistsSettingsAndAudit()
    {
        await using var dbContext = TestDb.Create();
        var service = CreateSuperAdminService(dbContext);

        var result = await service.SetMaintenanceModeAsync(Guid.NewGuid(), new SetMaintenanceModeRequest
        {
            IsEnabled = true,
            Message = "Scheduled upgrade"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.IsEnabled.Should().BeTrue();
        dbContext.SystemSettings.Should().Contain(item => item.Key == "system.maintenance.enabled" && item.Value == "True");
        dbContext.SystemSettings.Should().Contain(item => item.Key == "system.maintenance.message" && item.Value == "Scheduled upgrade");
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.MaintenanceModeChanged);
    }

    [Fact]
    public async Task BackgroundJobRunAsync_TracksStartAndCompletion()
    {
        await using var dbContext = TestDb.Create();
        var service = CreateSuperAdminService(dbContext);

        var started = await service.StartBackgroundJobRunAsync(new StartBackgroundJobRunRequest
        {
            JobName = "notification-outbox-worker",
            WorkerName = "worker-01"
        });
        var completed = await service.CompleteBackgroundJobRunAsync(started.Value!.Id, new CompleteBackgroundJobRunRequest
        {
            Status = BackgroundJobRunStatus.Succeeded,
            ProcessedCount = 9,
            FailedCount = 0
        });

        started.IsSuccess.Should().BeTrue(started.Message);
        completed.IsSuccess.Should().BeTrue(completed.Message);
        completed.Value!.Status.Should().Be(BackgroundJobRunStatus.Succeeded);
        completed.Value.ProcessedCount.Should().Be(9);
        dbContext.BackgroundJobRuns.Should().ContainSingle(item => item.JobName == "notification-outbox-worker" && item.Status == BackgroundJobRunStatus.Succeeded);
    }

    [Fact]
    public async Task IntegrationFailureAsync_DeduplicatesOpenFailuresAndAllowsResolve()
    {
        await using var dbContext = TestDb.Create();
        var actor = Guid.NewGuid();
        var service = CreateSuperAdminService(dbContext);

        var first = await service.RecordIntegrationFailureAsync(new RecordIntegrationFailureRequest
        {
            IntegrationName = "SMS",
            OperationName = "Send",
            ErrorMessage = "Timeout"
        });
        var second = await service.RecordIntegrationFailureAsync(new RecordIntegrationFailureRequest
        {
            IntegrationName = "SMS",
            OperationName = "Send",
            ErrorMessage = "Timeout again"
        });
        var resolved = await service.ResolveIntegrationFailureAsync(actor, first.Value!.Id, new ResolveIntegrationFailureRequest
        {
            ResolutionNote = "Provider recovered."
        });

        second.Value!.OccurrenceCount.Should().Be(2);
        resolved.Value!.Status.Should().Be(IntegrationFailureStatus.Resolved);
        resolved.Value.ResolvedByUserId.Should().Be(actor);
        dbContext.IntegrationFailureEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task GetSystemHealthAsync_CreatesDegradedSnapshotWhenFailuresExist()
    {
        await using var dbContext = TestDb.Create();
        dbContext.IntegrationFailureEvents.Add(new IntegrationFailureEvent
        {
            IntegrationName = "Email",
            OperationName = "Send",
            ErrorMessage = "SMTP down",
            Status = IntegrationFailureStatus.Open
        });
        await dbContext.SaveChangesAsync();
        var service = CreateSuperAdminService(dbContext);

        var result = await service.GetSystemHealthAsync();

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(SystemHealthStatus.Degraded);
        result.Value.OpenIntegrationFailures.Should().Be(1);
        dbContext.SystemHealthSnapshots.Should().ContainSingle(item => item.Status == SystemHealthStatus.Degraded);
    }

    [Fact]
    public async Task GetDeploymentChecklistAsync_ReturnsCommercialReadinessItems()
    {
        await using var dbContext = TestDb.Create();
        dbContext.LicenseProfiles.Add(new LicenseProfile
        {
            LicensedTo = "Buyer",
            LicenseKeyFingerprint = "fingerprint",
            MaxCompounds = 1,
            MaxUnits = 100
        });
        dbContext.SystemSettings.Add(new SystemSetting { Key = "deployment.backup.strategy", Value = "daily", ValueType = SystemSettingValueType.String });
        await AddCompoundAsync(dbContext, "P22-CHK1");
        await dbContext.SaveChangesAsync();
        var service = CreateSuperAdminService(dbContext);

        var result = await service.GetDeploymentChecklistAsync();

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().Contain(item => item.Key == "license" && item.IsCompleted);
        result.Value.Items.Should().Contain(item => item.Key == "compound" && item.IsCompleted);
        result.Value.TotalCount.Should().BeGreaterThan(0);
    }

    private static SystemAdministrationService CreateService(ApplicationDbContext dbContext, Guid[] allowedCompoundIds)
    {
        return new SystemAdministrationService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds),
            new AuditLogService(dbContext, new FakeCompoundAccessService(allowedCompoundIds), new HttpContextAccessor()),
            new FakeHostEnvironment());
    }

    private static SystemAdministrationService CreateSuperAdminService(ApplicationDbContext dbContext)
    {
        var access = new FakeCompoundAccessService(isSuperAdmin: true);
        return new SystemAdministrationService(
            dbContext,
            access,
            new AuditLogService(dbContext, access, new HttpContextAccessor()),
            new FakeHostEnvironment());
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "DARAK.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
