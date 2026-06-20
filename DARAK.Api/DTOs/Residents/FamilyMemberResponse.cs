namespace DARAK.Api.DTOs.FamilyMembers;

public sealed record FamilyMemberResponse(
    Guid Id,
    Guid ResidentProfileId,
    string FullName,
    string Relationship,
    DateOnly? DateOfBirth,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
