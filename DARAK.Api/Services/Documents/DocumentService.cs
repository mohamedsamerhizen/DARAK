using System.Globalization;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class DocumentService(
    ApplicationDbContext dbContext,
    IWebHostEnvironment webHostEnvironment,
    ICompoundAccessService? compoundAccessService = null)
    : IDocumentService
{
    public const long MaxFileSizeInBytes = 10 * 1024 * 1024;

    private const int MaxOriginalFileNameLength = 255;
    private const int MaxStoredFileNameLength = 255;
    private const int MaxContentTypeLength = 200;
    private const int MaxExtensionLength = 20;
    private const int MaxStoragePathLength = 500;
    private const int MaxRelatedEntityTypeLength = 100;
    private const int MaxDescriptionLength = 1000;
    private const int MaxIpAddressLength = 128;
    private const int MaxUserAgentLength = 500;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf",
        "jpg",
        "jpeg",
        "png",
        "webp",
        "doc",
        "docx",
        "xls",
        "xlsx"
    };

    private static readonly Dictionary<string, string[]> AllowedContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = ["application/pdf"],
        ["jpg"] = ["image/jpeg"],
        ["jpeg"] = ["image/jpeg"],
        ["png"] = ["image/png"],
        ["webp"] = ["image/webp"],
        ["doc"] = ["application/msword"],
        ["docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        ["xls"] = ["application/vnd.ms-excel"],
        ["xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]
    };

    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "exe",
        "bat",
        "cmd",
        "ps1",
        "js",
        "dll",
        "sh",
        "html",
        "htm"
    };

    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46, 0x2D];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] WebpRiffSignature = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpFormatSignature = [0x57, 0x45, 0x42, 0x50];

    public async Task<ServiceResult<DocumentFileResponse>> UploadDocumentAsync(
        Guid? currentUserId,
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<DocumentFileResponse>.BadRequest("Current user is invalid.");
        }

        var validation = await ValidateUploadRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return ToResult<DocumentFileResponse>(validation);
        }

        var compoundResult = await ResolveDocumentCompoundIdAsync(
            request.CompoundId,
            request.PropertyUnitId,
            cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<DocumentFileResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Document compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<DocumentFileResponse>.Forbidden("Current user cannot access this compound.");
        }

        var ownerScopeValidation = await ValidateOwnerUserScopeAsync(
            request.OwnerUserId,
            compoundResult.Value,
            cancellationToken);
        if (ownerScopeValidation is not null)
        {
            return ToResult<DocumentFileResponse>(ownerScopeValidation);
        }

        var file = request.File!;
        var originalFileName = NormalizeFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}.{extension}";
        var now = DateTime.UtcNow;
        var storageDirectory = GetStorageDirectory(now);

        Directory.CreateDirectory(storageDirectory);

        var physicalPath = Path.Combine(storageDirectory, storedFileName);
        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var document = new DocumentFile
        {
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            ContentType = NormalizeContentType(file.ContentType),
            Extension = extension,
            SizeInBytes = file.Length,
            StoragePath = ToRelativeStoragePath(physicalPath),
            Category = request.Category!.Value,
            Visibility = request.Visibility!.Value,
            RelatedEntityType = TrimOrNull(request.RelatedEntityType),
            RelatedEntityId = request.RelatedEntityId,
            UploadedByUserId = currentUserId.Value,
            OwnerUserId = request.OwnerUserId,
            CompoundId = compoundResult.Value,
            PropertyUnitId = request.PropertyUnitId,
            Description = TrimOrNull(request.Description),
            ApprovalStatus = request.RequiresReview ? DocumentApprovalStatus.PendingReview : DocumentApprovalStatus.NotRequired,
            ExpiresAtUtc = request.ExpiresAtUtc,
            VersionNumber = 1,
            CreatedAtUtc = now
        };

        document.AccessLogs.Add(new DocumentAccessLog
        {
            UserId = currentUserId.Value,
            Action = DocumentAccessAction.Uploaded,
            CreatedAtUtc = now
        });

        dbContext.DocumentFiles.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentFileResponse>.Success(ToDocumentFileResponse(document));
    }

    public async Task<ServiceResult<DocumentFileResponse>> UpdateMetadataAsync(
        Guid id,
        UpdateDocumentMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateMetadataRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return ToResult<DocumentFileResponse>(validation);
        }

        var document = await dbContext.DocumentFiles
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (document is null)
        {
            return ServiceResult<DocumentFileResponse>.NotFound("Document was not found.");
        }

        if (!await CanAccessCompoundAsync(document.CompoundId, cancellationToken))
        {
            return ServiceResult<DocumentFileResponse>.NotFound("Document was not found.");
        }

        var compoundResult = await ResolveDocumentCompoundIdAsync(
            request.CompoundId,
            request.PropertyUnitId,
            cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<DocumentFileResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Document compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<DocumentFileResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (compoundResult.Value != document.CompoundId)
        {
            return ServiceResult<DocumentFileResponse>.BadRequest("Document compound cannot be changed.");
        }

        var ownerScopeValidation = await ValidateOwnerUserScopeAsync(
            request.OwnerUserId,
            compoundResult.Value,
            cancellationToken);
        if (ownerScopeValidation is not null)
        {
            return ToResult<DocumentFileResponse>(ownerScopeValidation);
        }

        document.Category = request.Category!.Value;
        document.Visibility = request.Visibility!.Value;
        document.RelatedEntityType = TrimOrNull(request.RelatedEntityType);
        document.RelatedEntityId = request.RelatedEntityId;
        document.OwnerUserId = request.OwnerUserId;
        document.PropertyUnitId = request.PropertyUnitId;
        document.Description = TrimOrNull(request.Description);
        document.ExpiresAtUtc = request.ExpiresAtUtc;
        document.ApprovalStatus = request.RequiresReview ? DocumentApprovalStatus.PendingReview : DocumentApprovalStatus.NotRequired;
        document.ReviewedByUserId = null;
        document.ReviewedAtUtc = null;
        document.ReviewReason = null;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentFileResponse>.Success(ToDocumentFileResponse(document));
    }

    public async Task<ServiceResult<DocumentFileResponse>> GetDocumentAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<DocumentFileResponse>.BadRequest("Current user is invalid.");
        }

        var document = await dbContext.DocumentFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (document is null
            || !CanAccessDocument(document, currentUserId.Value, isManager)
            || (isManager && !await CanAccessCompoundAsync(document.CompoundId, cancellationToken))
            || (!isManager && !await CanResidentAccessDocumentCompoundAsync(document, currentUserId.Value, cancellationToken)))
        {
            return ServiceResult<DocumentFileResponse>.NotFound("Document was not found.");
        }

        await AddAccessLogAsync(
            document.Id,
            currentUserId.Value,
            DocumentAccessAction.Viewed,
            ipAddress,
            userAgent,
            cancellationToken);

        return ServiceResult<DocumentFileResponse>.Success(ToDocumentFileResponse(document));
    }

    public async Task<ServiceResult<PagedResult<DocumentFileResponse>>> SearchDocumentsAsync(
        DocumentQueryRequest query,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<DocumentFileResponse>>.BadRequest("Current user is invalid.");
        }

        var validation = ValidateQueryRequest(query);
        if (validation is not null)
        {
            return ToResult<PagedResult<DocumentFileResponse>>(validation);
        }

        var documents = ApplyFilters(
            dbContext.DocumentFiles
                .AsNoTracking()
                .Where(document => !document.IsDeleted),
            query);

        if (isManager)
        {
            documents = await ApplyCurrentCompoundAccessAsync(documents, cancellationToken);
        }
        else
        {
            var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId.Value, cancellationToken);
            documents = ApplyResidentVisibilityScope(documents, currentUserId.Value, residentCompoundIds);
        }

        var totalCount = await documents.CountAsync(cancellationToken);
        var items = await documents
            .OrderByDescending(document => document.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(document => new DocumentFileResponse(
                document.Id,
                document.OriginalFileName,
                document.ContentType,
                document.Extension,
                document.SizeInBytes,
                document.Category,
                document.Visibility,
                document.RelatedEntityType,
                document.RelatedEntityId,
                document.UploadedByUserId,
                document.OwnerUserId,
                document.CompoundId,
                document.PropertyUnitId,
                document.Description,
                document.ApprovalStatus,
                document.ReviewedByUserId,
                document.ReviewedAtUtc,
                document.ReviewReason,
                document.ExpiresAtUtc,
                document.VersionNumber,
                document.RootDocumentFileId,
                document.PreviousVersionDocumentFileId,
                document.CreatedAtUtc,
                document.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<DocumentFileResponse>>.Success(
            new PagedResult<DocumentFileResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<DocumentDownloadResponse>> DownloadDocumentAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<DocumentDownloadResponse>.BadRequest("Current user is invalid.");
        }

        var document = await dbContext.DocumentFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (document is null
            || !CanAccessDocument(document, currentUserId.Value, isManager)
            || (isManager && !await CanAccessCompoundAsync(document.CompoundId, cancellationToken))
            || (!isManager && !await CanResidentAccessDocumentCompoundAsync(document, currentUserId.Value, cancellationToken)))
        {
            return ServiceResult<DocumentDownloadResponse>.NotFound("Document was not found.");
        }

        var physicalPath = ResolveStoragePath(document.StoragePath);
        if (physicalPath is null || !File.Exists(physicalPath))
        {
            return ServiceResult<DocumentDownloadResponse>.NotFound("Document file content was not found.");
        }

        await AddAccessLogAsync(
            document.Id,
            currentUserId.Value,
            DocumentAccessAction.Downloaded,
            ipAddress,
            userAgent,
            cancellationToken);

        return ServiceResult<DocumentDownloadResponse>.Success(
            new DocumentDownloadResponse(
                document.Id,
                physicalPath,
                document.OriginalFileName,
                document.ContentType));
    }

    public async Task<ServiceResult<object?>> SoftDeleteDocumentAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<object?>.BadRequest("Current user is invalid.");
        }

        var document = await dbContext.DocumentFiles
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (document is null || !await CanAccessCompoundAsync(document.CompoundId, cancellationToken))
        {
            return ServiceResult<object?>.NotFound("Document was not found.");
        }

        var now = DateTime.UtcNow;
        document.IsDeleted = true;
        document.DeletedAtUtc = now;
        document.UpdatedAtUtc = now;
        document.AccessLogs.Add(new DocumentAccessLog
        {
            UserId = currentUserId.Value,
            Action = DocumentAccessAction.Deleted,
            CreatedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<ServiceResult<PagedResult<DocumentAccessLogResponse>>> GetAccessLogsAsync(
        Guid documentFileId,
        DocumentAccessLogQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.DocumentFiles
            .AsNoTracking()
            .Where(item => item.Id == documentFileId)
            .Select(item => new { item.Id, item.CompoundId })
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null || !await CanAccessCompoundAsync(document.CompoundId, cancellationToken))
        {
            return ServiceResult<PagedResult<DocumentAccessLogResponse>>.NotFound("Document was not found.");
        }

        var logs = dbContext.DocumentAccessLogs
            .AsNoTracking()
            .Include(log => log.User)
            .Where(log => log.DocumentFileId == documentFileId)
            .OrderByDescending(log => log.CreatedAtUtc);

        var totalCount = await logs.CountAsync(cancellationToken);
        var items = await logs
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(log => new DocumentAccessLogResponse(
                log.Id,
                log.DocumentFileId,
                log.UserId,
                log.User.FullName,
                log.Action,
                log.CreatedAtUtc,
                log.IpAddress,
                log.UserAgent))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<DocumentAccessLogResponse>>.Success(
            new PagedResult<DocumentAccessLogResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    private async Task<ValidationFailure?> ValidateUploadRequestAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File is required.");
        }

        if (request.File.Length == 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File cannot be empty.");
        }

        if (request.File.Length > MaxFileSizeInBytes)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File size cannot exceed 10 MB.");
        }

        var fileName = NormalizeFileName(request.File.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File name is required.");
        }

        if (fileName.Length > MaxOriginalFileNameLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File name is too long.");
        }

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File extension is required.");
        }

        if (extension.Length > MaxExtensionLength
            || DangerousExtensions.Contains(extension)
            || !AllowedExtensions.Contains(extension))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File type is not allowed.");
        }

        var contentType = NormalizeContentType(request.File.ContentType);
        if (contentType.Length > MaxContentTypeLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "File content type is too long.");
        }

        if (!AllowedContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes)
            || !allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "File content type does not match the allowed file extension.");
        }

        if (!await FileMatchesExpectedSignatureAsync(request.File, extension, cancellationToken))
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "File binary signature does not match the allowed file extension.");
        }

        return await ValidateMetadataAsync(
            request.Category,
            request.Visibility,
            request.RelatedEntityType,
            request.OwnerUserId,
            request.PropertyUnitId,
            request.Description,
            request.ExpiresAtUtc,
            cancellationToken);
    }

    private async Task<ValidationFailure?> ValidateMetadataRequestAsync(
        UpdateDocumentMetadataRequest request,
        CancellationToken cancellationToken)
    {
        return await ValidateMetadataAsync(
            request.Category,
            request.Visibility,
            request.RelatedEntityType,
            request.OwnerUserId,
            request.PropertyUnitId,
            request.Description,
            request.ExpiresAtUtc,
            cancellationToken);
    }

    private async Task<ValidationFailure?> ValidateMetadataAsync(
        DocumentCategory? category,
        DocumentVisibility? visibility,
        string? relatedEntityType,
        Guid? ownerUserId,
        Guid? propertyUnitId,
        string? description,
        DateTime? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (!category.HasValue || !Enum.IsDefined(category.Value))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document category is invalid.");
        }

        if (!visibility.HasValue || !Enum.IsDefined(visibility.Value))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document visibility is invalid.");
        }

        if (TrimOrNull(relatedEntityType)?.Length > MaxRelatedEntityTypeLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Related entity type is too long.");
        }

        if (TrimOrNull(description)?.Length > MaxDescriptionLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Description is too long.");
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= DateTime.UtcNow.AddMinutes(-5))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document expiry must be in the future.");
        }

        if (ownerUserId.HasValue)
        {
            var userExists = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == ownerUserId.Value, cancellationToken);
            if (!userExists)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Owner user was not found.");
            }
        }

        if (propertyUnitId.HasValue)
        {
            var propertyUnitExists = await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(unit => unit.Id == propertyUnitId.Value, cancellationToken);
            if (!propertyUnitExists)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Property unit was not found.");
            }
        }

        return null;
    }

    private async Task<ValidationFailure?> ValidateOwnerUserScopeAsync(
        Guid? ownerUserId,
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        if (!ownerUserId.HasValue)
        {
            return null;
        }

        var ownerBelongsToCompound = await dbContext.ResidentProfiles
            .AsNoTracking()
            .AnyAsync(profile =>
                profile.UserId == ownerUserId.Value
                && profile.CompoundId == compoundId
                && profile.IsActive,
                cancellationToken);

        return ownerBelongsToCompound
            ? null
            : new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Owner user must be an active resident in the document compound.");
    }

    private static async Task<bool> FileMatchesExpectedSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var requiredLength = extension.ToLowerInvariant() switch
        {
            "pdf" => PdfSignature.Length,
            "jpg" or "jpeg" => JpegSignature.Length,
            "png" => PngSignature.Length,
            "webp" => 12,
            _ => 0
        };

        if (requiredLength == 0)
        {
            return true;
        }

        await using var stream = file.OpenReadStream();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var buffer = new byte[requiredLength];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, requiredLength), cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (bytesRead < requiredLength)
        {
            return false;
        }

        return extension.ToLowerInvariant() switch
        {
            "pdf" => buffer.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature),
            "jpg" or "jpeg" => buffer.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature),
            "png" => buffer.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature),
            "webp" => buffer.AsSpan(0, WebpRiffSignature.Length).SequenceEqual(WebpRiffSignature)
                && buffer.AsSpan(8, WebpFormatSignature.Length).SequenceEqual(WebpFormatSignature),
            _ => true
        };
    }

    private static ValidationFailure? ValidateQueryRequest(DocumentQueryRequest query)
    {
        if (query.Category.HasValue && !Enum.IsDefined(query.Category.Value))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document category is invalid.");
        }

        if (query.Visibility.HasValue && !Enum.IsDefined(query.Visibility.Value))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document visibility is invalid.");
        }

        if (TrimOrNull(query.RelatedEntityType)?.Length > MaxRelatedEntityTypeLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Related entity type is too long.");
        }

        if (TrimOrNull(query.SearchTerm)?.Length > MaxOriginalFileNameLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Search term is too long.");
        }

        return null;
    }

    private async Task<IQueryable<DocumentFile>> ApplyCurrentCompoundAccessAsync(
        IQueryable<DocumentFile> documents,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return documents;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return documents.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return documents;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return documents.Where(_ => false);
        }

        return documents.Where(document => scope.AllowedCompoundIds.Contains(document.CompoundId));
    }

    private async Task<bool> CanAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<ServiceResult<Guid>> ResolveDocumentCompoundIdAsync(
        Guid? requestedCompoundId,
        Guid? propertyUnitId,
        CancellationToken cancellationToken)
    {
        if (propertyUnitId.HasValue)
        {
            var unitCompoundId = await dbContext.PropertyUnits
                .AsNoTracking()
                .Where(unit => unit.Id == propertyUnitId.Value)
                .Select(unit => (Guid?)unit.CompoundId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!unitCompoundId.HasValue)
            {
                return ServiceResult<Guid>.NotFound("Property unit was not found.");
            }

            if (requestedCompoundId.HasValue && requestedCompoundId.Value != unitCompoundId.Value)
            {
                return ServiceResult<Guid>.BadRequest("Document compound must match the selected property unit compound.");
            }

            return ServiceResult<Guid>.Success(unitCompoundId.Value);
        }

        if (!requestedCompoundId.HasValue || requestedCompoundId.Value == Guid.Empty)
        {
            return ServiceResult<Guid>.BadRequest("Compound id is required for documents.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == requestedCompoundId.Value && compound.IsActive, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<Guid>.NotFound("Compound was not found.");
        }

        return ServiceResult<Guid>.Success(requestedCompoundId.Value);
    }

    private async Task<Guid[]> GetResidentCompoundIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record =>
                record.OccupancyStatus == OccupancyStatus.Active
                && record.ResidentProfile.UserId == currentUserId
                && record.ResidentProfile.IsActive)
            .Select(record => record.PropertyUnit.CompoundId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task<bool> CanResidentAccessDocumentCompoundAsync(
        DocumentFile document,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (document.OwnerUserId == currentUserId)
        {
            return true;
        }

        var residentCompoundIds = await GetResidentCompoundIdsAsync(currentUserId, cancellationToken);
        return residentCompoundIds.Contains(document.CompoundId);
    }

    private async Task AddAccessLogAsync(
        Guid documentFileId,
        Guid userId,
        DocumentAccessAction action,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        dbContext.DocumentAccessLogs.Add(new DocumentAccessLog
        {
            DocumentFileId = documentFileId,
            UserId = userId,
            Action = action,
            IpAddress = Truncate(TrimOrNull(ipAddress), MaxIpAddressLength),
            UserAgent = Truncate(TrimOrNull(userAgent), MaxUserAgentLength),
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string GetStorageDirectory(DateTime timestamp)
    {
        return Path.Combine(
            GetStorageRootPath(),
            timestamp.Year.ToString(CultureInfo.InvariantCulture),
            timestamp.Month.ToString("00", CultureInfo.InvariantCulture));
    }

    private string GetStorageRootPath()
    {
        return Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", "Uploads", "Documents");
    }

    private string ToRelativeStoragePath(string physicalPath)
    {
        return Path.GetRelativePath(webHostEnvironment.ContentRootPath, physicalPath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private string? ResolveStoragePath(string storagePath)
    {
        var contentRoot = Path.GetFullPath(webHostEnvironment.ContentRootPath);
        var storageRoot = EnsureTrailingSeparator(Path.GetFullPath(GetStorageRootPath()));
        var physicalPath = Path.GetFullPath(Path.Combine(
            contentRoot,
            storagePath.Replace('/', Path.DirectorySeparatorChar)));

        return physicalPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase)
            ? physicalPath
            : null;
    }

    private static IQueryable<DocumentFile> ApplyFilters(
        IQueryable<DocumentFile> documents,
        DocumentQueryRequest query)
    {
        if (query.Category.HasValue)
        {
            documents = documents.Where(document => document.Category == query.Category.Value);
        }

        if (query.Visibility.HasValue)
        {
            documents = documents.Where(document => document.Visibility == query.Visibility.Value);
        }

        if (query.OwnerUserId.HasValue)
        {
            documents = documents.Where(document => document.OwnerUserId == query.OwnerUserId.Value);
        }

        if (query.UploadedByUserId.HasValue)
        {
            documents = documents.Where(document => document.UploadedByUserId == query.UploadedByUserId.Value);
        }

        if (query.CompoundId.HasValue)
        {
            documents = documents.Where(document => document.CompoundId == query.CompoundId.Value);
        }

        var relatedEntityType = TrimOrNull(query.RelatedEntityType);
        if (relatedEntityType is not null)
        {
            documents = documents.Where(document => document.RelatedEntityType == relatedEntityType);
        }

        if (query.RelatedEntityId.HasValue)
        {
            documents = documents.Where(document => document.RelatedEntityId == query.RelatedEntityId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            documents = documents.Where(document => document.PropertyUnitId == query.PropertyUnitId.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            documents = documents.Where(document =>
                document.OriginalFileName.Contains(searchTerm)
                || (document.Description != null && document.Description.Contains(searchTerm)));
        }

        return documents;
    }

    private static IQueryable<DocumentFile> ApplyResidentVisibilityScope(
        IQueryable<DocumentFile> documents,
        Guid currentUserId,
        Guid[] residentCompoundIds)
    {
        return documents.Where(document =>
            (document.Visibility == DocumentVisibility.PublicToResidents
                && residentCompoundIds.Contains(document.CompoundId))
            || (document.OwnerUserId == currentUserId
                && (document.Visibility == DocumentVisibility.Private
                    || document.Visibility == DocumentVisibility.ResidentAndAdmin)));
    }

    private static bool CanAccessDocument(
        DocumentFile document,
        Guid currentUserId,
        bool isManager)
    {
        if (isManager)
        {
            return true;
        }

        return document.Visibility switch
        {
            DocumentVisibility.PublicToResidents => true,
            DocumentVisibility.Private => document.OwnerUserId == currentUserId,
            DocumentVisibility.ResidentAndAdmin => document.OwnerUserId == currentUserId,
            _ => false
        };
    }

    private static DocumentFileResponse ToDocumentFileResponse(DocumentFile document)
    {
        return new DocumentFileResponse(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.Extension,
            document.SizeInBytes,
            document.Category,
            document.Visibility,
            document.RelatedEntityType,
            document.RelatedEntityId,
            document.UploadedByUserId,
            document.OwnerUserId,
            document.CompoundId,
            document.PropertyUnitId,
            document.Description,
            document.ApprovalStatus,
            document.ReviewedByUserId,
            document.ReviewedAtUtc,
            document.ReviewReason,
            document.ExpiresAtUtc,
            document.VersionNumber,
            document.RootDocumentFileId,
            document.PreviousVersionDocumentFileId,
            document.CreatedAtUtc,
            document.UpdatedAtUtc);
    }

    private static string NormalizeFileName(string fileName)
    {
        return Path.GetFileName(fileName).Trim();
    }

    private static string NormalizeContentType(string? contentType)
    {
        return TrimOrNull(contentType) ?? "application/octet-stream";
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
