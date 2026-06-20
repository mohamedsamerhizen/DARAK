namespace DARAK.Api.DTOs.Operational;

public sealed class AdminCommandCenterIntelligenceQuery
{
    public Guid? CompoundId { get; init; }

    public int CriticalItemLimit { get; init; } = 20;
}

public sealed record AdminCommandCenterIntelligenceResponse(
    Guid? CompoundId,
    int OverallHealthScore,
    string HealthStatus,
    int CriticalItemCount,
    int AttentionItemCount,
    IReadOnlyCollection<CommandCenterDomainCardResponse> Domains,
    IReadOnlyCollection<CommandCenterCriticalItemResponse> CriticalItems,
    DateTime GeneratedAtUtc);

public sealed record CommandCenterDomainCardResponse(
    string Domain,
    string Label,
    int OpenItemCount,
    int CriticalItemCount,
    int AttentionItemCount,
    int HealthScore,
    string Recommendation);

public sealed record CommandCenterCriticalItemResponse(
    string Domain,
    string SourceType,
    Guid SourceId,
    Guid CompoundId,
    string Title,
    string Severity,
    string Recommendation,
    DateTime CreatedAtUtc,
    DateTime? DueAtUtc);
