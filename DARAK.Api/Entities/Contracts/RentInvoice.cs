using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class RentInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RentContractId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal RentAmount { get; set; }

    public decimal PreviousBalanceAmount { get; set; }

    public decimal LateFeeAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public RentInvoiceStatus RentInvoiceStatus { get; set; } = RentInvoiceStatus.Unpaid;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public RentContract RentContract { get; set; } = null!;

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;
}
