using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class Phase8FDocumentComplianceProTests
{
    [Fact]
    public async Task GetComplianceReportAsync_CountsCompliantAndNonCompliantResidents()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC8F-COMP");
        var compliantResident = await AddResidentAsync(dbContext, compound.Id, "Compliant Resident");
        var missingResident = await AddResidentAsync(dbContext, compound.Id, "Missing Resident");
        await AddMandatoryRequirementAsync(dbContext, compound.Id, DocumentCategory.ResidentIdentity, requiresApproval: true);
        await AddDocumentAsync(
            dbContext,
            compound.Id,
            compliantResident.UserId,
            DocumentCategory.ResidentIdentity,
            DocumentApprovalStatus.Approved,
            DateTime.UtcNow.AddDays(90));

        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.GetComplianceReportAsync(compound.Id);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalResidents.Should().Be(2);
        result.Value.FullyCompliantResidentCount.Should().Be(1);
        result.Value.NonCompliantResidentCount.Should().Be(1);
        result.Value.MissingMandatoryDocumentCount.Should().Be(1);
        result.Value.Residents.Single(item => item.ResidentProfileId == compliantResident.Id).IsCompliant.Should().BeTrue();
        var nonCompliant = result.Value.Residents.Single(item => item.ResidentProfileId == missingResident.Id);
        nonCompliant.IsCompliant.Should().BeFalse();
        nonCompliant.Gaps.Single().Reason.Should().Be("Missing");
    }

    [Fact]
    public async Task GetComplianceReportAsync_FlagsExpiredAndExpiringSoonDocuments()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC8F-EXP");
        var expiredResident = await AddResidentAsync(dbContext, compound.Id, "Expired Resident");
        var expiringResident = await AddResidentAsync(dbContext, compound.Id, "Expiring Resident");
        await AddMandatoryRequirementAsync(dbContext, compound.Id, DocumentCategory.ResidentIdentity, requiresApproval: true);
        await AddDocumentAsync(
            dbContext,
            compound.Id,
            expiredResident.UserId,
            DocumentCategory.ResidentIdentity,
            DocumentApprovalStatus.Approved,
            DateTime.UtcNow.AddDays(-1));
        await AddDocumentAsync(
            dbContext,
            compound.Id,
            expiringResident.UserId,
            DocumentCategory.ResidentIdentity,
            DocumentApprovalStatus.Approved,
            DateTime.UtcNow.AddDays(10));

        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.GetComplianceReportAsync(compound.Id);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.ExpiredDocumentCount.Should().Be(1);
        result.Value.ExpiringSoonDocumentCount.Should().Be(1);
        var expired = result.Value.Residents.Single(item => item.ResidentProfileId == expiredResident.Id);
        expired.IsCompliant.Should().BeFalse();
        expired.Gaps.Single().Reason.Should().Be("Expired");
        result.Value.Residents.Single(item => item.ResidentProfileId == expiringResident.Id).IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task GetComplianceReportAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "DOC8F-A");
        var blocked = await AddCompoundAsync(dbContext, "DOC8F-B");
        var service = CreateService(dbContext, [allowed.Id]);

        var result = await service.GetComplianceReportAsync(blocked.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static DocumentManagementService CreateService(ApplicationDbContext dbContext, Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new DocumentManagementService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string name)
    {
        var compound = new Compound
        {
            Name = name,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Documents"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = Guid.NewGuid(),
            FullName = fullName,
            IsActive = true
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task AddMandatoryRequirementAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        DocumentCategory category,
        bool requiresApproval)
    {
        dbContext.DocumentRequirements.Add(new DocumentRequirement
        {
            CompoundId = compoundId,
            Category = category,
            AppliesTo = DocumentRequirementAppliesTo.Resident,
            Title = $"Mandatory {category}",
            IsMandatory = true,
            RequiresApproval = requiresApproval,
            IsActive = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task AddDocumentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid ownerUserId,
        DocumentCategory category,
        DocumentApprovalStatus approvalStatus,
        DateTime? expiresAtUtc)
    {
        dbContext.DocumentFiles.Add(new DocumentFile
        {
            CompoundId = compoundId,
            OwnerUserId = ownerUserId,
            OriginalFileName = $"document-{Guid.NewGuid():N}.pdf",
            StoredFileName = $"document-{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            Extension = "pdf",
            StoragePath = "App_Data/Uploads/Documents/document.pdf",
            Category = category,
            Visibility = DocumentVisibility.Private,
            ApprovalStatus = approvalStatus,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }
}
