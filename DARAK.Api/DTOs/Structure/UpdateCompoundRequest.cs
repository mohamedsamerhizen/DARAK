using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Compounds;

public sealed class UpdateCompoundRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    [MaxLength(100)]
    public string City { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Area { get; init; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; init; }

    public bool IsActive { get; init; } = true;
}
