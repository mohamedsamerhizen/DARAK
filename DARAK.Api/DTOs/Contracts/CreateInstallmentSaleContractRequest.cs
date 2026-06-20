using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.PropertySales;

public sealed class CreateInstallmentSaleContractRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    public Guid PropertyUnitId { get; init; }

    [Required]
    public Guid ResidentProfileId { get; init; }

    [Required]
    [MaxLength(100)]
    public string ContractNumber { get; init; } = string.Empty;

    public DateOnly ContractDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0.01, double.MaxValue)]
    public decimal PropertyPrice { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DownPaymentAmount { get; init; }

    [Range(1, 600)]
    public int InstallmentCount { get; init; }

    [Required]
    public DateOnly FirstInstallmentDueDate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
