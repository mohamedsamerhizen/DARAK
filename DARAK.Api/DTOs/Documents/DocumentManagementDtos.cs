using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Documents;

public sealed class DocumentRequirementSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public DocumentCategory? Category { get; init; }

    public DocumentRequirementAppliesTo? AppliesTo { get; init; }

    public bool? IsMandatory { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(150)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateDocumentRequirementRequest
{
    public Guid CompoundId { get; init; }

    public DocumentCategory Category { get; init; }

    public DocumentRequirementAppliesTo AppliesTo { get; init; } = DocumentRequirementAppliesTo.Resident;

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsMandatory { get; init; } = true;

    [Range(1, 3650)]
    public int? ValidityDays { get; init; }

    public bool RequiresApproval { get; init; } = true;
}

public sealed class UpdateDocumentRequirementRequest
{
    public DocumentCategory Category { get; init; }

    public DocumentRequirementAppliesTo AppliesTo { get; init; } = DocumentRequirementAppliesTo.Resident;

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsMandatory { get; init; } = true;

    [Range(1, 3650)]
    public int? ValidityDays { get; init; }

    public bool RequiresApproval { get; init; } = true;

    public bool IsActive { get; init; } = true;
}

public sealed class ReviewDocumentRequest
{
    [MaxLength(1000)]
    public string? Reason { get; init; }
}

public sealed record DocumentRequirementResponse(
    Guid Id,
    Guid CompoundId,
    DocumentCategory Category,
    DocumentRequirementAppliesTo AppliesTo,
    string Title,
    string? Description,
    bool IsMandatory,
    int? ValidityDays,
    bool RequiresApproval,
    bool IsActive,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? DeactivatedAtUtc);

public sealed record DocumentManagementDashboardResponse(
    Guid? CompoundId,
    int TotalActiveDocuments,
    int PendingReviewCount,
    int ApprovedCount,
    int RejectedCount,
    int ExpiredCount,
    int ExpiringSoonCount,
    int ActiveRequirementCount,
    int MandatoryRequirementCount,
    int MissingMandatoryDocumentCount);


public sealed record DocumentComplianceReportResponse(
    Guid? CompoundId,
    int TotalResidents,
    int FullyCompliantResidentCount,
    int NonCompliantResidentCount,
    int MandatoryRequirementCount,
    int MissingMandatoryDocumentCount,
    int ExpiredDocumentCount,
    int ExpiringSoonDocumentCount,
    IReadOnlyCollection<DocumentComplianceResidentResponse> Residents);

public sealed record DocumentComplianceResidentResponse(
    Guid ResidentProfileId,
    Guid CompoundId,
    string ResidentName,
    int MandatoryRequirementCount,
    int SatisfiedMandatoryRequirementCount,
    int MissingMandatoryDocumentCount,
    int ExpiredDocumentCount,
    int ExpiringSoonDocumentCount,
    bool IsCompliant,
    IReadOnlyCollection<DocumentComplianceGapResponse> Gaps);

public sealed record DocumentComplianceGapResponse(
    Guid RequirementId,
    string RequirementTitle,
    DocumentCategory Category,
    DocumentRequirementAppliesTo AppliesTo,
    string Reason,
    Guid? DocumentFileId,
    DocumentApprovalStatus? ApprovalStatus,
    DateTime? DocumentExpiresAtUtc);

public sealed record ResidentDocumentChecklistResponse(
    Guid ResidentProfileId,
    Guid CompoundId,
    string ResidentName,
    IReadOnlyCollection<ResidentDocumentChecklistItemResponse> Items);

public sealed record ResidentDocumentChecklistItemResponse(
    Guid RequirementId,
    string RequirementTitle,
    DocumentCategory Category,
    DocumentRequirementAppliesTo AppliesTo,
    bool IsMandatory,
    int? ValidityDays,
    bool RequiresApproval,
    bool IsSatisfied,
    Guid? DocumentFileId,
    DocumentApprovalStatus? ApprovalStatus,
    DateTime? DocumentExpiresAtUtc);
