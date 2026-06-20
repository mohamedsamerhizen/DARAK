using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentFinancialHealthService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IResidentFinancialHealthService
{
    private const decimal LargeOutstandingAmount = 1_000_000m;
    private const int RecentWindowDays = 30;

    public async Task<ServiceResult<ResidentFinancialHealthResponse>> GetAdminResidentFinancialHealthAsync(
        Guid? currentUserId,
        Guid residentProfileId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentFinancialHealthResponse>.BadRequest("Current user is invalid.");
        }

        if (residentProfileId == Guid.Empty)
        {
            return ServiceResult<ResidentFinancialHealthResponse>.BadRequest("Resident profile id is required.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.Id == residentProfileId && profile.IsActive, cancellationToken);

        if (resident is null || !await CanAccessCompoundAsync(resident.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentFinancialHealthResponse>.NotFound("Resident financial health was not found.");
        }

        return ServiceResult<ResidentFinancialHealthResponse>.Success(
            await BuildHealthAsync(resident, cancellationToken));
    }

    public async Task<ServiceResult<ResidentFinancialHealthResponse>> GetCurrentResidentFinancialHealthAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentFinancialHealthResponse>.BadRequest("Current user is invalid.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == currentUserId.Value && profile.IsActive)
            .OrderByDescending(profile => profile.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (resident is null)
        {
            return ServiceResult<ResidentFinancialHealthResponse>.NotFound("Resident financial health was not found.");
        }

        return ServiceResult<ResidentFinancialHealthResponse>.Success(
            await BuildHealthAsync(resident, cancellationToken));
    }

    public async Task<ServiceResult<FinancialHealthDashboardSummaryResponse>> GetDashboardSummaryAsync(
        Guid? currentUserId,
        FinancialHealthDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialHealthDashboardSummaryResponse>.BadRequest("Current user is invalid.");
        }

        if (query.CompoundId == Guid.Empty)
        {
            return ServiceResult<FinancialHealthDashboardSummaryResponse>.BadRequest("Compound id is invalid.");
        }

        if (query.HighRiskLimit <= 0 || query.HighRiskLimit > 50)
        {
            return ServiceResult<FinancialHealthDashboardSummaryResponse>.BadRequest("High risk limit must be between 1 and 50.");
        }

        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<FinancialHealthDashboardSummaryResponse>.Forbidden("Current user cannot access financial health dashboard.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<FinancialHealthDashboardSummaryResponse>.Forbidden("Current user cannot access this compound.");
        }

        var residentsQuery = dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.IsActive)
            .ApplyCompoundAccess(scope, profile => profile.CompoundId);

        if (query.CompoundId.HasValue)
        {
            residentsQuery = residentsQuery.Where(profile => profile.CompoundId == query.CompoundId.Value);
        }

        var residents = await residentsQuery
            .OrderBy(profile => profile.FullName)
            .ToListAsync(cancellationToken);

        var healthItems = await BuildHealthBatchAsync(residents, cancellationToken);

        var highRisk = healthItems
            .Where(item => item.Status is ResidentFinancialHealthStatus.Critical or ResidentFinancialHealthStatus.AtRisk)
            .OrderByDescending(item => item.Status == ResidentFinancialHealthStatus.Critical)
            .ThenByDescending(item => item.OverdueAmount)
            .ThenByDescending(item => item.OverdueBillsCount)
            .ThenByDescending(item => item.AveragePaymentDelayDays)
            .Take(query.HighRiskLimit)
            .Select(item => new FinancialHealthResidentSummaryResponse(
                item.ResidentProfileId,
                item.ResidentName,
                item.CompoundId,
                item.Status,
                item.TotalOutstandingAmount,
                item.OverdueAmount,
                item.OverdueBillsCount,
                item.AveragePaymentDelayDays,
                item.OnTimePaymentRate,
                item.RecentDisputesCount,
                item.FailedPaymentsCount,
                item.PaymentConsistency,
                item.RiskReasons))
            .ToArray();

        return ServiceResult<FinancialHealthDashboardSummaryResponse>.Success(
            new FinancialHealthDashboardSummaryResponse(
                healthItems.Count,
                healthItems.Count(item => item.Status == ResidentFinancialHealthStatus.Healthy),
                healthItems.Count(item => item.Status == ResidentFinancialHealthStatus.Watch),
                healthItems.Count(item => item.Status == ResidentFinancialHealthStatus.AtRisk),
                healthItems.Count(item => item.Status == ResidentFinancialHealthStatus.Critical),
                healthItems.Sum(item => item.TotalOutstandingAmount),
                healthItems.Sum(item => item.OverdueAmount),
                healthItems.Sum(item => item.OverdueBillsCount),
                highRisk));
    }

    private async Task<ResidentFinancialHealthResponse> BuildHealthAsync(
        ResidentProfile resident,
        CancellationToken cancellationToken)
    {
        return (await BuildHealthBatchAsync([resident], cancellationToken)).Single();
    }

    private async Task<IReadOnlyList<ResidentFinancialHealthResponse>> BuildHealthBatchAsync(
        IReadOnlyList<ResidentProfile> residents,
        CancellationToken cancellationToken)
    {
        if (residents.Count == 0)
        {
            return [];
        }

        var residentIds = residents
            .Select(resident => resident.Id)
            .Distinct()
            .ToArray();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var recentCutoff = DateTime.UtcNow.AddDays(-RecentWindowDays);

        var utilityBills = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(bill => bill.ResidentProfileId.HasValue
                && residentIds.Contains(bill.ResidentProfileId.Value)
                && bill.BillStatus != BillStatus.Cancelled)
            .Select(bill => new ResidentFinancialObligationSnapshot(
                bill.ResidentProfileId!.Value,
                bill.Id,
                PaymentTargetType.UtilityBill,
                bill.TotalAmount,
                bill.PaidAmount,
                bill.DueDate,
                bill.BillStatus == BillStatus.Overdue))
            .ToListAsync(cancellationToken);

        var rentInvoices = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(invoice => residentIds.Contains(invoice.ResidentProfileId)
                && invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .Select(invoice => new ResidentFinancialObligationSnapshot(
                invoice.ResidentProfileId,
                invoice.Id,
                PaymentTargetType.RentInvoice,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.DueDate,
                invoice.RentInvoiceStatus == RentInvoiceStatus.Overdue))
            .ToListAsync(cancellationToken);

        var installments = await dbContext.InstallmentScheduleItems
            .AsNoTracking()
            .Where(installment => residentIds.Contains(installment.ResidentProfileId)
                && installment.InstallmentStatus != InstallmentStatus.Cancelled)
            .Select(installment => new ResidentFinancialObligationSnapshot(
                installment.ResidentProfileId,
                installment.Id,
                PaymentTargetType.PropertyInstallment,
                installment.Amount,
                installment.PaidAmount,
                installment.DueDate,
                installment.InstallmentStatus == InstallmentStatus.Overdue))
            .ToListAsync(cancellationToken);

        var obligationsByResident = utilityBills
            .Concat(rentInvoices)
            .Concat(installments)
            .GroupBy(item => item.ResidentProfileId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var succeededPayments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.ResidentProfileId.HasValue
                && residentIds.Contains(payment.ResidentProfileId.Value)
                && payment.PaymentStatus == PaymentStatus.Succeeded
                && (payment.TargetType == PaymentTargetType.UtilityBill
                    || payment.TargetType == PaymentTargetType.RentInvoice
                    || payment.TargetType == PaymentTargetType.PropertyInstallment))
            .Select(payment => new ResidentPaymentSnapshot(
                payment.ResidentProfileId!.Value,
                payment.TargetType,
                payment.TargetId,
                payment.CompletedAt ?? payment.UpdatedAt ?? payment.CreatedAt))
            .ToListAsync(cancellationToken);

        var paymentMetricsByResident = await CalculatePaymentMetricsBatchAsync(
            succeededPayments,
            cancellationToken);

        var recentDisputesByResident = await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => residentIds.Contains(conversation.ResidentProfileId)
                && conversation.Topic == ConversationTopic.Billing
                && conversation.LinkedEntityType == ConversationLinkedEntityType.UtilityBill
                && conversation.CreatedAtUtc >= recentCutoff)
            .GroupBy(conversation => conversation.ResidentProfileId)
            .Select(group => new { ResidentProfileId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ResidentProfileId, item => item.Count, cancellationToken);

        var failedPaymentsByResident = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.ResidentProfileId.HasValue
                && residentIds.Contains(payment.ResidentProfileId.Value)
                && payment.PaymentStatus == PaymentStatus.Failed)
            .GroupBy(payment => payment.ResidentProfileId!.Value)
            .Select(group => new { ResidentProfileId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ResidentProfileId, item => item.Count, cancellationToken);

        var failedAttemptItems = await dbContext.PaymentAttempts
            .AsNoTracking()
            .Where(attempt => attempt.AttemptStatus == PaymentStatus.Failed
                && attempt.Payment.ResidentProfileId.HasValue
                && residentIds.Contains(attempt.Payment.ResidentProfileId.Value))
            .Select(attempt => new
            {
                ResidentProfileId = attempt.Payment.ResidentProfileId!.Value,
                attempt.PaymentId
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var failedAttemptsByResident = failedAttemptItems
            .GroupBy(item => item.ResidentProfileId)
            .ToDictionary(group => group.Key, group => group.Count());

        var healthItems = new List<ResidentFinancialHealthResponse>(residents.Count);
        foreach (var resident in residents)
        {
            obligationsByResident.TryGetValue(resident.Id, out var obligations);
            paymentMetricsByResident.TryGetValue(resident.Id, out var paymentMetrics);
            recentDisputesByResident.TryGetValue(resident.Id, out var recentDisputesCount);
            failedPaymentsByResident.TryGetValue(resident.Id, out var failedPaymentsCount);
            failedAttemptsByResident.TryGetValue(resident.Id, out var failedAttemptCount);

            healthItems.Add(BuildHealthResponse(
                resident,
                obligations ?? [],
                paymentMetrics ?? PaymentBehaviorMetrics.Empty,
                recentDisputesCount,
                Math.Max(failedPaymentsCount, failedAttemptCount),
                today));
        }

        return healthItems;
    }

    private async Task<IReadOnlyDictionary<Guid, PaymentBehaviorMetrics>> CalculatePaymentMetricsBatchAsync(
        IReadOnlyList<ResidentPaymentSnapshot> succeededPayments,
        CancellationToken cancellationToken)
    {
        if (succeededPayments.Count == 0)
        {
            return new Dictionary<Guid, PaymentBehaviorMetrics>();
        }

        var utilityBillIds = succeededPayments
            .Where(payment => payment.TargetType == PaymentTargetType.UtilityBill)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        var rentInvoiceIds = succeededPayments
            .Where(payment => payment.TargetType == PaymentTargetType.RentInvoice)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        var installmentIds = succeededPayments
            .Where(payment => payment.TargetType == PaymentTargetType.PropertyInstallment)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();

        var utilityDueDates = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(bill => utilityBillIds.Contains(bill.Id))
            .Select(bill => new { bill.Id, bill.DueDate })
            .ToDictionaryAsync(item => item.Id, item => item.DueDate, cancellationToken);

        var rentDueDates = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(invoice => rentInvoiceIds.Contains(invoice.Id))
            .Select(invoice => new { invoice.Id, invoice.DueDate })
            .ToDictionaryAsync(item => item.Id, item => item.DueDate, cancellationToken);

        var installmentDueDates = await dbContext.InstallmentScheduleItems
            .AsNoTracking()
            .Where(installment => installmentIds.Contains(installment.Id))
            .Select(installment => new { installment.Id, installment.DueDate })
            .ToDictionaryAsync(item => item.Id, item => item.DueDate, cancellationToken);

        var metrics = new Dictionary<Guid, PaymentBehaviorMetrics>();
        foreach (var group in succeededPayments.GroupBy(payment => payment.ResidentProfileId))
        {
            var payments = group.ToArray();
            var delays = new List<int>();
            foreach (var payment in payments)
            {
                DateOnly? dueDate = payment.TargetType switch
                {
                    PaymentTargetType.UtilityBill when utilityDueDates.TryGetValue(payment.TargetId, out var utilityDueDate) => utilityDueDate,
                    PaymentTargetType.RentInvoice when rentDueDates.TryGetValue(payment.TargetId, out var rentDueDate) => rentDueDate,
                    PaymentTargetType.PropertyInstallment when installmentDueDates.TryGetValue(payment.TargetId, out var installmentDueDate) => installmentDueDate,
                    _ => null
                };

                if (!dueDate.HasValue)
                {
                    continue;
                }

                delays.Add(Math.Max(0, DateOnly.FromDateTime(payment.PaidAtUtc.Date).DayNumber - dueDate.Value.DayNumber));
            }

            if (delays.Count == 0)
            {
                metrics[group.Key] = new PaymentBehaviorMetrics(
                    0m,
                    0m,
                    payments.Max(payment => payment.PaidAtUtc),
                    PaymentConsistency.NoHistory,
                    0);
                continue;
            }

            var averageDelay = Math.Round((decimal)delays.Average(), 2);
            var onTimeRate = Math.Round(delays.Count(delay => delay == 0) * 100m / delays.Count, 2);
            var consistency = DeterminePaymentConsistency(averageDelay, onTimeRate, delays.Count);

            metrics[group.Key] = new PaymentBehaviorMetrics(
                averageDelay,
                onTimeRate,
                payments.Max(payment => payment.PaidAtUtc),
                consistency,
                delays.Count);
        }

        return metrics;
    }

    private static ResidentFinancialHealthResponse BuildHealthResponse(
        ResidentProfile resident,
        IReadOnlyList<ResidentFinancialObligationSnapshot> obligations,
        PaymentBehaviorMetrics paymentMetrics,
        int recentDisputesCount,
        int failedPaymentsCount,
        DateOnly today)
    {
        var outstandingItems = obligations
            .Select(item => item with
            {
                RemainingAmount = Math.Max(0m, item.TotalAmount - item.PaidAmount),
                IsOverdue = item.IsOverdue || item.DueDate < today
            })
            .Where(item => item.RemainingAmount > 0m)
            .ToArray();

        var totalOutstandingAmount = outstandingItems.Sum(item => item.RemainingAmount);
        var overdueItems = outstandingItems.Where(item => item.IsOverdue).ToArray();
        var overdueAmount = overdueItems.Sum(item => item.RemainingAmount);
        var overdueBillsCount = overdueItems.Length;
        var longestOverdueDays = overdueItems.Length == 0
            ? 0
            : overdueItems.Max(item => Math.Max(0, today.DayNumber - item.DueDate.DayNumber));

        var status = DetermineStatus(
            totalOutstandingAmount,
            overdueAmount,
            overdueBillsCount,
            longestOverdueDays,
            paymentMetrics.AveragePaymentDelayDays,
            paymentMetrics.OnTimePaymentRate,
            paymentMetrics.PaymentConsistency,
            recentDisputesCount,
            failedPaymentsCount);

        var riskReasons = BuildRiskReasons(
            totalOutstandingAmount,
            overdueAmount,
            overdueBillsCount,
            longestOverdueDays,
            paymentMetrics.AveragePaymentDelayDays,
            paymentMetrics.OnTimePaymentRate,
            paymentMetrics.PaymentConsistency,
            paymentMetrics.PaymentSamplesCount,
            paymentMetrics.LastPaymentDate,
            recentDisputesCount,
            failedPaymentsCount);

        return new ResidentFinancialHealthResponse(
            resident.Id,
            resident.CompoundId,
            resident.FullName,
            totalOutstandingAmount,
            overdueAmount,
            overdueBillsCount,
            paymentMetrics.AveragePaymentDelayDays,
            paymentMetrics.OnTimePaymentRate,
            longestOverdueDays,
            paymentMetrics.LastPaymentDate,
            recentDisputesCount,
            failedPaymentsCount,
            paymentMetrics.PaymentConsistency,
            status,
            riskReasons);
    }

    private async Task<CompoundAccessScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            ? new CompoundAccessScope(true, true, [])
            : await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return true;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static ResidentFinancialHealthStatus DetermineStatus(
        decimal totalOutstandingAmount,
        decimal overdueAmount,
        int overdueBillsCount,
        int longestOverdueDays,
        decimal averagePaymentDelayDays,
        decimal onTimePaymentRate,
        PaymentConsistency paymentConsistency,
        int recentDisputesCount,
        int failedPaymentsCount)
    {
        if (longestOverdueDays >= 60
            || overdueAmount >= LargeOutstandingAmount
            || overdueBillsCount >= 5
            || failedPaymentsCount >= 5)
        {
            return ResidentFinancialHealthStatus.Critical;
        }

        if (overdueBillsCount >= 2
            || longestOverdueDays >= 30
            || averagePaymentDelayDays >= 21m
            || (paymentConsistency != PaymentConsistency.NoHistory && onTimePaymentRate < 60m)
            || recentDisputesCount >= 2
            || failedPaymentsCount >= 3)
        {
            return ResidentFinancialHealthStatus.AtRisk;
        }

        if (totalOutstandingAmount > 0m
            || overdueAmount > 0m
            || averagePaymentDelayDays >= 7m
            || (paymentConsistency != PaymentConsistency.NoHistory && onTimePaymentRate < 85m)
            || recentDisputesCount > 0
            || failedPaymentsCount > 0)
        {
            return ResidentFinancialHealthStatus.Watch;
        }

        return ResidentFinancialHealthStatus.Healthy;
    }

    private static PaymentConsistency DeterminePaymentConsistency(
        decimal averagePaymentDelayDays,
        decimal onTimePaymentRate,
        int sampleCount)
    {
        if (sampleCount == 0)
        {
            return PaymentConsistency.NoHistory;
        }

        if (onTimePaymentRate >= 90m && averagePaymentDelayDays <= 3m)
        {
            return PaymentConsistency.Strong;
        }

        if (onTimePaymentRate >= 75m && averagePaymentDelayDays <= 10m)
        {
            return PaymentConsistency.Stable;
        }

        if (onTimePaymentRate >= 50m && averagePaymentDelayDays <= 21m)
        {
            return PaymentConsistency.Irregular;
        }

        return PaymentConsistency.Poor;
    }

    private static IReadOnlyList<string> BuildRiskReasons(
        decimal totalOutstandingAmount,
        decimal overdueAmount,
        int overdueBillsCount,
        int longestOverdueDays,
        decimal averagePaymentDelayDays,
        decimal onTimePaymentRate,
        PaymentConsistency paymentConsistency,
        int paymentSamplesCount,
        DateTime? lastPaymentDate,
        int recentDisputesCount,
        int failedPaymentsCount)
    {
        var reasons = new List<string>();

        if (overdueBillsCount > 0)
        {
            reasons.Add($"{overdueBillsCount} overdue financial item(s).");
        }

        if (overdueAmount > 0m)
        {
            reasons.Add($"Overdue amount is {overdueAmount:N0} IQD.");
        }

        if (totalOutstandingAmount > 0m)
        {
            reasons.Add($"Total outstanding amount is {totalOutstandingAmount:N0} IQD.");
        }

        if (longestOverdueDays > 0)
        {
            reasons.Add($"Longest overdue item is {longestOverdueDays} day(s) overdue.");
        }

        if (paymentSamplesCount > 0 && averagePaymentDelayDays > 0m)
        {
            reasons.Add($"Average payment delay is {averagePaymentDelayDays:N1} day(s).");
        }

        if (paymentSamplesCount > 0 && onTimePaymentRate < 85m)
        {
            reasons.Add($"On-time payment rate is {onTimePaymentRate:N1}%.");
        }

        if (lastPaymentDate.HasValue && lastPaymentDate.Value < DateTime.UtcNow.AddDays(-45))
        {
            reasons.Add($"Last successful payment was {Math.Max(0, (DateTime.UtcNow.Date - lastPaymentDate.Value.Date).Days)} day(s) ago.");
        }

        if (recentDisputesCount > 0)
        {
            reasons.Add($"{recentDisputesCount} billing dispute(s) in the last {RecentWindowDays} days.");
        }

        if (failedPaymentsCount > 0)
        {
            reasons.Add($"{failedPaymentsCount} failed payment record(s).");
        }

        if (paymentConsistency == PaymentConsistency.NoHistory && totalOutstandingAmount > 0m)
        {
            reasons.Add("No successful payment history for current financial obligations.");
        }

        return reasons;
    }

    private sealed record ResidentFinancialObligationSnapshot(
        Guid ResidentProfileId,
        Guid Id,
        PaymentTargetType TargetType,
        decimal TotalAmount,
        decimal PaidAmount,
        DateOnly DueDate,
        bool IsOverdue)
    {
        public decimal RemainingAmount { get; init; }
    }

    private sealed record ResidentPaymentSnapshot(
        Guid ResidentProfileId,
        PaymentTargetType TargetType,
        Guid TargetId,
        DateTime PaidAtUtc);

    private sealed record PaymentBehaviorMetrics(
        decimal AveragePaymentDelayDays,
        decimal OnTimePaymentRate,
        DateTime? LastPaymentDate,
        PaymentConsistency PaymentConsistency,
        int PaymentSamplesCount)
    {
        public static PaymentBehaviorMetrics Empty { get; } = new(0m, 0m, null, PaymentConsistency.NoHistory, 0);
    }
}
