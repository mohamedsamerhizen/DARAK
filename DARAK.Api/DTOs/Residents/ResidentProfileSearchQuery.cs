using DARAK.Api.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Residents;

public sealed class ResidentProfileSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }

    public bool? IsActive { get; init; }
}
