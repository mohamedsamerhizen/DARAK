using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed class AddUtilityBillLineRequest
{
    public Guid CompoundServiceId { get; init; }

    [MaxLength(300)]
    public string? Description { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; init; } = 1m;

    [Range(0, double.MaxValue)]
    public decimal? UnitPrice { get; init; }
}
