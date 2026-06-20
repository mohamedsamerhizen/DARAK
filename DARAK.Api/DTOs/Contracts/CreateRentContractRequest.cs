using System.ComponentModel.DataAnnotations;
using DARAK.Api.Validation;

namespace DARAK.Api.DTOs.Rents;

public sealed class CreateRentContractRequest
{
    [Required]
    [NotEmptyGuid]
    public Guid CompoundId { get; init; }

    [Required]
    [NotEmptyGuid]
    public Guid PropertyUnitId { get; init; }

    [Required]
    [NotEmptyGuid]
    public Guid ResidentProfileId { get; init; }

    [Required]
    [MaxLength(100)]
    public string ContractNumber { get; init; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal MonthlyRentAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DepositAmount { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
