using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Operations;

public sealed record AccessControlOperationsSummaryResponse(
    int PendingContractorPermitCount,
    int ApprovedContractorPermitCount,
    int ContractorOnSiteCount,
    int ActiveCredentialCount,
    int ExpiringCredentialCount,
    int RevokedCredentialCount);

public sealed record ContractorWorkPermitResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid VendorId,
    string VendorName,
    Guid? RelatedWorkOrderId,
    string Purpose,
    string WorkArea,
    string? EquipmentList,
    ContractorWorkPermitRiskLevel RiskLevel,
    ContractorWorkPermitStatus Status,
    DateTime AllowedFromUtc,
    DateTime AllowedUntilUtc,
    bool RequiresEscort,
    Guid? CreatedByUserId,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    Guid? DeniedByUserId,
    DateTime? DeniedAtUtc,
    string? DenialReason,
    Guid? GuardCheckedInByUserId,
    DateTime? CheckedInAtUtc,
    Guid? GuardCheckedOutByUserId,
    DateTime? CheckedOutAtUtc,
    string? GuardNotes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class ContractorWorkPermitQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? VendorId { get; init; }

    public Guid? RelatedWorkOrderId { get; init; }

    public ContractorWorkPermitStatus? Status { get; init; }

    public ContractorWorkPermitRiskLevel? RiskLevel { get; init; }

    public DateTime? ActiveFromUtc { get; init; }

    public DateTime? ActiveUntilUtc { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateContractorWorkPermitRequest
{
    public Guid CompoundId { get; init; }

    public Guid VendorId { get; init; }

    public Guid? RelatedWorkOrderId { get; init; }

    [Required]
    [MaxLength(500)]
    public string Purpose { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string WorkArea { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? EquipmentList { get; init; }

    public ContractorWorkPermitRiskLevel RiskLevel { get; init; } = ContractorWorkPermitRiskLevel.Low;

    public DateTime AllowedFromUtc { get; init; }

    public DateTime AllowedUntilUtc { get; init; }

    public bool RequiresEscort { get; init; }
}

public sealed class ContractorPermitDecisionRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class DenyContractorWorkPermitRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class GuardContractorPermitAccessRequest
{
    [MaxLength(100)]
    public string? AccessCode { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed record AccessCredentialResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    AccessCredentialType CredentialType,
    AccessCredentialStatus Status,
    AccessCredentialOwnerType OwnerType,
    Guid? OwnerEntityId,
    string OwnerDisplayName,
    string CredentialCode,
    DateTime ValidFromUtc,
    DateTime? ValidUntilUtc,
    Guid? SourceVisitorPassId,
    Guid? SourceContractorWorkPermitId,
    Guid? RevokedByUserId,
    DateTime? RevokedAtUtc,
    string? RevocationReason,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class AccessCredentialQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public AccessCredentialType? CredentialType { get; init; }

    public AccessCredentialStatus? Status { get; init; }

    public AccessCredentialOwnerType? OwnerType { get; init; }

    public Guid? OwnerEntityId { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateAccessCredentialRequest
{
    public Guid CompoundId { get; init; }

    public AccessCredentialType CredentialType { get; init; } = AccessCredentialType.TemporaryAccessCode;

    public AccessCredentialOwnerType OwnerType { get; init; } = AccessCredentialOwnerType.Other;

    public Guid? OwnerEntityId { get; init; }

    [Required]
    [MaxLength(150)]
    public string OwnerDisplayName { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? CredentialCode { get; init; }

    public DateTime ValidFromUtc { get; init; }

    public DateTime? ValidUntilUtc { get; init; }

    public Guid? SourceVisitorPassId { get; init; }

    public Guid? SourceContractorWorkPermitId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class RevokeAccessCredentialRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record AccessControlProDashboardResponse(
    Guid? CompoundId,
    int TodayExpectedVisitorCount,
    int TodayCheckedInVisitorCount,
    int PendingVisitorApprovalCount,
    int OverstayingVisitorCount,
    int VisitorDeniedTodayCount,
    int TodayContractorPermitCount,
    int ContractorCheckedInCount,
    int ContractorEscortRequiredCount,
    int HighRiskContractorPermitCount,
    int PendingContractorApprovalCount,
    int ActiveCredentialCount,
    int ExpiringCredentialCount,
    int RevokedCredentialCount,
    int SecurityCommandItemCount,
    IReadOnlyList<AccessControlMetricBucketResponse> RiskBuckets);

public sealed record AccessControlMetricBucketResponse(
    string Key,
    int Count);

public sealed record AccessSecurityCommandQueueItemResponse(
    string SourceType,
    Guid SourceId,
    Guid CompoundId,
    string CompoundName,
    string Subject,
    string RiskLevel,
    string Status,
    string Reason,
    string RecommendedAction,
    DateTime DetectedAtUtc);

public sealed class AccessSecurityCommandQueueQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public string? RiskLevel { get; init; }

    public string? SourceType { get; init; }

    public bool IncludeInformational { get; init; }
}

public sealed record AccessCredentialRiskQueueItemResponse(
    Guid CredentialId,
    Guid CompoundId,
    string CompoundName,
    AccessCredentialType CredentialType,
    AccessCredentialOwnerType OwnerType,
    string OwnerDisplayName,
    string CredentialCode,
    AccessCredentialStatus Status,
    DateTime ValidFromUtc,
    DateTime? ValidUntilUtc,
    string RiskLevel,
    string RiskReason,
    string RecommendedAction);

public sealed class AccessCredentialRiskQueueQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public AccessCredentialOwnerType? OwnerType { get; init; }

    public string? RiskLevel { get; init; }
}

public sealed record ContractorEscortQueueItemResponse(
    Guid ContractorWorkPermitId,
    Guid CompoundId,
    string CompoundName,
    Guid VendorId,
    string VendorName,
    string Purpose,
    string WorkArea,
    ContractorWorkPermitRiskLevel RiskLevel,
    ContractorWorkPermitStatus Status,
    bool RequiresEscort,
    DateTime AllowedFromUtc,
    DateTime AllowedUntilUtc,
    DateTime? CheckedInAtUtc,
    string EscortStatus,
    string RecommendedAction);

public sealed class ContractorEscortQueueQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public ContractorWorkPermitRiskLevel? MinimumRiskLevel { get; init; }

    public bool OnlyActiveWindow { get; init; } = true;
}

public sealed record AccessAuditTrailItemResponse(
    string SourceType,
    Guid SourceId,
    Guid CompoundId,
    string CompoundName,
    string Subject,
    string Action,
    Guid? GuardUserId,
    string? GuardName,
    DateTime OccurredAtUtc,
    string? Notes);

public sealed class AccessAuditTrailQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? UntilUtc { get; init; }

    public string? SourceType { get; init; }
}


public sealed record AccessGateSituationReportResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    int TodayExpectedVisitorCount,
    int VisitorOnSiteCount,
    int VisitorOverstayingCount,
    int PendingVisitorApprovalCount,
    int VisitorDeniedTodayCount,
    int TodayContractorPermitCount,
    int ContractorOnSiteCount,
    int ContractorOverstayingCount,
    int PendingContractorApprovalCount,
    int ContractorEscortRequiredCount,
    int HighRiskContractorPermitCount,
    int ActiveCredentialCount,
    int ExpiredActiveCredentialCount,
    int LostCredentialCount,
    int CriticalActionCount,
    int HighActionCount,
    int MediumActionCount,
    string RecommendedDecision,
    IReadOnlyList<AccessControlMetricBucketResponse> OperationalBuckets,
    IReadOnlyList<AccessSecurityCommandQueueItemResponse> TopSecurityActions);

public sealed record VisitorVerificationBoardItemResponse(
    Guid VisitorPassId,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentName,
    string VisitorName,
    string VisitorPhoneNumber,
    VisitorPassStatus Status,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    DateTime? CheckedInAtUtc,
    DateTime? CheckedOutAtUtc,
    string RiskLevel,
    string VerificationStatus,
    bool ActionRequired,
    string RecommendedAction,
    string? LastAccessAction,
    DateTime? LastAccessAtUtc);

public sealed class VisitorVerificationBoardQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public VisitorPassStatus? Status { get; init; }

    public bool IncludeFuture { get; init; }

    public bool OnlyActionRequired { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed record ContractorAccessComplianceBoardItemResponse(
    Guid ContractorWorkPermitId,
    Guid CompoundId,
    string CompoundName,
    Guid VendorId,
    string VendorName,
    string Purpose,
    string WorkArea,
    ContractorWorkPermitRiskLevel RiskLevel,
    ContractorWorkPermitStatus Status,
    DateTime AllowedFromUtc,
    DateTime AllowedUntilUtc,
    bool RequiresEscort,
    DateTime? CheckedInAtUtc,
    DateTime? CheckedOutAtUtc,
    bool IsOverstaying,
    bool RequiresEscortVerification,
    bool ActionRequired,
    string ComplianceStatus,
    string RecommendedAction);

public sealed class ContractorAccessComplianceBoardQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public ContractorWorkPermitRiskLevel? MinimumRiskLevel { get; init; }

    public ContractorWorkPermitStatus? Status { get; init; }

    public bool OnlyActionRequired { get; init; }
}

public sealed record AccessCredentialControlBoardItemResponse(
    Guid CredentialId,
    Guid CompoundId,
    string CompoundName,
    AccessCredentialType CredentialType,
    AccessCredentialOwnerType OwnerType,
    string OwnerDisplayName,
    string CredentialCode,
    AccessCredentialStatus Status,
    DateTime ValidFromUtc,
    DateTime? ValidUntilUtc,
    bool ActionRequired,
    string ControlStatus,
    string RiskLevel,
    string RecommendedAction);

public sealed class AccessCredentialControlBoardQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public AccessCredentialOwnerType? OwnerType { get; init; }

    public AccessCredentialStatus? Status { get; init; }

    public bool OnlyActionRequired { get; init; }
}

public sealed record GuardShiftHandoverReportResponse(
    Guid? CompoundId,
    DateTime ShiftStartUtc,
    DateTime ShiftEndUtc,
    int VisitorCheckInCount,
    int VisitorCheckOutCount,
    int VisitorDeniedCount,
    int ContractorCheckInCount,
    int ContractorCheckOutCount,
    int OpenVisitorOnSiteCount,
    int OpenContractorOnSiteCount,
    int CriticalOpenActionCount,
    string HandoverSummary,
    IReadOnlyList<AccessAuditTrailItemResponse> RecentAccessEvents,
    IReadOnlyList<AccessSecurityCommandQueueItemResponse> OpenSecurityActions);
