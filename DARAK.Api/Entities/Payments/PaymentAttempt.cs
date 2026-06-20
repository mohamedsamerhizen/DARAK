using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public PaymentStatus AttemptStatus { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? ProviderTransactionId { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Payment Payment { get; set; } = null!;
}
