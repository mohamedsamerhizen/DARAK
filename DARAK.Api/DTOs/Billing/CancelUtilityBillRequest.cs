using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed class CancelUtilityBillRequest
{
    [MaxLength(500)]
    public string? CancellationReason { get; init; }
}
