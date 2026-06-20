using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class WorkOrderRating
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkOrderId { get; set; }

    public Guid UserId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkOrder WorkOrder { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
