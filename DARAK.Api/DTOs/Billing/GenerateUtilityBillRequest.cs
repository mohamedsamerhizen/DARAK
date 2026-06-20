using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed class GenerateUtilityBillRequest
{
    public Guid CompoundId { get; init; }

    public Guid PropertyUnitId { get; init; }

    public Guid BillingCycleId { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? PreviousBalanceAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? LateFeeAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? DiscountAmount { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<AddUtilityBillLineRequest> Lines { get; init; } = [];
}
