using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.FamilyMembers;

public sealed class UpdateFamilyMemberRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Relationship { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; } = true;
}
