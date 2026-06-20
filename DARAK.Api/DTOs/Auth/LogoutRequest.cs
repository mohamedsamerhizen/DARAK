using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs;

public sealed class LogoutRequest
{
    [Required]
    [MaxLength(512)]
    public string RefreshToken { get; init; } = string.Empty;
}
