using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Payments;

public sealed class ClosePaymentReconciliationBatchRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}
