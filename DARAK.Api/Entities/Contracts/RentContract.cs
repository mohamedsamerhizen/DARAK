using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class RentContract
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public string ContractNumber { get; set; } = string.Empty;

    public RentContractStatus ContractStatus { get; set; } = RentContractStatus.Active;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal MonthlyRentAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? TerminatedAt { get; set; }

    public string? TerminationReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ICollection<RentInvoice> RentInvoices { get; set; } = new List<RentInvoice>();
}
