using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace DARAK.Tests;

public sealed class DocumentServiceTests
{
    [Theory]
    [InlineData("lease.pdf", "application/pdf", "pdf")]
    [InlineData("photo.jpg", "image/jpeg", "jpg")]
    [InlineData("photo.jpeg", "image/jpeg", "jpeg")]
    [InlineData("render.png", "image/png", "png")]
    [InlineData("render.webp", "image/webp", "webp")]
    [InlineData("legacy.doc", "application/msword", "doc")]
    [InlineData("lease.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx")]
    [InlineData("legacy.xls", "application/vnd.ms-excel", "xls")]
    [InlineData("sheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")]
    public async Task UploadDocumentAsync_AcceptsContentTypeMatchingExtension(
        string fileName,
        string contentType,
        string expectedExtension)
    {
        await using var dbContext = TestDb.Create();
        using var environment = TestWebHostEnvironment.Create();

        var compound = new Compound
        {
            Name = "Test Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Test"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();

        var service = new DocumentService(dbContext, environment);

        var result = await service.UploadDocumentAsync(
            Guid.NewGuid(),
            new UploadDocumentRequest
            {
                File = CreateFile(fileName, contentType),
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = compound.Id
            });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Extension.Should().Be(expectedExtension);
        result.Value.ContentType.Should().Be(contentType);
    }

    [Theory]
    [InlineData("lease.pdf", "image/png")]
    [InlineData("photo.png", "application/pdf")]
    [InlineData("photo.jpg", "image/png")]
    [InlineData("legacy.doc", "application/pdf")]
    [InlineData("legacy.xls", "application/pdf")]
    [InlineData("sheet.xlsx", "application/pdf")]
    public async Task UploadDocumentAsync_RejectsContentTypeMismatchingExtension(
        string fileName,
        string contentType)
    {
        await using var dbContext = TestDb.Create();
        using var environment = TestWebHostEnvironment.Create();

        var compound = new Compound
        {
            Name = "Test Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Test"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();

        var service = new DocumentService(dbContext, environment);

        var result = await service.UploadDocumentAsync(
            Guid.NewGuid(),
            new UploadDocumentRequest
            {
                File = CreateFile(fileName, contentType),
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = compound.Id
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task UploadDocumentAsync_RejectsMissingContentTypeForAllowedExtension()
    {
        await using var dbContext = TestDb.Create();
        using var environment = TestWebHostEnvironment.Create();

        var compound = new Compound
        {
            Name = "Test Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Test"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();

        var service = new DocumentService(dbContext, environment);

        var result = await service.UploadDocumentAsync(
            Guid.NewGuid(),
            new UploadDocumentRequest
            {
                File = CreateFile("lease.pdf", string.Empty),
                Category = DocumentCategory.LeaseContract,
                Visibility = DocumentVisibility.Private,
                CompoundId = compound.Id
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    private static IFormFile CreateFile(string fileName, string contentType)
    {
        byte[] bytes = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37],
            "jpg" or "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10],
            "png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            "webp" => [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50],
            _ => [1, 2, 3, 4]
        };
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment, IDisposable
    {
        private TestWebHostEnvironment(string contentRootPath)
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

        public static TestWebHostEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "DARAK.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            return new TestWebHostEnvironment(root);
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
}
