using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Rents;

public sealed class GenerateMonthlyRentInvoicesRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Range(2000, 2200)]
    public int Year { get; init; }

    [Range(1, 12)]
    public int Month { get; init; }

    [Required]
    public DateOnly IssueDate { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    public bool IncludePreviousBalance { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
