using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed class GenerateMonthlyUtilityBillsRequest
{
    public Guid CompoundId { get; init; }

    public Guid BillingCycleId { get; init; }

    public bool IncludeOnlyOccupiedUnits { get; init; } = true;

    public bool IncludePreviousBalance { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
