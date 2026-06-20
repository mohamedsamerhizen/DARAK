using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.CompoundServices;

public sealed class CompoundServiceSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public UtilityServiceType? ServiceType { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
