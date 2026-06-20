namespace DARAK.Api.DTOs.EmergencyContacts;

public sealed record EmergencyContactResponse(
    Guid Id,
    Guid ResidentProfileId,
    string FullName,
    string Relationship,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
