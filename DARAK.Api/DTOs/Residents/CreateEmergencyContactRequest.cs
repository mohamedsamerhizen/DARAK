using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.EmergencyContacts;

public sealed class CreateEmergencyContactRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Relationship { get; init; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;
}
