using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class PropertySaleContract
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public SaleType SaleType { get; set; }

    public SaleContractStatus ContractStatus { get; set; } = SaleContractStatus.Active;

    public string ContractNumber { get; set; } = string.Empty;

    public DateOnly ContractDate { get; set; }

    public decimal PropertyPrice { get; set; }

    public decimal DownPaymentAmount { get; set; }

    public int InstallmentCount { get; set; }

    public DateOnly? FirstInstallmentDueDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ICollection<InstallmentScheduleItem> Installments { get; set; } = new List<InstallmentScheduleItem>();
}
