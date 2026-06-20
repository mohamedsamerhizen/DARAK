namespace DARAK.Api.DTOs.Compounds;

public sealed record CompoundResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string City,
    string Area,
    string? Address,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
