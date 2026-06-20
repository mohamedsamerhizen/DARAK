namespace DARAK.Api.DTOs;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyCollection<string> Roles);
