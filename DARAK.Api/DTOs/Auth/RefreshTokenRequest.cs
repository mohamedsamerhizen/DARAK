using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs;

public sealed class RefreshTokenRequest
{
    [Required]
    [MaxLength(512)]
    public string RefreshToken { get; init; } = string.Empty;
}
