using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public PaymentTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "IQD";

    public string? IdempotencyKey { get; set; }

    public string PaymentReference { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }

    public ICollection<PaymentAttempt> Attempts { get; set; } = new List<PaymentAttempt>();

    public Receipt? Receipt { get; set; }
}
