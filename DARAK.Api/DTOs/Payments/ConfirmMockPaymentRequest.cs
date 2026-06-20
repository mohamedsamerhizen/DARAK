using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Payments;

public sealed class ConfirmMockPaymentRequest
{
    [MaxLength(120)]
    public string? ProviderTransactionId { get; init; }

    [MaxLength(500)]
    public string? Message { get; init; }
}
