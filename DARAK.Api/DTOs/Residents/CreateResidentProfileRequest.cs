using System.ComponentModel.DataAnnotations;
using DARAK.Api.Validation;

namespace DARAK.Api.DTOs.Residents;

public sealed class CreateResidentProfileRequest
{
    [Required]
    [NotEmptyGuid]
    public Guid UserId { get; init; }

    [Required]
    [NotEmptyGuid]
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? NationalId { get; init; }

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }

    [MaxLength(30)]
    public string? AlternativePhoneNumber { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
