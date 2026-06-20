namespace DARAK.Api.DTOs.Rents;

public sealed record GenerateMonthlyRentInvoicesResponse(
    int CreatedCount,
    int SkippedCount,
    IReadOnlyCollection<string> SkippedReasons);
