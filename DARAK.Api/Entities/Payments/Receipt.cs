namespace DARAK.Api.Entities;

public sealed class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public Payment Payment { get; set; } = null!;
}
