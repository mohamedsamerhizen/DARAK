namespace DARAK.Api.DTOs.BillingCycles;

public sealed record BillingCycleResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    int Year,
    int Month,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    bool IsClosed,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
