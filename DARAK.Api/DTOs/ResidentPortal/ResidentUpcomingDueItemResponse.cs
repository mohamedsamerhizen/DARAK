namespace DARAK.Api.DTOs.ResidentPortal;

public sealed record ResidentUpcomingDueItemResponse(
    string Type,
    Guid TargetId,
    string Label,
    DateOnly DueDate,
    decimal Amount,
    string Status);
