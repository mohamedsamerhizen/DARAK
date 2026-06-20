using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Rents;

public sealed class CancelRentInvoiceRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
