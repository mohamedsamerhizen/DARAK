using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Meters;

public sealed class GenerateBillLineFromReadingRequest
{
    public Guid UtilityBillId { get; init; }

    public Guid MeterReadingId { get; init; }

    public Guid CompoundServiceId { get; init; }

    [MaxLength(300)]
    public string? DescriptionOverride { get; init; }
}
