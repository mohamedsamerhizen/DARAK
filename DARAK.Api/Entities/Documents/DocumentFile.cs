using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class DocumentFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public long SizeInBytes { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public DocumentCategory Category { get; set; }

    public DocumentVisibility Visibility { get; set; } = DocumentVisibility.Private;

    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public Guid? OwnerUserId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public string? Description { get; set; }

    public DocumentApprovalStatus ApprovalStatus { get; set; } = DocumentApprovalStatus.NotRequired;

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewReason { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public int VersionNumber { get; set; } = 1;

    public Guid? RootDocumentFileId { get; set; }

    public Guid? PreviousVersionDocumentFileId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ApplicationUser? UploadedByUser { get; set; }

    public ApplicationUser? OwnerUser { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit? PropertyUnit { get; set; }

    public DocumentFile? RootDocumentFile { get; set; }

    public DocumentFile? PreviousVersionDocumentFile { get; set; }

    public ICollection<DocumentFile> Versions { get; set; } = new List<DocumentFile>();

    public ICollection<DocumentAccessLog> AccessLogs { get; set; } = new List<DocumentAccessLog>();
}
