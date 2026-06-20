using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ResidentCustodyItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public CustodyItemType ItemType { get; set; }

    public CustodyItemStatus Status { get; set; } = CustodyItemStatus.Issued;

    public string Identifier { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal? ReplacementFeeAmount { get; set; }

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReturnedAtUtc { get; set; }

    public Guid? IssuedByUserId { get; set; }

    public Guid? ReturnedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;
}
