using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Financial;

public sealed class FinancialHealthDashboardQuery
{
    public Guid? CompoundId { get; init; }

    public int HighRiskLimit { get; init; } = 10;
}

public sealed record ResidentFinancialHealthResponse(
    Guid ResidentProfileId,
    Guid CompoundId,
    string ResidentName,
    decimal TotalOutstandingAmount,
    decimal OverdueAmount,
    int OverdueBillsCount,
    decimal AveragePaymentDelayDays,
    decimal OnTimePaymentRate,
    int LongestOverdueDays,
    DateTime? LastPaymentDate,
    int RecentDisputesCount,
    int FailedPaymentsCount,
    PaymentConsistency PaymentConsistency,
    ResidentFinancialHealthStatus Status,
    IReadOnlyList<string> RiskReasons);

public sealed record FinancialHealthResidentSummaryResponse(
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    ResidentFinancialHealthStatus Status,
    decimal TotalOutstandingAmount,
    decimal OverdueAmount,
    int OverdueBillsCount,
    decimal AveragePaymentDelayDays,
    decimal OnTimePaymentRate,
    int RecentDisputesCount,
    int FailedPaymentsCount,
    PaymentConsistency PaymentConsistency,
    IReadOnlyList<string> RiskReasons);

public sealed record FinancialHealthDashboardSummaryResponse(
    int ResidentsCount,
    int HealthyResidentsCount,
    int WatchResidentsCount,
    int AtRiskResidentsCount,
    int CriticalResidentsCount,
    decimal TotalOutstandingAmount,
    decimal TotalOverdueAmount,
    int TotalOverdueBillsCount,
    IReadOnlyList<FinancialHealthResidentSummaryResponse> HighestRiskResidents);
