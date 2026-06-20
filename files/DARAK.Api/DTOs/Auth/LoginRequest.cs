using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}
