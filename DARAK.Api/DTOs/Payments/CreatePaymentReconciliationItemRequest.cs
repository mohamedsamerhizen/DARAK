using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed class CreatePaymentReconciliationItemRequest
{
    [Required]
    [MaxLength(120)]
    public string ProviderTransactionId { get; init; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal ProviderAmount { get; init; }

    public PaymentStatus ProviderStatus { get; init; }
}
