using DARAK.Api.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Compounds;

public sealed class CompoundSearchQuery : PaginationQuery
{
    [MaxLength(200)]
    public string? SearchTerm { get; init; }

    public string? City { get; init; }

    public string? Area { get; init; }

    public bool? IsActive { get; init; }
}
