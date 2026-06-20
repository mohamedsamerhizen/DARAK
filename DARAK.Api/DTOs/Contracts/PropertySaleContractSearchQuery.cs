using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.PropertySales;

public sealed class PropertySaleContractSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public SaleType? SaleType { get; init; }

    public SaleContractStatus? ContractStatus { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
