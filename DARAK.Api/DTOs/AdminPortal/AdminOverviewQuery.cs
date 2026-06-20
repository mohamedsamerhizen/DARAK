namespace DARAK.Api.DTOs.AdminPortal;

public sealed class AdminOverviewQuery
{
    public Guid? CompoundId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public int TopCount { get; init; } = 10;
}
