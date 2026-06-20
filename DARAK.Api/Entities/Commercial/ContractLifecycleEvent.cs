using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ContractLifecycleEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public CommercialContractType ContractType { get; set; }

    public Guid ContractId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public ContractLifecycleEventType EventType { get; set; }

    public Guid? ActorUserId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;
}
