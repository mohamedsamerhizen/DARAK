namespace DARAK.Api.Entities;

public sealed class AuditLogChange
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AuditLogEntryId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public bool IsSensitive { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public AuditLogEntry AuditLogEntry { get; set; } = null!;
}
