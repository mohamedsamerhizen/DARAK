using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed class UtilityBillSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? BillingCycleId { get; init; }

    public BillStatus? BillStatus { get; init; }

    public int? Year { get; init; }

    public int? Month { get; init; }

    public DateOnly? DueBefore { get; init; }

    public DateOnly? DueAfter { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
