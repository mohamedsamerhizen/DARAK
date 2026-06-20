namespace DARAK.Api.Entities;

public sealed class OperationalChecklistTemplateItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OperationalChecklistTemplateId { get; set; }

    public OperationalChecklistTemplate Template { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsRequired { get; set; } = true;

    public int SortOrder { get; set; }
}
