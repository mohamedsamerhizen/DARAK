using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class IntegrationFailureEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IntegrationName { get; set; } = string.Empty;

    public string OperationName { get; set; } = string.Empty;

    public IntegrationFailureStatus Status { get; set; } = IntegrationFailureStatus.Open;

    public string ErrorMessage { get; set; } = string.Empty;

    public int OccurrenceCount { get; set; } = 1;

    public DateTime FirstOccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastOccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public ApplicationUser? ResolvedByUser { get; set; }

    public string? ResolutionNote { get; set; }

    public string? MetadataJson { get; set; }
}
