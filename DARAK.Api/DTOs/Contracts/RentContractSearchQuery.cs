using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Rents;

public sealed class RentContractSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public RentContractStatus? ContractStatus { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
