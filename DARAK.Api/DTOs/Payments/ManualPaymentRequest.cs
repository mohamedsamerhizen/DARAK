using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed class ManualPaymentRequest
{
    public PaymentTargetType TargetType { get; init; }

    public Guid TargetId { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [MaxLength(120)]
    public string? IdempotencyKey { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}
