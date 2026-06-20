using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.CompoundServices;

public sealed class CreateCompoundServiceRequest
{
    public Guid CompoundId { get; init; }

    public UtilityServiceType ServiceType { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DefaultMonthlyFee { get; init; }

    public bool IsMeterBased { get; init; }
}
