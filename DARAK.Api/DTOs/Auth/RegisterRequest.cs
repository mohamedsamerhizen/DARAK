using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs;

public sealed class RegisterRequest
{
    [Required]
    [MaxLength(120)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}
