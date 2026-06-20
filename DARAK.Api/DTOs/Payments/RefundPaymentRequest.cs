using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Payments;

public sealed class RefundPaymentRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
