using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class UnitHandoverChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UnitHandoverChecklistId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public UnitHandoverItemStatus Status { get; set; } = UnitHandoverItemStatus.Pending;

    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public UnitHandoverChecklist Checklist { get; set; } = null!;
}
