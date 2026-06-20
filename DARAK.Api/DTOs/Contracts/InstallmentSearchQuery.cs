using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.PropertySales;

public sealed class InstallmentSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? PropertySaleContractId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public InstallmentStatus? InstallmentStatus { get; init; }

    public DateOnly? DueFrom { get; init; }

    public DateOnly? DueTo { get; init; }
}
