using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task AppendEntryAsync_StoresChangeSetAndMasksSensitiveValuesInDetails()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AUD-1");
        var service = CreateService(dbContext, compound.Id);

        var id = await service.AppendEntryAsync(new AuditLogRecord(
            compound.Id,
            null,
            Guid.NewGuid(),
            "SuperAdmin",
            AuditActionType.Updated,
            AuditEntityType.Payment,
            Guid.NewGuid(),
            AuditSeverity.High,
            "Payments",
            "Payment metadata updated.",
            "Correction approved by finance.",
            BeforeValuesJson: "{\"secret\":\"before\"}",
            AfterValuesJson: "{\"secret\":\"after\"}",
            MetadataJson: "{\"token\":\"metadata\"}",
            Changes:
            [
                new AuditLogChangeRecord("Amount", "100", "120"),
                new AuditLogChangeRecord("SecretToken", "old-secret", "new-secret", IsSensitive: true)
            ]));
        await dbContext.SaveChangesAsync();

        var details = await service.GetDetailsAsync(id);

        details.IsSuccess.Should().BeTrue(details.Message);
        details.Value!.Changes.Should().HaveCount(2);
        details.Value.Changes.Should().Contain(change =>
            change.PropertyName == "SecretToken"
            && change.OldValue == null
            && change.NewValue == "***"
            && change.IsSensitive);
        details.Value.BeforeValuesJson.Should().Be("***");
        details.Value.AfterValuesJson.Should().Be("***");
        details.Value.MetadataJson.Should().Be("***");
    }

    [Fact]
    public async Task GetDetailsAsync_ExposesRawJsonOnlyForSuperAdmin()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AUD-RAW");
        var service = CreateSuperAdminService(dbContext);

        var id = await service.AppendEntryAsync(new AuditLogRecord(
            compound.Id,
            null,
            Guid.NewGuid(),
            "SuperAdmin",
            AuditActionType.Updated,
            AuditEntityType.Payment,
            Guid.NewGuid(),
            AuditSeverity.High,
            "Payments",
            "Payment raw JSON updated.",
            BeforeValuesJson: "{\"secret\":\"before\"}",
            AfterValuesJson: "{\"secret\":\"after\"}",
            MetadataJson: "{\"token\":\"metadata\"}"));
        await dbContext.SaveChangesAsync();

        var details = await service.GetDetailsAsync(id);

        details.IsSuccess.Should().BeTrue(details.Message);
        details.Value!.BeforeValuesJson.Should().Be("{\"secret\":\"before\"}");
        details.Value.AfterValuesJson.Should().Be("{\"secret\":\"after\"}");
        details.Value.MetadataJson.Should().Be("{\"token\":\"metadata\"}");
    }

    [Fact]
    public async Task SearchAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "AUD-2A");
        var blocked = await AddCompoundAsync(dbContext, "AUD-2B");
        dbContext.AuditLogEntries.AddRange(
            NewAudit(allowed.Id, AuditActionType.Created, AuditEntityType.UtilityBill, "Allowed bill"),
            NewAudit(blocked.Id, AuditActionType.Created, AuditEntityType.UtilityBill, "Blocked bill"));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.SearchAsync(new AuditSearchQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.Single().CompoundId.Should().Be(allowed.Id);
    }

    [Fact]
    public async Task GetDetailsAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "AUD-3A");
        var blocked = await AddCompoundAsync(dbContext, "AUD-3B");
        var audit = NewAudit(blocked.Id, AuditActionType.Deleted, AuditEntityType.Document, "Blocked document delete");
        dbContext.AuditLogEntries.Add(audit);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.GetDetailsAsync(audit.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GetResidentTrailAsync_ReturnsOnlyResidentScopedAuditEntries()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AUD-4");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Audit Resident");
        var otherResident = await AddResidentAsync(dbContext, compound.Id, "Other Resident");
        dbContext.AuditLogEntries.AddRange(
            NewAudit(compound.Id, AuditActionType.FinancialAdjustmentApplied, AuditEntityType.FinancialAdjustment, "Resident adjustment", resident.Id),
            NewAudit(compound.Id, AuditActionType.FinancialAdjustmentApplied, AuditEntityType.FinancialAdjustment, "Other adjustment", otherResident.Id));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetResidentTrailAsync(resident.Id, new AuditEntityTrailQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.Single().ResidentProfileId.Should().Be(resident.Id);
    }

    [Fact]
    public async Task GetEntityTrailAsync_ReturnsMatchingEntityHistory()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AUD-5");
        var paymentId = Guid.NewGuid();
        dbContext.AuditLogEntries.AddRange(
            NewAudit(compound.Id, AuditActionType.Created, AuditEntityType.Payment, "Payment created", entityId: paymentId),
            NewAudit(compound.Id, AuditActionType.Updated, AuditEntityType.Payment, "Payment updated", entityId: paymentId),
            NewAudit(compound.Id, AuditActionType.Created, AuditEntityType.UtilityBill, "Bill created", entityId: Guid.NewGuid()));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetEntityTrailAsync(AuditEntityType.Payment, paymentId, new AuditEntityTrailQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(item => item.EntityType == AuditEntityType.Payment && item.EntityId == paymentId);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsSeverityActionsEntitiesAndSources()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AUD-6");
        dbContext.AuditLogEntries.AddRange(
            NewAudit(compound.Id, AuditActionType.ApprovalApproved, AuditEntityType.ApprovalRequest, "Approved", severity: AuditSeverity.Critical, sourceModule: "Approvals"),
            NewAudit(compound.Id, AuditActionType.FinancialAdjustmentApplied, AuditEntityType.FinancialAdjustment, "Applied", severity: AuditSeverity.High, sourceModule: "Finance"),
            NewAudit(compound.Id, AuditActionType.FinancialAdjustmentApplied, AuditEntityType.FinancialAdjustment, "Applied again", severity: AuditSeverity.High, sourceModule: "Finance"));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetDashboardAsync(new AuditDashboardQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalCount.Should().Be(3);
        result.Value.CriticalCount.Should().Be(1);
        result.Value.HighCount.Should().Be(2);
        result.Value.ByAction.Should().Contain(item => item.ActionType == AuditActionType.FinancialAdjustmentApplied && item.Count == 2);
        result.Value.ByEntity.Should().Contain(item => item.EntityType == AuditEntityType.FinancialAdjustment && item.Count == 2);
        result.Value.BySourceModule.Should().Contain(item => item.SourceModule == "Finance" && item.Count == 2);
    }

    private static AuditLogService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new AuditLogService(dbContext, new FakeCompoundAccessService(allowedCompoundIds), new HttpContextAccessor());
    }

    private static AuditLogService CreateSuperAdminService(ApplicationDbContext dbContext)
    {
        return new AuditLogService(dbContext, new FakeCompoundAccessService(isSuperAdmin: true), new HttpContextAccessor());
    }

    private static AuditLogEntry NewAudit(
        Guid compoundId,
        AuditActionType actionType,
        AuditEntityType entityType,
        string description,
        Guid? residentProfileId = null,
        Guid? entityId = null,
        AuditSeverity severity = AuditSeverity.Medium,
        string sourceModule = "Tests")
    {
        return new AuditLogEntry
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            ActorUserId = Guid.NewGuid(),
            ActorRole = "SuperAdmin",
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId ?? Guid.NewGuid(),
            Severity = severity,
            SourceModule = sourceModule,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada"
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compoundId,
            FullName = fullName,
            PhoneNumber = "+9647700000000",
            IsActive = true
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }
}

