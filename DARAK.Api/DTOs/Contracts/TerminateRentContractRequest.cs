using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Rents;

public sealed class TerminateRentContractRequest
{
    [Required]
    public DateOnly TerminationDate { get; init; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
