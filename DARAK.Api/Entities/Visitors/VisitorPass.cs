using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class VisitorPass
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResidentProfileId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public string VisitorName { get; set; } = string.Empty;

    public string VisitorPhoneNumber { get; set; } = string.Empty;

    public string VisitReason { get; set; } = string.Empty;

    public string AccessCode { get; set; } = string.Empty;

    public VisitorPassStatus Status { get; set; } = VisitorPassStatus.Pending;

    public DateTime ValidFrom { get; set; }

    public DateTime ValidUntil { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public DateTime? CheckedOutAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? DenialReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ICollection<VisitorAccessLog> AccessLogs { get; set; } = new List<VisitorAccessLog>();
}
