namespace DARAK.Api.DTOs.Residents;

public sealed record ResidentProfileResponse(
    Guid Id,
    Guid UserId,
    Guid CompoundId,
    string CompoundName,
    string FullName,
    string? NationalId,
    string? PhoneNumber,
    string? AlternativePhoneNumber,
    DateOnly? DateOfBirth,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
