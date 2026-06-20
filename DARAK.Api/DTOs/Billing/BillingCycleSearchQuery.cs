using DARAK.Api.DTOs.Common;

namespace DARAK.Api.DTOs.BillingCycles;

public sealed class BillingCycleSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public int? Year { get; init; }

    public int? Month { get; init; }

    public bool? IsClosed { get; init; }
}
