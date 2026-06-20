using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Meters;

public sealed class MeterReadingSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? MeterId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public MeterType? MeterType { get; init; }

    public int? Year { get; init; }

    public int? Month { get; init; }

    public bool? IsBilled { get; init; }
}
