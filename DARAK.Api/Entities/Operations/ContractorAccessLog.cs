using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ContractorAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContractorWorkPermitId { get; set; }

    public ContractorWorkPermit ContractorWorkPermit { get; set; } = null!;

    public Guid? GuardUserId { get; set; }

    public ApplicationUser? GuardUser { get; set; }

    public ContractorAccessAction Action { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
