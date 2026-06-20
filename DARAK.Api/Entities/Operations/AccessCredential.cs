using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class AccessCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public AccessCredentialType CredentialType { get; set; } = AccessCredentialType.TemporaryAccessCode;

    public AccessCredentialStatus Status { get; set; } = AccessCredentialStatus.Active;

    public AccessCredentialOwnerType OwnerType { get; set; } = AccessCredentialOwnerType.Other;

    public Guid? OwnerEntityId { get; set; }

    public string OwnerDisplayName { get; set; } = string.Empty;

    public string CredentialCode { get; set; } = string.Empty;

    public DateTime ValidFromUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public Guid? SourceVisitorPassId { get; set; }

    public VisitorPass? SourceVisitorPass { get; set; }

    public Guid? SourceContractorWorkPermitId { get; set; }

    public ContractorWorkPermit? SourceContractorWorkPermit { get; set; }

    public Guid? RevokedByUserId { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevocationReason { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
