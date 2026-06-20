using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using Microsoft.AspNetCore.Http;

namespace DARAK.Api.DTOs.Documents;

public sealed record DocumentFileResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    string Extension,
    long SizeInBytes,
    DocumentCategory Category,
    DocumentVisibility Visibility,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    Guid? UploadedByUserId,
    Guid? OwnerUserId,
    Guid CompoundId,
    Guid? PropertyUnitId,
    string? Description,
    DocumentApprovalStatus ApprovalStatus,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewReason,
    DateTime? ExpiresAtUtc,
    int VersionNumber,
    Guid? RootDocumentFileId,
    Guid? PreviousVersionDocumentFileId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record DocumentAccessLogResponse(
    Guid Id,
    Guid DocumentFileId,
    Guid UserId,
    string? UserName,
    DocumentAccessAction Action,
    DateTime CreatedAtUtc,
    string? IpAddress,
    string? UserAgent);

public sealed record DocumentDownloadResponse(
    Guid Id,
    string PhysicalPath,
    string OriginalFileName,
    string ContentType);

public sealed class DocumentQueryRequest : PaginationQuery
{
    public DocumentCategory? Category { get; init; }

    public DocumentVisibility? Visibility { get; init; }

    public Guid? OwnerUserId { get; init; }

    public Guid? UploadedByUserId { get; init; }

    public Guid? CompoundId { get; init; }

    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class DocumentAccessLogQueryRequest : PaginationQuery
{
}

public sealed class UploadDocumentRequest
{
    public IFormFile? File { get; init; }

    public DocumentCategory? Category { get; init; }

    public DocumentVisibility? Visibility { get; init; }

    [MaxLength(100)]
    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    public Guid? OwnerUserId { get; init; }

    public Guid? CompoundId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public bool RequiresReview { get; init; }
}

public sealed class UpdateDocumentMetadataRequest
{
    public DocumentCategory? Category { get; init; }

    public DocumentVisibility? Visibility { get; init; }

    [MaxLength(100)]
    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    public Guid? OwnerUserId { get; init; }

    public Guid? CompoundId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public bool RequiresReview { get; init; }
}
