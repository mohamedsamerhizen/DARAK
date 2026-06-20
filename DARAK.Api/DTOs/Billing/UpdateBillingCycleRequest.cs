using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.BillingCycles;

public sealed class UpdateBillingCycleRequest
{
    [Range(2000, 2100)]
    public int Year { get; init; }

    [Range(1, 12)]
    public int Month { get; init; }

    public DateOnly PeriodStart { get; init; }

    public DateOnly PeriodEnd { get; init; }

    public DateOnly DueDate { get; init; }
}
