namespace DARAK.Api.DTOs.UtilityBills;

public sealed record GenerateMonthlyUtilityBillsResponse(
    int CreatedCount,
    int SkippedCount,
    IReadOnlyCollection<string> SkippedReasons);
