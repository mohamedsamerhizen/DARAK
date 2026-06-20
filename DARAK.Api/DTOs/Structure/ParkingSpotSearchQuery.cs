using DARAK.Api.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.ParkingSpots;

public sealed class ParkingSpotSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public bool? IsCovered { get; init; }

    public bool? IsReserved { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
