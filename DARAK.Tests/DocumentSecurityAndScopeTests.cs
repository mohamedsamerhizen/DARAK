using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace DARAK.Tests;

public sealed class DocumentSecurityAndScopeTests
{
    [Fact]
    public async Task UploadDocumentAsync_StripsPathSegmentsFromOriginalFileName()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var compound = CreateCompound("Documents");
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        var service = new DocumentService(dbContext, environment);

        var result = await service.UploadDocumentAsync(
            Guid.NewGuid(),
            new UploadDocumentRequest
            {
                File = CreateFile("../secret.pdf", "application/pdf"),
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = compound.Id
            });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.OriginalFileName.Should().Be("secret.pdf");
        result.Value.OriginalFileName.Should().NotContain("/");
        result.Value.OriginalFileName.Should().NotContain("\\");
    }

    [Fact]
    public async Task UploadDocumentAsync_RejectsPdfWithInvalidMagicBytes()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var compound = CreateCompound("Documents");
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        var service = new DocumentService(dbContext, environment);

        var result = await service.UploadDocumentAsync(
            Guid.NewGuid(),
            new UploadDocumentRequest
            {
                File = CreateFile("lease.pdf", "application/pdf", [1, 2, 3, 4]),
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = compound.Id
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task UploadDocumentAsync_RejectsOwnerUserOutsideDocumentCompound()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var allowedCompound = CreateCompound("Allowed");
        var blockedCompound = CreateCompound("Blocked");
        var ownerUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "owner@darak.test",
            Email = "owner@darak.test",
            FullName = "Owner User"
        };
        var blockedResident = new ResidentProfile
        {
            UserId = ownerUser.Id,
            CompoundId = blockedCompound.Id,
            FullName = "Blocked Resident"
        };

        dbContext.AddRange(allowedCompound, blockedCompound, ownerUser, blockedResident);
        await dbContext.SaveChangesAsync();
        var service = new DocumentService(dbContext, environment);

        var result = await service.UploadDocumentAsync(
            Guid.NewGuid(),
            new UploadDocumentRequest
            {
                File = CreateFile("lease.pdf", "application/pdf"),
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = allowedCompound.Id,
                OwnerUserId = ownerUser.Id
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task UpdateMetadataAsync_RejectsChangingDocumentCompound()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var originalCompound = CreateCompound("Original");
        var otherCompound = CreateCompound("Other");
        var document = CreateDocument(
            originalCompound.Id,
            DocumentVisibility.Private,
            ownerUserId: null,
            "lease.pdf");

        dbContext.AddRange(originalCompound, otherCompound, document);
        await dbContext.SaveChangesAsync();
        var service = new DocumentService(dbContext, environment);

        var result = await service.UpdateMetadataAsync(
            document.Id,
            new UpdateDocumentMetadataRequest
            {
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = otherCompound.Id
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        (await dbContext.DocumentFiles.FindAsync(document.Id))!.CompoundId.Should().Be(originalCompound.Id);
    }

    [Fact]
    public async Task DownloadDocumentAsync_RejectsStoragePathOutsideUploadRoot()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var userId = Guid.NewGuid();
        var compound = CreateCompound("Documents");
        var document = new DocumentFile
        {
            CompoundId = compound.Id,
            OriginalFileName = "lease.pdf",
            StoredFileName = "lease.pdf",
            ContentType = "application/pdf",
            Extension = "pdf",
            SizeInBytes = 128,
            StoragePath = "../outside.pdf",
            Category = DocumentCategory.LeaseContract,
            Visibility = DocumentVisibility.Private,
            UploadedByUserId = userId,
            OwnerUserId = userId
        };
        dbContext.AddRange(compound, document);
        await dbContext.SaveChangesAsync();
        var service = new DocumentService(dbContext, environment);

        var result = await service.DownloadDocumentAsync(
            document.Id,
            userId,
            isManager: false,
            ipAddress: "127.0.0.1",
            userAgent: "tests");

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task SearchDocumentsAsync_ResidentSeesOnlyPublicDocumentsInOwnActiveCompounds()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var userId = Guid.NewGuid();
        var seed = await SeedResidentDocumentScopeAsync(dbContext, userId);
        var service = new DocumentService(dbContext, environment);

        var result = await service.SearchDocumentsAsync(
            new DocumentQueryRequest(),
            userId,
            isManager: false);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().Id.Should().Be(seed.VisiblePublicDocumentId);
    }

    [Fact]
    public async Task GetDocumentAsync_ResidentCannotViewPrivateDocumentInSameCompoundUnlessOwner()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var userId = Guid.NewGuid();
        var seed = await SeedResidentDocumentScopeAsync(dbContext, userId);
        var service = new DocumentService(dbContext, environment);

        var result = await service.GetDocumentAsync(
            seed.PrivateSameCompoundDocumentId,
            userId,
            isManager: false,
            ipAddress: null,
            userAgent: null);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GetDocumentAsync_ManagerCannotViewDocumentOutsideAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var managerUserId = Guid.NewGuid();
        var seed = await SeedResidentDocumentScopeAsync(dbContext, Guid.NewGuid());
        var service = new DocumentService(
            dbContext,
            environment,
            new FakeCompoundAccessService(new[] { seed.AllowedCompoundId }));

        var result = await service.GetDocumentAsync(
            seed.BlockedPublicDocumentId,
            managerUserId,
            isManager: true,
            ipAddress: null,
            userAgent: null);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task SoftDeleteDocumentAsync_ManagerCannotDeleteDocumentOutsideAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var managerUserId = Guid.NewGuid();
        var seed = await SeedResidentDocumentScopeAsync(dbContext, Guid.NewGuid());
        var service = new DocumentService(
            dbContext,
            environment,
            new FakeCompoundAccessService(new[] { seed.AllowedCompoundId }));

        var result = await service.SoftDeleteDocumentAsync(
            seed.BlockedPublicDocumentId,
            managerUserId);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        dbContext.DocumentFiles.Single(document => document.Id == seed.BlockedPublicDocumentId).IsDeleted.Should().BeFalse();
        dbContext.DocumentAccessLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccessLogsAsync_ManagerCannotReadLogsForDocumentOutsideAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        using var environment = Pack2WebHostEnvironment.Create();
        var auditUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "audit@darak.test",
            Email = "audit@darak.test",
            FullName = "Audit User"
        };
        var seed = await SeedResidentDocumentScopeAsync(dbContext, Guid.NewGuid());
        dbContext.Users.Add(auditUser);
        dbContext.DocumentAccessLogs.Add(new DocumentAccessLog
        {
            DocumentFileId = seed.BlockedPublicDocumentId,
            UserId = auditUser.Id,
            Action = DocumentAccessAction.Viewed,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = new DocumentService(
            dbContext,
            environment,
            new FakeCompoundAccessService(new[] { seed.AllowedCompoundId }));

        var result = await service.GetAccessLogsAsync(
            seed.BlockedPublicDocumentId,
            new DocumentAccessLogQueryRequest());

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static async Task<DocumentScopeSeed> SeedResidentDocumentScopeAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        Guid userId)
    {
        var allowedCompound = CreateCompound("Allowed");
        var blockedCompound = CreateCompound("Blocked");
        var allowedUnit = CreateUnit(allowedCompound.Id, "A-101");
        var resident = new ResidentProfile
        {
            UserId = userId,
            CompoundId = allowedCompound.Id,
            FullName = "Resident"
        };
        var occupancy = new OccupancyRecord
        {
            CompoundId = allowedCompound.Id,
            PropertyUnitId = allowedUnit.Id,
            ResidentProfileId = resident.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };
        var visiblePublicDocument = CreateDocument(
            allowedCompound.Id,
            DocumentVisibility.PublicToResidents,
            ownerUserId: null,
            "visible.pdf");
        var blockedPublicDocument = CreateDocument(
            blockedCompound.Id,
            DocumentVisibility.PublicToResidents,
            ownerUserId: null,
            "blocked.pdf");
        var privateSameCompoundDocument = CreateDocument(
            allowedCompound.Id,
            DocumentVisibility.Private,
            ownerUserId: Guid.NewGuid(),
            "private.pdf");

        dbContext.AddRange(
            allowedCompound,
            blockedCompound,
            allowedUnit,
            resident,
            occupancy,
            visiblePublicDocument,
            blockedPublicDocument,
            privateSameCompoundDocument);
        await dbContext.SaveChangesAsync();

        return new DocumentScopeSeed(
            allowedCompound.Id,
            visiblePublicDocument.Id,
            blockedPublicDocument.Id,
            privateSameCompoundDocument.Id);
    }

    private static Compound CreateCompound(string name)
    {
        return new Compound
        {
            Name = name,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
    }

    private static PropertyUnit CreateUnit(Guid compoundId, string unitNumber)
    {
        return new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
    }

    private static DocumentFile CreateDocument(
        Guid compoundId,
        DocumentVisibility visibility,
        Guid? ownerUserId,
        string fileName)
    {
        return new DocumentFile
        {
            CompoundId = compoundId,
            OriginalFileName = fileName,
            StoredFileName = Guid.NewGuid().ToString("N") + ".pdf",
            ContentType = "application/pdf",
            Extension = "pdf",
            SizeInBytes = 100,
            StoragePath = "App_Data/Uploads/Documents/2026/06/" + Guid.NewGuid().ToString("N") + ".pdf",
            Category = DocumentCategory.LeaseContract,
            Visibility = visibility,
            OwnerUserId = ownerUserId,
            UploadedByUserId = ownerUserId
        };
    }

    private static IFormFile CreateFile(string fileName, string contentType)
    {
        return CreateFile(fileName, contentType, GetValidBytesFor(fileName));
    }

    private static IFormFile CreateFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] GetValidBytesFor(string fileName)
    {
        return Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31],
            "jpg" or "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0],
            "png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            "webp" => [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50],
            _ => [1, 2, 3, 4]
        };
    }

    private sealed class Pack2WebHostEnvironment : IWebHostEnvironment, IDisposable
    {
        private Pack2WebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "DARAK.Tests";

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }

        public static Pack2WebHostEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "DARAK.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            return new Pack2WebHostEnvironment(root);
        }

        public void Dispose()
        {
            (ContentRootFileProvider as IDisposable)?.Dispose();
            (WebRootFileProvider as IDisposable)?.Dispose();

            if (Directory.Exists(ContentRootPath))
            {
                Directory.Delete(ContentRootPath, recursive: true);
            }
        }
    }

    private sealed record DocumentScopeSeed(
        Guid AllowedCompoundId,
        Guid VisiblePublicDocumentId,
        Guid BlockedPublicDocumentId,
        Guid PrivateSameCompoundDocumentId);
}
