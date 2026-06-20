using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Meters;

public sealed class MeterSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public MeterType? MeterType { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
