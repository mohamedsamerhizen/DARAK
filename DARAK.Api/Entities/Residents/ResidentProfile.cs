using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ResidentProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid CompoundId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? NationalId { get; set; }

    public string? PhoneNumber { get; set; }

    public string? AlternativePhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Compound Compound { get; set; } = null!;

    public ICollection<OccupancyRecord> OccupancyRecords { get; set; } = new List<OccupancyRecord>();

    public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();

    public ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();
}
