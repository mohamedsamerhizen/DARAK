using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ViolationFine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ViolationId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public decimal Amount { get; set; }

    public decimal PaidAmount { get; set; }

    public ViolationFineStatus Status { get; set; } = ViolationFineStatus.Unpaid;

    public string Reason { get; set; } = string.Empty;

    public DateOnly DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public Violation Violation { get; set; } = null!;

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }
}
