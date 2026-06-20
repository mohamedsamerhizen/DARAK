using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class DocumentManagementProTests
{
    [Fact]
    public async Task CreateRequirementAsync_CreatesRequirementAndAuditEntry()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC19-REQ");
        var service = CreateService(dbContext, [compound.Id]);
        var userId = Guid.NewGuid();

        var result = await service.CreateRequirementAsync(userId, new CreateDocumentRequirementRequest
        {
            CompoundId = compound.Id,
            Category = DocumentCategory.ResidentIdentity,
            AppliesTo = DocumentRequirementAppliesTo.Resident,
            Title = "Resident identity",
            Description = "A valid identity document is required.",
            IsMandatory = true,
            ValidityDays = 365,
            RequiresApproval = true
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.IsMandatory.Should().BeTrue();
        dbContext.DocumentRequirements.Should().ContainSingle(item => item.Id == result.Value.Id);
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.DocumentRequirementCreated);
    }

    [Fact]
    public async Task CreateRequirementAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "DOC19-A");
        var blocked = await AddCompoundAsync(dbContext, "DOC19-B");
        var service = CreateService(dbContext, [allowed.Id]);

        var result = await service.CreateRequirementAsync(Guid.NewGuid(), new CreateDocumentRequirementRequest
        {
            CompoundId = blocked.Id,
            Category = DocumentCategory.LeaseContract,
            AppliesTo = DocumentRequirementAppliesTo.Tenant,
            Title = "Blocked requirement"
        });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        dbContext.DocumentRequirements.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveDocumentAsync_ApprovesPendingDocumentAndWritesAudit()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC19-APP");
        var document = await AddDocumentAsync(dbContext, compound.Id, DocumentApprovalStatus.PendingReview);
        var service = CreateService(dbContext, [compound.Id]);
        var reviewerId = Guid.NewGuid();

        var result = await service.ApproveDocumentAsync(reviewerId, document.Id, new ReviewDocumentRequest { Reason = "Valid document." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ApprovalStatus.Should().Be(DocumentApprovalStatus.Approved);
        result.Value.ReviewedByUserId.Should().Be(reviewerId);
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.DocumentApproved);
    }

    [Fact]
    public async Task RejectDocumentAsync_RequiresReason()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC19-REJ");
        var document = await AddDocumentAsync(dbContext, compound.Id, DocumentApprovalStatus.PendingReview);
        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.RejectDocumentAsync(Guid.NewGuid(), document.Id, new ReviewDocumentRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsPendingExpiredExpiringAndRequirements()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC19-DASH");
        await AddDocumentAsync(dbContext, compound.Id, DocumentApprovalStatus.PendingReview, expiresAtUtc: DateTime.UtcNow.AddDays(10));
        await AddDocumentAsync(dbContext, compound.Id, DocumentApprovalStatus.Approved, expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        dbContext.DocumentRequirements.Add(new DocumentRequirement
        {
            CompoundId = compound.Id,
            Category = DocumentCategory.LeaseContract,
            AppliesTo = DocumentRequirementAppliesTo.Tenant,
            Title = "Lease",
            IsMandatory = true,
            RequiresApproval = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.GetDashboardAsync(compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.PendingReviewCount.Should().Be(1);
        result.Value.ExpiredCount.Should().Be(1);
        result.Value.ExpiringSoonCount.Should().Be(1);
        result.Value.ActiveRequirementCount.Should().Be(1);
        result.Value.MandatoryRequirementCount.Should().Be(1);
    }

    [Fact]
    public async Task GetResidentChecklistAsync_MarksMandatoryRequirementSatisfiedOnlyWhenApprovedAndNotExpired()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "DOC19-CHK");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Checklist Resident");
        dbContext.DocumentRequirements.Add(new DocumentRequirement
        {
            CompoundId = compound.Id,
            Category = DocumentCategory.ResidentIdentity,
            AppliesTo = DocumentRequirementAppliesTo.Resident,
            Title = "Resident identity",
            IsMandatory = true,
            RequiresApproval = true,
            IsActive = true
        });
        dbContext.DocumentFiles.Add(new DocumentFile
        {
            CompoundId = compound.Id,
            OwnerUserId = resident.UserId,
            OriginalFileName = "id.pdf",
            StoredFileName = "id.pdf",
            ContentType = "application/pdf",
            Extension = "pdf",
            StoragePath = "App_Data/Uploads/Documents/id.pdf",
            Category = DocumentCategory.ResidentIdentity,
            Visibility = DocumentVisibility.Private,
            ApprovalStatus = DocumentApprovalStatus.Approved,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(180)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.GetResidentChecklistAsync(resident.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Single().IsSatisfied.Should().BeTrue();
    }

    private static DocumentManagementService CreateService(ApplicationDbContext dbContext, Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new DocumentManagementService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string codePrefix)
    {
        var compound = new Compound
        {
            Name = codePrefix,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Commercial"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
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

    private static async Task<DocumentFile> AddDocumentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        DocumentApprovalStatus approvalStatus,
        DateTime? expiresAtUtc = null)
    {
        var document = new DocumentFile
        {
            CompoundId = compoundId,
            OriginalFileName = $"document-{Guid.NewGuid():N}.pdf",
            StoredFileName = $"document-{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            Extension = "pdf",
            StoragePath = "App_Data/Uploads/Documents/document.pdf",
            Category = DocumentCategory.Administrative,
            Visibility = DocumentVisibility.AdminOnly,
            ApprovalStatus = approvalStatus,
            ExpiresAtUtc = expiresAtUtc
        };

        dbContext.DocumentFiles.Add(document);
        await dbContext.SaveChangesAsync();
        return document;
    }
}
