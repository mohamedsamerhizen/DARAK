using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class CompoundService
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public UtilityServiceType ServiceType { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal DefaultMonthlyFee { get; set; }

    public bool IsMeterBased { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public ICollection<UtilityBillLine> UtilityBillLines { get; set; } = new List<UtilityBillLine>();
}
