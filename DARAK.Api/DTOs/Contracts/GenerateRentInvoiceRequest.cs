using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Rents;

public sealed class GenerateRentInvoiceRequest
{
    [Required]
    public Guid RentContractId { get; init; }

    [Range(2000, 2200)]
    public int Year { get; init; }

    [Range(1, 12)]
    public int Month { get; init; }

    [Required]
    public DateOnly IssueDate { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    [Range(0, double.MaxValue)]
    public decimal PreviousBalanceAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal LateFeeAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
