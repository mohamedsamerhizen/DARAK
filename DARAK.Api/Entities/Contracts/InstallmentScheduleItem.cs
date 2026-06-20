using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class InstallmentScheduleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PropertySaleContractId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public int InstallmentNumber { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public decimal PaidAmount { get; set; }

    public InstallmentStatus InstallmentStatus { get; set; } = InstallmentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public PropertySaleContract PropertySaleContract { get; set; } = null!;

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;
}
