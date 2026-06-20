using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class OperationalChecklistRunItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OperationalChecklistRunId { get; set; }

    public OperationalChecklistRun Run { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsRequired { get; set; } = true;

    public int SortOrder { get; set; }

    public OperationalChecklistItemStatus Status { get; set; } = OperationalChecklistItemStatus.Pending;

    public string? Notes { get; set; }
}
