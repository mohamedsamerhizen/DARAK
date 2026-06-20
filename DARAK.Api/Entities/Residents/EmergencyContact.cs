namespace DARAK.Api.Entities;

public sealed class EmergencyContact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResidentProfileId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ResidentProfile ResidentProfile { get; set; } = null!;
}
