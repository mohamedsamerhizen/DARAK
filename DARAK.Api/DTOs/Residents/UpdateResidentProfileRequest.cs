using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Residents;

public sealed class UpdateResidentProfileRequest
{
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

    public bool IsActive { get; init; } = true;
}
