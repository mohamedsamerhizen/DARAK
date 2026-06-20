using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.PropertySales;

public sealed class CancelSaleContractRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
