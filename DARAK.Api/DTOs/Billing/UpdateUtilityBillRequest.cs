using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed class UpdateUtilityBillRequest
{
    public DateOnly DueDate { get; init; }

    [Range(0, double.MaxValue)]
    public decimal LateFeeAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
