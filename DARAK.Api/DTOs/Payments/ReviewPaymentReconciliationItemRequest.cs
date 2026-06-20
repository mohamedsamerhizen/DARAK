using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed class ReviewPaymentReconciliationItemRequest
{
    public PaymentReconciliationReviewDecision Decision { get; init; }

    [Required]
    [MaxLength(1000)]
    public string ReviewNotes { get; init; } = string.Empty;
}
