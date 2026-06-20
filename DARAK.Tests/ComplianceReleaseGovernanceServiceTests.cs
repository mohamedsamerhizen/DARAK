using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ComplianceReleaseGovernanceServiceTests
{
    [Fact]
    public async Task GetReleaseReadinessBoardAsync_ReturnsBlockedWhenCriticalReleaseEvidenceIsMissing()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedReleaseScenarioAsync(dbContext);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetReleaseReadinessBoardAsync(new ReleaseGovernanceQuery
        {
            CompoundId = compound.Id,
            Days = 30
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ReleaseStatus.Should().Be("Blocked");
        result.Value.BlockerCount.Should().BeGreaterThan(0);
        result.Value.Items.Should().Contain(item => item.Key == "critical-audit" && !item.IsPassed);
        result.Value.RecommendedActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetComplianceExceptionQueueAsync_CombinesAuditNotificationsIntegrationsAndApprovals()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedReleaseScenarioAsync(dbContext);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetComplianceExceptionQueueAsync(new ReleaseGovernanceQuery
        {
            CompoundId = compound.Id,
            ItemLimit = 20
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalCount.Should().BeGreaterThanOrEqualTo(4);
        result.Value.Items.Should().Contain(item => item.SourceType == "NotificationOutbox");
        result.Value.Items.Should().Contain(item => item.SourceType == "IntegrationFailure");
        result.Value.Items.Should().Contain(item => item.SourceType == "ApprovalRequest");
        result.Value.Items.Should().Contain(item => item.SourceType == "AuditLogEntry");
    }

    [Fact]
    public async Task GetAuditEvidenceDashboardAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await SeedReleaseScenarioAsync(dbContext);
        var other = await AddCompoundAsync(dbContext, "P8-OTHER");
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            CompoundId = other.Id,
            ActionType = AuditActionType.SystemSettingUpdated,
            EntityType = AuditEntityType.SystemSetting,
            Severity = AuditSeverity.Critical,
            SourceModule = "Other",
            Description = "Other compound critical audit.",
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.GetAuditEvidenceDashboardAsync(new ReleaseGovernanceQuery
        {
            Days = 30
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.SourceModules.Should().NotContain(item => item.SourceModule == "Other");
        result.Value.CriticalEvents.Should().Be(1);
    }

    [Fact]
    public async Task GetBuyerHandoffReadinessAsync_ReturnsConditionalBuyerSummary()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedReleaseScenarioAsync(dbContext);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetBuyerHandoffReadinessAsync(new ReleaseGovernanceQuery
        {
            CompoundId = compound.Id
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.HandoffScore.Should().BeGreaterThan(0);
        result.Value.Items.Should().Contain(item => item.Area == "Commercial");
        result.Value.HandoffNotes.Should().NotBeNull();
    }

    [Fact]
    public async Task GetGovernanceTimelineAsync_ReturnsRecentGovernanceEvents()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedReleaseScenarioAsync(dbContext);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetGovernanceTimelineAsync(new ReleaseGovernanceQuery
        {
            CompoundId = compound.Id,
            ItemLimit = 10
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().Contain(item => item.EventType == "Audit");
        result.Value.Items.Should().Contain(item => item.EventType == "IntegrationFailure");
        result.Value.Items.Should().Contain(item => item.EventType == "BackgroundJob");
        result.Value.Items.Should().Contain(item => item.EventType == "SystemHealth");
    }

    private static ComplianceReleaseGovernanceService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new ComplianceReleaseGovernanceService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<Compound> SeedReleaseScenarioAsync(ApplicationDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var compound = await AddCompoundAsync(dbContext, "P8-COMP");
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "P8-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available
        };
        dbContext.PropertyUnits.Add(unit);
        dbContext.LicenseProfiles.Add(new LicenseProfile
        {
            LicensedTo = "Pack8 Buyer",
            LicenseKeyFingerprint = "P8-LICENSE",
            Status = LicenseStatus.Active,
            Plan = LicensePlan.Enterprise,
            MaxCompounds = 10,
            MaxUnits = 1000,
            ExpiresAtUtc = now.AddYears(1)
        });
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            CompoundId = compound.Id,
            ActionType = AuditActionType.SystemSettingUpdated,
            EntityType = AuditEntityType.SystemSetting,
            Severity = AuditSeverity.Critical,
            SourceModule = "System",
            Description = "Critical release audit event.",
            CreatedAtUtc = now.AddHours(-2),
            CorrelationId = "P8-CORR"
        });
        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            CompoundId = compound.Id,
            Channel = NotificationChannel.Email,
            EventType = NotificationEventType.General,
            Priority = NotificationPriority.Urgent,
            Status = NotificationStatus.Failed,
            RecipientName = "Buyer Admin",
            Subject = "Release evidence",
            Body = "Failed evidence notification.",
            LastError = "SMTP failure",
            FailedAtUtc = now.AddHours(-1)
        });
        dbContext.IntegrationFailureEvents.Add(new IntegrationFailureEvent
        {
            IntegrationName = "Email",
            OperationName = "Send",
            Status = IntegrationFailureStatus.Open,
            ErrorMessage = "SMTP timeout",
            OccurrenceCount = 4,
            FirstOccurredAtUtc = now.AddHours(-3),
            LastOccurredAtUtc = now.AddHours(-1)
        });
        dbContext.BackgroundJobRuns.Add(new BackgroundJobRun
        {
            JobName = "release-verification",
            Status = BackgroundJobRunStatus.Failed,
            StartedAtUtc = now.AddMinutes(-45),
            CompletedAtUtc = now.AddMinutes(-40),
            FailedCount = 1,
            ErrorMessage = "Verification failed."
        });
        dbContext.ApprovalRequests.Add(new ApprovalRequest
        {
            CompoundId = compound.Id,
            RequestedByUserId = Guid.NewGuid(),
            ActionType = ApprovalActionType.ManualFinancialCorrection,
            EntityType = ApprovalEntityType.Other,
            Status = ApprovalStatus.Pending,
            Priority = ApprovalPriority.High,
            Reason = "Release-blocking approval.",
            CreatedAtUtc = now.AddHours(-5),
            DueAtUtc = now.AddHours(2)
        });
        dbContext.SystemHealthSnapshots.Add(new SystemHealthSnapshot
        {
            Status = SystemHealthStatus.Degraded,
            PendingNotifications = 1,
            FailedNotifications = 1,
            OpenIntegrationFailures = 1,
            FailedBackgroundJobs24h = 1,
            Summary = "Release verification degraded.",
            CapturedAtUtc = now.AddMinutes(-15)
        });
        dbContext.NotificationTemplates.Add(new NotificationTemplate
        {
            EventType = NotificationEventType.General,
            Channel = NotificationChannel.Email,
            Code = "release.general",
            SubjectTemplate = "Release",
            BodyTemplate = "Release body"
        });
        dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "release.backup.strategy",
            Value = "daily",
            ValueType = SystemSettingValueType.String,
            Description = "Release backup strategy"
        });
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = "Pack8 " + code,
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
}
