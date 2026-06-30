using System.Text.Json;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Approvals;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class FinancialControlService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : IFinancialControlService
{
    private const string DefaultCurrency = "IQD";

    public async Task<ServiceResult<FinancialControlDashboardResponse>> GetDashboardAsync(
        FinancialDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateRange = NormalizeDateRange(query.FromDate, query.ToDate);
        if (dateRange.Error is not null)
        {
            return ServiceResult<FinancialControlDashboardResponse>.BadRequest(dateRange.Error);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<FinancialControlDashboardResponse>.Forbidden("Current user cannot access finance dashboard.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<FinancialControlDashboardResponse>.NotFound("Finance dashboard was not found.");
        }

        var residentQuery = ApplyOptionalCompoundFilter(
            dbContext.ResidentProfiles.AsNoTracking().ApplyCompoundAccess(scope, resident => resident.CompoundId),
            query.CompoundId);

        var activeResidentCount = await residentQuery.CountAsync(resident => resident.IsActive, cancellationToken);

        var outstanding = await CalculateOutstandingAsync(scope, query.CompoundId, dateRange.ToDate, cancellationToken);
        var revenue = await CalculateRevenueAsync(scope, query.CompoundId, dateRange.FromUtc, dateRange.ToExclusiveUtc, cancellationToken);
        var adjustmentRows = await ApplyOptionalCompoundFilter(
                dbContext.FinancialAdjustments.AsNoTracking().ApplyCompoundAccess(scope, adjustment => adjustment.CompoundId),
                query.CompoundId)
            .Select(adjustment => new
            {
                adjustment.Status,
                adjustment.AdjustmentType,
                adjustment.Amount
            })
            .ToListAsync(cancellationToken);

        var adjustmentCounts = adjustmentRows
            .GroupBy(adjustment => adjustment.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToArray();

        var appliedAdjustments = adjustmentRows
            .Where(adjustment => adjustment.Status == FinancialAdjustmentStatus.Applied)
            .ToArray();

        var response = new FinancialControlDashboardResponse(
            query.CompoundId,
            dateRange.FromDate,
            dateRange.ToDate,
            activeResidentCount,
            outstanding.TotalOutstanding,
            outstanding.TotalOverdue,
            outstanding.UtilityBillOutstanding,
            outstanding.RentInvoiceOutstanding,
            outstanding.InstallmentOutstanding,
            outstanding.ViolationFineOutstanding,
            revenue.Collected,
            revenue.Refunded,
            revenue.NetCollected,
            appliedAdjustments.Where(item => item.AdjustmentType == FinancialAdjustmentType.Credit).Sum(item => item.Amount),
            appliedAdjustments.Where(item => item.AdjustmentType == FinancialAdjustmentType.Debit).Sum(item => item.Amount),
            adjustmentCounts.FirstOrDefault(item => item.Status == FinancialAdjustmentStatus.PendingApproval)?.Count ?? 0,
            adjustmentCounts.FirstOrDefault(item => item.Status == FinancialAdjustmentStatus.Applied)?.Count ?? 0,
            adjustmentCounts.FirstOrDefault(item => item.Status == FinancialAdjustmentStatus.Cancelled)?.Count ?? 0,
            DateTime.UtcNow);

        return ServiceResult<FinancialControlDashboardResponse>.Success(response);
    }

    public async Task<ServiceResult<ResidentStatementResponse>> GetResidentStatementAsync(
        Guid residentProfileId,
        ResidentStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        if (residentProfileId == Guid.Empty)
        {
            return ServiceResult<ResidentStatementResponse>.BadRequest("Resident profile id is required.");
        }

        var dateRange = NormalizeOptionalDateRange(query.FromDate, query.ToDate);
        if (dateRange.Error is not null)
        {
            return ServiceResult<ResidentStatementResponse>.BadRequest(dateRange.Error);
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentProfileId, cancellationToken);
        if (resident is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(resident.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentStatementResponse>.NotFound("Resident statement was not found.");
        }

        return await BuildResidentStatementResponseAsync(resident, query, dateRange, cancellationToken);
    }

    public async Task<ServiceResult<ResidentStatementResponse>> GetResidentStatementForUserAsync(
        Guid userId,
        ResidentStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<ResidentStatementResponse>.BadRequest("Current user is invalid.");
        }

        var dateRange = NormalizeOptionalDateRange(query.FromDate, query.ToDate);
        if (dateRange.Error is not null)
        {
            return ServiceResult<ResidentStatementResponse>.BadRequest(dateRange.Error);
        }

        var profiles = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .OrderBy(profile => profile.CreatedAt)
            .ToArrayAsync(cancellationToken);
        if (profiles.Length == 0)
        {
            return ServiceResult<ResidentStatementResponse>.NotFound("Resident statement was not found.");
        }

        ResidentProfile resident;
        if (query.ResidentProfileId.HasValue)
        {
            resident = profiles.FirstOrDefault(profile => profile.Id == query.ResidentProfileId.Value)!;
            if (resident is null)
            {
                return ServiceResult<ResidentStatementResponse>.NotFound("Resident statement was not found.");
            }
        }
        else
        {
            if (profiles.Length > 1)
            {
                return ServiceResult<ResidentStatementResponse>.BadRequest(
                    "residentProfileId is required when the current user has multiple resident profiles.");
            }

            resident = profiles[0];
        }

        return await BuildResidentStatementResponseAsync(resident, query, dateRange, cancellationToken);
    }

    private async Task<ServiceResult<ResidentStatementResponse>> BuildResidentStatementResponseAsync(
        ResidentProfile resident,
        ResidentStatementQuery query,
        OptionalDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var lines = await BuildResidentStatementLinesAsync(resident.Id, resident.CompoundId, dateRange, cancellationToken);
        var orderedLines = lines
            .OrderBy(line => line.OccurredAtUtc)
            .ThenBy(line => line.SourceType)
            .ThenBy(line => line.Reference)
            .ToArray();

        var runningBalance = 0m;
        var responseLines = orderedLines.Select(line =>
        {
            runningBalance += line.DebitAmount - line.CreditAmount;
            return new ResidentStatementLineResponse(
                line.OccurredAtUtc,
                line.SourceType,
                line.SourceId,
                line.Reference,
                line.Description,
                line.DebitAmount,
                line.CreditAmount,
                runningBalance,
                line.IsUnderFinancialReview,
                line.FinancialDisputeId,
                line.FinancialDisputeStatus,
                line.ViolationAppealId,
                line.ViolationAppealStatus,
                line.FinancialAdjustmentId,
                line.FinancialAdjustmentStatus);
        }).ToArray();

        var response = new ResidentStatementResponse(
            resident.Id,
            resident.CompoundId,
            resident.FullName,
            DefaultCurrency,
            dateRange.FromDate,
            dateRange.ToDate,
            responseLines.Sum(line => line.DebitAmount),
            responseLines.Sum(line => line.CreditAmount),
            responseLines.LastOrDefault()?.BalanceAfterLine ?? 0m,
            responseLines.Count(line => line.IsUnderFinancialReview),
            responseLines.Where(line => line.FinancialAdjustmentStatus == FinancialAdjustmentStatus.Applied).Sum(line => line.CreditAmount),
            responseLines.Where(line => line.FinancialAdjustmentStatus == FinancialAdjustmentStatus.Applied).Sum(line => line.DebitAmount),
            responseLines);

        return ServiceResult<ResidentStatementResponse>.Success(response);
    }

    public async Task<ServiceResult<PagedResult<FinancialAdjustmentResponse>>> SearchAdjustmentsAsync(
        FinancialAdjustmentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<FinancialAdjustmentResponse>>.Forbidden("Current user cannot access financial adjustments.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<FinancialAdjustmentResponse>>.Success(
                new PagedResult<FinancialAdjustmentResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var adjustments = ApplyAdjustmentFilters(
            dbContext.FinancialAdjustments
                .AsNoTracking()
                .Include(adjustment => adjustment.ResidentProfile)
                .ApplyCompoundAccess(scope, adjustment => adjustment.CompoundId),
            query);

        var totalCount = await adjustments.CountAsync(cancellationToken);
        var items = await adjustments
            .OrderByDescending(adjustment => adjustment.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(adjustment => ToAdjustmentResponse(adjustment))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<FinancialAdjustmentResponse>>.Success(
            new PagedResult<FinancialAdjustmentResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<FinancialAdjustmentResponse>> GetAdjustmentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<FinancialAdjustmentResponse>.BadRequest("Financial adjustment id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var adjustment = await GetScopedAdjustmentQuery(scope, asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return ServiceResult<FinancialAdjustmentResponse>.NotFound("Financial adjustment was not found.");
        }

        return ServiceResult<FinancialAdjustmentResponse>.Success(ToAdjustmentResponse(adjustment));
    }

    public async Task<ServiceResult<FinancialAdjustmentResponse>> CreateAdjustmentAsync(
        Guid? currentUserId,
        CreateFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialAdjustmentResponse>.Forbidden("Current user is required.");
        }

        var validation = ValidateCreateAdjustmentRequest(request);
        if (validation is not null)
        {
            return ServiceResult<FinancialAdjustmentResponse>.BadRequest(validation);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<FinancialAdjustmentResponse>.NotFound("Resident was not found.");
        }

        var resident = await dbContext.ResidentProfiles
            .FirstOrDefaultAsync(item => item.Id == request.ResidentProfileId, cancellationToken);
        if (resident is null || resident.CompoundId != request.CompoundId)
        {
            return ServiceResult<FinancialAdjustmentResponse>.NotFound("Resident was not found.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var adjustment = new FinancialAdjustment
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = resident.Id,
            AdjustmentType = request.AdjustmentType,
            Status = FinancialAdjustmentStatus.PendingApproval,
            Amount = request.Amount,
            Currency = NormalizeCurrency(request.Currency),
            Reason = request.Reason.Trim(),
            RequestedByUserId = currentUserId.Value,
            CreatedAtUtc = now
        };

        var approval = new ApprovalRequest
        {
            CompoundId = request.CompoundId,
            RequestedByUserId = currentUserId.Value,
            ActionType = ApprovalActionType.ManualFinancialCorrection,
            EntityType = ApprovalEntityType.ResidentProfile,
            EntityId = resident.Id,
            Status = ApprovalStatus.Pending,
            Priority = request.Amount >= 1_000_000m ? ApprovalPriority.High : ApprovalPriority.Normal,
            ExecutionStatus = ApprovalExecutionStatus.NotReady,
            Reason = $"Manual financial {request.AdjustmentType} adjustment for resident {resident.FullName}.",
            RequestPayloadJson = JsonSerializer.Serialize(new
            {
                adjustment.Id,
                request.CompoundId,
                request.ResidentProfileId,
                request.AdjustmentType,
                request.Amount,
                Currency = adjustment.Currency,
                request.Reason
            }),
            CreatedAtUtc = now,
            DueAtUtc = now.AddHours(48)
        };

        adjustment.ApprovalRequestId = approval.Id;
        dbContext.ApprovalRequests.Add(approval);
        dbContext.FinancialAdjustments.Add(adjustment);
        AddActivityEvent(
            adjustment,
            currentUserId.Value,
            ActivityEventType.FinancialAdjustmentRequested,
            "Financial adjustment requested",
            $"A {adjustment.AdjustmentType} adjustment for {adjustment.Amount:N2} {adjustment.Currency} was requested.");
        AddNotification(
            adjustment,
            currentUserId.Value,
            NotificationEventType.FinancialAdjustmentRequested,
            "Financial adjustment approval required",
            $"A {adjustment.AdjustmentType} adjustment for {resident.FullName} requires approval.");
        await AddFinancialAdjustmentAuditAsync(
            adjustment,
            currentUserId.Value,
            AuditActionType.FinancialAdjustmentRequested,
            AuditSeverity.High,
            "Financial adjustment requested",
            $"Manual {adjustment.AdjustmentType} adjustment requested.",
            [
                new AuditLogChangeRecord(nameof(FinancialAdjustment.Status), null, FinancialAdjustmentStatus.PendingApproval.ToString()),
                new AuditLogChangeRecord(nameof(FinancialAdjustment.Amount), null, adjustment.Amount.ToString("F2")),
                new AuditLogChangeRecord(nameof(FinancialAdjustment.Reason), null, adjustment.Reason)
            ],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        adjustment.ResidentProfile = resident;
        return ServiceResult<FinancialAdjustmentResponse>.Success(ToAdjustmentResponse(adjustment));
    }

    public async Task<ServiceResult<FinancialAdjustmentResponse>> ApplyAdjustmentAsync(
        Guid? currentUserId,
        Guid id,
        ApplyFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialAdjustmentResponse>.Forbidden("Current user is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var adjustment = await GetScopedAdjustmentQuery(scope, asNoTracking: false)
            .Include(item => item.ApprovalRequest)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return ServiceResult<FinancialAdjustmentResponse>.NotFound("Financial adjustment was not found.");
        }

        if (adjustment.Status != FinancialAdjustmentStatus.PendingApproval)
        {
            return ServiceResult<FinancialAdjustmentResponse>.Conflict("Only pending financial adjustments can be applied.");
        }

        if (adjustment.ApprovalRequest is null
            || adjustment.ApprovalRequest.Status != ApprovalStatus.Approved
            || adjustment.ApprovalRequest.ExecutionStatus != ApprovalExecutionStatus.ReadyForExecution)
        {
            return ServiceResult<FinancialAdjustmentResponse>.Conflict("Financial adjustment cannot be applied before approval is granted.");
        }

        var now = DateTime.UtcNow;
        adjustment.Status = FinancialAdjustmentStatus.Applied;
        adjustment.AppliedByUserId = currentUserId.Value;
        adjustment.AppliedAtUtc = now;
        adjustment.UpdatedAtUtc = now;

        adjustment.ApprovalRequest.ExecutionStatus = ApprovalExecutionStatus.Executed;
        adjustment.ApprovalRequest.ExecutedAtUtc = now;
        adjustment.ApprovalRequest.ExecutedByUserId = currentUserId.Value;
        adjustment.ApprovalRequest.ExecutionNotes = string.IsNullOrWhiteSpace(request.Notes)
            ? "Financial adjustment applied."
            : request.Notes.Trim();
        adjustment.ApprovalRequest.UpdatedAtUtc = now;

        var direction = adjustment.AdjustmentType == FinancialAdjustmentType.Debit
            ? FinancialLedgerEntryDirection.Debit
            : FinancialLedgerEntryDirection.Credit;

        var ledgerEntry = new ResidentLedgerEntry
        {
            CompoundId = adjustment.CompoundId,
            ResidentProfileId = adjustment.ResidentProfileId,
            FinancialAdjustmentId = adjustment.Id,
            Direction = direction,
            SourceType = FinancialLedgerSourceType.FinancialAdjustment,
            SourceId = adjustment.Id,
            Amount = adjustment.Amount,
            Currency = adjustment.Currency,
            Reference = $"ADJ-{adjustment.Id.ToString("N")[..12].ToUpperInvariant()}",
            Description = adjustment.Reason,
            OccurredAtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = currentUserId.Value
        };
        dbContext.ResidentLedgerEntries.Add(ledgerEntry);

        AddActivityEvent(
            adjustment,
            currentUserId.Value,
            ActivityEventType.FinancialAdjustmentApplied,
            "Financial adjustment applied",
            $"Financial adjustment {ledgerEntry.Reference} was applied.");
        AddNotification(
            adjustment,
            currentUserId.Value,
            NotificationEventType.FinancialAdjustmentApplied,
            "Financial adjustment applied",
            $"A {adjustment.AdjustmentType} financial adjustment was applied.");
        await AddFinancialAdjustmentAuditAsync(
            adjustment,
            currentUserId.Value,
            AuditActionType.FinancialAdjustmentApplied,
            AuditSeverity.Critical,
            "Financial adjustment applied",
            $"Financial adjustment {ledgerEntry.Reference} was applied and ledger entry was created.",
            [
                new AuditLogChangeRecord(nameof(FinancialAdjustment.Status), FinancialAdjustmentStatus.PendingApproval.ToString(), FinancialAdjustmentStatus.Applied.ToString()),
                new AuditLogChangeRecord(nameof(ResidentLedgerEntry.Direction), null, ledgerEntry.Direction.ToString()),
                new AuditLogChangeRecord(nameof(ResidentLedgerEntry.Amount), null, ledgerEntry.Amount.ToString("F2"))
            ],
            cancellationToken);
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            adjustment.CompoundId,
            adjustment.ResidentProfileId,
            currentUserId.Value,
            RoleNames.FinancialAdjustmentManagers,
            AuditActionType.LedgerEntryCreated,
            AuditEntityType.ResidentLedgerEntry,
            ledgerEntry.Id,
            AuditSeverity.High,
            "Finance",
            "Resident ledger entry created from approved financial adjustment.",
            adjustment.Reason,
            AfterValuesJson: JsonSerializer.Serialize(new
            {
                ledgerEntry.Id,
                ledgerEntry.Direction,
                ledgerEntry.Amount,
                ledgerEntry.Currency,
                ledgerEntry.Reference,
                ledgerEntry.SourceType,
                ledgerEntry.SourceId
            })), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<FinancialAdjustmentResponse>.Success(ToAdjustmentResponse(adjustment));
    }

    public async Task<ServiceResult<FinancialAdjustmentResponse>> CancelAdjustmentAsync(
        Guid? currentUserId,
        Guid id,
        CancelFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialAdjustmentResponse>.Forbidden("Current user is required.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is null)
        {
            return ServiceResult<FinancialAdjustmentResponse>.BadRequest("Cancellation reason is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var adjustment = await GetScopedAdjustmentQuery(scope, asNoTracking: false)
            .Include(item => item.ApprovalRequest)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return ServiceResult<FinancialAdjustmentResponse>.NotFound("Financial adjustment was not found.");
        }

        if (adjustment.Status != FinancialAdjustmentStatus.PendingApproval)
        {
            return ServiceResult<FinancialAdjustmentResponse>.Conflict("Only pending financial adjustments can be cancelled.");
        }

        var now = DateTime.UtcNow;
        adjustment.Status = FinancialAdjustmentStatus.Cancelled;
        adjustment.CancelledByUserId = currentUserId.Value;
        adjustment.CancelledAtUtc = now;
        adjustment.CancellationReason = reason;
        adjustment.UpdatedAtUtc = now;

        if (adjustment.ApprovalRequest is not null && adjustment.ApprovalRequest.Status == ApprovalStatus.Pending)
        {
            adjustment.ApprovalRequest.Status = ApprovalStatus.Cancelled;
            adjustment.ApprovalRequest.CancelledAtUtc = now;
            adjustment.ApprovalRequest.DecisionReason = reason;
            adjustment.ApprovalRequest.UpdatedAtUtc = now;
        }

        AddActivityEvent(
            adjustment,
            currentUserId.Value,
            ActivityEventType.FinancialAdjustmentCancelled,
            "Financial adjustment cancelled",
            reason);
        AddNotification(
            adjustment,
            currentUserId.Value,
            NotificationEventType.FinancialAdjustmentCancelled,
            "Financial adjustment cancelled",
            reason);
        await AddFinancialAdjustmentAuditAsync(
            adjustment,
            currentUserId.Value,
            AuditActionType.FinancialAdjustmentCancelled,
            AuditSeverity.High,
            "Financial adjustment cancelled",
            reason,
            [new AuditLogChangeRecord(nameof(FinancialAdjustment.Status), FinancialAdjustmentStatus.PendingApproval.ToString(), FinancialAdjustmentStatus.Cancelled.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<FinancialAdjustmentResponse>.Success(ToAdjustmentResponse(adjustment));
    }

    public async Task<ServiceResult<FinancialAgingReportResponse>> GetAgingReportAsync(
        FinancialAgingReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = query.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<FinancialAgingReportResponse>.Forbidden("Current user cannot access aging report.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<FinancialAgingReportResponse>.NotFound("Aging report was not found.");
        }

        var outstandingItems = await LoadOutstandingItemsAsync(scope, query.CompoundId, asOfDate, cancellationToken);
        var buckets = outstandingItems
            .GroupBy(item => GetAgingBucket(item.DueDate, asOfDate))
            .Select(group => new FinancialAgingBucketResponse(group.Key, group.Count(), group.Sum(item => item.Amount)))
            .ToDictionary(item => item.Bucket, item => item);

        var current = buckets.GetValueOrDefault("Current") ?? new FinancialAgingBucketResponse("Current", 0, 0m);
        var days1To30 = buckets.GetValueOrDefault("1-30") ?? new FinancialAgingBucketResponse("1-30", 0, 0m);
        var days31To60 = buckets.GetValueOrDefault("31-60") ?? new FinancialAgingBucketResponse("31-60", 0, 0m);
        var days61To90 = buckets.GetValueOrDefault("61-90") ?? new FinancialAgingBucketResponse("61-90", 0, 0m);
        var over90 = buckets.GetValueOrDefault("90+") ?? new FinancialAgingBucketResponse("90+", 0, 0m);

        return ServiceResult<FinancialAgingReportResponse>.Success(new FinancialAgingReportResponse(
            query.CompoundId,
            asOfDate,
            current.Amount,
            days1To30.Amount,
            days31To60.Amount,
            days61To90.Amount,
            over90.Amount,
            outstandingItems.Sum(item => item.Amount),
            [current, days1To30, days31To60, days61To90, over90]));
    }


    public async Task<ServiceResult<FinancialAgingRiskReportResponse>> GetAgingRiskReportAsync(
        FinancialAgingRiskReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.MinimumOverdueDays < 0)
        {
            return ServiceResult<FinancialAgingRiskReportResponse>.BadRequest("Minimum overdue days cannot be negative.");
        }

        var asOfDate = query.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<FinancialAgingRiskReportResponse>.Forbidden("Current user cannot access aging risk report.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<FinancialAgingRiskReportResponse>.NotFound("Aging risk report was not found.");
        }

        var outstandingItems = (await LoadOutstandingItemsAsync(scope, query.CompoundId, asOfDate, cancellationToken))
            .Where(item => item.ResidentProfileId.HasValue && item.Amount > 0)
            .Where(item => GetOverdueDays(item.DueDate, asOfDate) >= query.MinimumOverdueDays)
            .ToArray();

        if (outstandingItems.Length == 0)
        {
            return ServiceResult<FinancialAgingRiskReportResponse>.Success(new FinancialAgingRiskReportResponse(
                query.CompoundId,
                asOfDate,
                0,
                0,
                0,
                0,
                0m,
                0m,
                0m,
                0m,
                []));
        }

        var residentIds = outstandingItems
            .Select(item => item.ResidentProfileId!.Value)
            .Distinct()
            .ToArray();

        var residentNames = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(resident => residentIds.Contains(resident.Id))
            .Select(resident => new { resident.Id, resident.FullName })
            .ToDictionaryAsync(resident => resident.Id, resident => resident.FullName, cancellationToken);

        var activeDisputes = await ApplyOptionalCompoundFilter(
                dbContext.FinancialDisputes.AsNoTracking().ApplyCompoundAccess(scope, dispute => dispute.CompoundId),
                query.CompoundId)
            .Where(dispute => dispute.Status != FinancialDisputeStatus.Resolved
                && dispute.Status != FinancialDisputeStatus.Cancelled)
            .Select(dispute => new
            {
                dispute.Id,
                dispute.ResidentProfileId,
                dispute.TargetType,
                dispute.TargetId,
                dispute.Status,
                dispute.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestDisputeByTarget = activeDisputes
            .GroupBy(dispute => (dispute.TargetType, dispute.TargetId))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(dispute => dispute.CreatedAtUtc).First());

        var pauseRuleTargets = await ApplyOptionalCompoundFilter(
                dbContext.PenaltyRules.AsNoTracking().ApplyCompoundAccess(scope, rule => rule.CompoundId),
                query.CompoundId)
            .Where(rule => rule.Status == PenaltyRuleStatus.Active && rule.PauseWhenDisputed)
            .Where(rule => !rule.EffectiveFrom.HasValue || rule.EffectiveFrom.Value <= asOfDate)
            .Where(rule => !rule.EffectiveUntil.HasValue || rule.EffectiveUntil.Value >= asOfDate)
            .Select(rule => rule.TargetType)
            .Distinct()
            .ToListAsync(cancellationToken);

        var pauseRuleTargetSet = pauseRuleTargets.ToHashSet();
        var itemResponses = outstandingItems.Select(item =>
        {
            var targetType = ToDisputeTargetType(item.SourceType);
            var penaltyRuleTargetType = ToPenaltyRuleTargetType(item.SourceType);
            var dispute = targetType.HasValue
                && latestDisputeByTarget.TryGetValue((targetType.Value, item.SourceId), out var foundDispute)
                    ? foundDispute
                    : null;
            var hasDispute = dispute is not null;
            var daysOverdue = GetOverdueDays(item.DueDate, asOfDate);
            var isOverdue = item.DueDate < asOfDate;
            var pauseRecommended = isOverdue
                && hasDispute
                && penaltyRuleTargetType.HasValue
                && pauseRuleTargetSet.Contains(penaltyRuleTargetType.Value);

            return new AgingRiskItemSeed(
                item.ResidentProfileId!.Value,
                new FinancialAgingRiskItemResponse(
                    item.SourceType,
                    item.SourceId,
                    item.DueDate,
                    daysOverdue,
                    item.Amount,
                    isOverdue,
                    hasDispute,
                    dispute?.Id,
                    dispute?.Status,
                    pauseRecommended,
                    GetAgingRiskItemAction(daysOverdue, hasDispute, pauseRecommended)));
        }).ToArray();

        var residents = itemResponses
            .GroupBy(item => item.ResidentProfileId)
            .Select(group =>
            {
                var items = group.Select(item => item.Item).OrderByDescending(item => item.DaysOverdue).ThenByDescending(item => item.Amount).ToArray();
                var outstandingAmount = items.Sum(item => item.Amount);
                var overdueAmount = items.Where(item => item.IsOverdue).Sum(item => item.Amount);
                var underReviewAmount = items.Where(item => item.HasActiveFinancialDispute).Sum(item => item.Amount);
                var pauseAmount = items.Where(item => item.PenaltyPauseRecommended).Sum(item => item.Amount);
                var oldestDueDate = items.Where(item => item.IsOverdue).Select(item => (DateOnly?)item.DueDate).Min();
                var oldestOverdueDays = items.Length == 0 ? 0 : items.Max(item => item.DaysOverdue);
                var activeDisputeCount = items.Count(item => item.HasActiveFinancialDispute);
                var isHighRisk = oldestOverdueDays >= 60 || overdueAmount >= 1_000_000m || pauseAmount > 0m;

                return new FinancialAgingRiskResidentResponse(
                    group.Key,
                    residentNames.GetValueOrDefault(group.Key, "Unknown resident"),
                    outstandingAmount,
                    overdueAmount,
                    underReviewAmount,
                    pauseAmount,
                    oldestDueDate,
                    oldestOverdueDays,
                    activeDisputeCount,
                    isHighRisk,
                    GetAgingRiskResidentAction(oldestOverdueDays, activeDisputeCount, pauseAmount),
                    items);
            })
            .OrderByDescending(resident => resident.IsHighRisk)
            .ThenByDescending(resident => resident.OldestOverdueDays)
            .ThenByDescending(resident => resident.OutstandingAmount)
            .ToArray();

        var allItems = residents.SelectMany(resident => resident.Items).ToArray();
        var response = new FinancialAgingRiskReportResponse(
            query.CompoundId,
            asOfDate,
            residents.Length,
            residents.Count(resident => resident.IsHighRisk),
            allItems.Length,
            allItems.Count(item => item.IsOverdue),
            allItems.Sum(item => item.Amount),
            allItems.Where(item => item.IsOverdue).Sum(item => item.Amount),
            allItems.Where(item => item.HasActiveFinancialDispute).Sum(item => item.Amount),
            allItems.Where(item => item.PenaltyPauseRecommended).Sum(item => item.Amount),
            residents);

        return ServiceResult<FinancialAgingRiskReportResponse>.Success(response);
    }

    public async Task<ServiceResult<FinancialClosureSummaryResponse>> GetFinancialClosureSummaryAsync(
        FinancialClosureSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ReconciliationLookbackDays is < 1 or > 365)
        {
            return ServiceResult<FinancialClosureSummaryResponse>.BadRequest("Reconciliation lookback days must be between 1 and 365.");
        }

        if (query.MinimumOverdueDays < 0)
        {
            return ServiceResult<FinancialClosureSummaryResponse>.BadRequest("Minimum overdue days cannot be negative.");
        }

        var asOfDate = query.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var reconciliationFromDate = asOfDate.AddDays(-query.ReconciliationLookbackDays);
        var now = DateTime.UtcNow;

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<FinancialClosureSummaryResponse>.Forbidden("Current user cannot access financial closure summary.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<FinancialClosureSummaryResponse>.NotFound("Financial closure summary was not found.");
        }

        var actionItems = new List<FinancialClosureActionItemResponse>();

        var reconciliationBatches = await ApplyOptionalCompoundFilter(
                dbContext.PaymentReconciliationBatches
                    .AsNoTracking()
                    .Include(batch => batch.Items)
                    .ApplyCompoundAccess(scope, batch => batch.CompoundId),
                query.CompoundId)
            .Where(batch => batch.StatementDate >= reconciliationFromDate && batch.StatementDate <= asOfDate)
            .OrderByDescending(batch => batch.StatementDate)
            .ThenByDescending(batch => batch.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var reconciliationIssueItems = reconciliationBatches
            .SelectMany(batch => batch.Items.Select(item => new { Batch = batch, Item = item }))
            .Where(row => row.Item.MatchStatus != PaymentReconciliationItemStatus.Matched)
            .ToArray();
        var unreviewedReconciliationItems = reconciliationIssueItems
            .Where(row => row.Item.ReviewDecision == PaymentReconciliationReviewDecision.None)
            .ToArray();

        actionItems.AddRange(unreviewedReconciliationItems
            .OrderByDescending(row => Math.Abs(row.Item.DifferenceAmount ?? 0m))
            .Take(10)
            .Select(row => new FinancialClosureActionItemResponse(
                "Payment Reconciliation",
                row.Item.MatchStatus is PaymentReconciliationItemStatus.CompoundMismatch or PaymentReconciliationItemStatus.LedgerEntryMissing
                    ? "High"
                    : "Medium",
                null,
                null,
                row.Item.Id,
                row.Item.MatchStatus.ToString(),
                row.Item.DifferenceAmount is null ? row.Item.ProviderAmount : Math.Abs(row.Item.DifferenceAmount.Value),
                Math.Max(0, asOfDate.DayNumber - row.Batch.StatementDate.DayNumber),
                $"Review provider transaction {row.Item.ProviderTransactionId} in statement {row.Batch.StatementReference}.")));

        var outstandingItems = (await LoadOutstandingItemsAsync(scope, query.CompoundId, asOfDate, cancellationToken))
            .Where(item => item.ResidentProfileId.HasValue && item.Amount > 0m)
            .Where(item => GetOverdueDays(item.DueDate, asOfDate) >= query.MinimumOverdueDays)
            .ToArray();

        var residentIds = outstandingItems
            .Select(item => item.ResidentProfileId!.Value)
            .Distinct()
            .ToArray();

        var residentNames = residentIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ResidentProfiles
                .AsNoTracking()
                .Where(resident => residentIds.Contains(resident.Id))
                .Select(resident => new { resident.Id, resident.FullName })
                .ToDictionaryAsync(resident => resident.Id, resident => resident.FullName, cancellationToken);

        var activeDisputes = await ApplyOptionalCompoundFilter(
                dbContext.FinancialDisputes.AsNoTracking().ApplyCompoundAccess(scope, dispute => dispute.CompoundId),
                query.CompoundId)
            .Where(dispute => dispute.Status != FinancialDisputeStatus.Resolved
                && dispute.Status != FinancialDisputeStatus.Cancelled
                && dispute.Status != FinancialDisputeStatus.Rejected)
            .Select(dispute => new
            {
                dispute.Id,
                dispute.ResidentProfileId,
                dispute.TargetType,
                dispute.TargetId,
                dispute.Status,
                dispute.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestDisputeByTarget = activeDisputes
            .GroupBy(dispute => (dispute.TargetType, dispute.TargetId))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(dispute => dispute.CreatedAtUtc).First());

        var pauseRuleTargets = await ApplyOptionalCompoundFilter(
                dbContext.PenaltyRules.AsNoTracking().ApplyCompoundAccess(scope, rule => rule.CompoundId),
                query.CompoundId)
            .Where(rule => rule.Status == PenaltyRuleStatus.Active && rule.PauseWhenDisputed)
            .Where(rule => !rule.EffectiveFrom.HasValue || rule.EffectiveFrom.Value <= asOfDate)
            .Where(rule => !rule.EffectiveUntil.HasValue || rule.EffectiveUntil.Value >= asOfDate)
            .Select(rule => rule.TargetType)
            .Distinct()
            .ToListAsync(cancellationToken);
        var pauseRuleTargetSet = pauseRuleTargets.ToHashSet();

        var closureAgingItems = outstandingItems.Select(item =>
        {
            var targetType = ToDisputeTargetType(item.SourceType);
            var penaltyRuleTargetType = ToPenaltyRuleTargetType(item.SourceType);
            var dispute = targetType.HasValue
                && latestDisputeByTarget.TryGetValue((targetType.Value, item.SourceId), out var foundDispute)
                    ? foundDispute
                    : null;
            var daysOverdue = GetOverdueDays(item.DueDate, asOfDate);
            var isOverdue = item.DueDate < asOfDate;
            var pauseRecommended = isOverdue
                && dispute is not null
                && penaltyRuleTargetType.HasValue
                && pauseRuleTargetSet.Contains(penaltyRuleTargetType.Value);

            return new FinancialClosureAgingItem(
                item.ResidentProfileId!.Value,
                item.SourceType,
                item.SourceId,
                item.Amount,
                daysOverdue,
                isOverdue,
                dispute is not null,
                pauseRecommended);
        }).ToArray();

        var agingResidents = closureAgingItems
            .GroupBy(item => item.ResidentProfileId)
            .Select(group =>
            {
                var items = group.ToArray();
                var overdueAmount = items.Where(item => item.IsOverdue).Sum(item => item.Amount);
                var underReviewAmount = items.Where(item => item.HasActiveFinancialDispute).Sum(item => item.Amount);
                var pauseAmount = items.Where(item => item.PenaltyPauseRecommended).Sum(item => item.Amount);
                var oldestOverdueDays = items.Length == 0 ? 0 : items.Max(item => item.DaysOverdue);
                var activeDisputeCount = items.Count(item => item.HasActiveFinancialDispute);
                var highRisk = oldestOverdueDays >= 60 || overdueAmount >= 1_000_000m || pauseAmount > 0m;

                return new FinancialClosureResidentRisk(
                    group.Key,
                    residentNames.GetValueOrDefault(group.Key, "Unknown resident"),
                    items.Sum(item => item.Amount),
                    overdueAmount,
                    underReviewAmount,
                    pauseAmount,
                    oldestOverdueDays,
                    activeDisputeCount,
                    highRisk,
                    GetAgingRiskResidentAction(oldestOverdueDays, activeDisputeCount, pauseAmount));
            })
            .OrderByDescending(resident => resident.IsHighRisk)
            .ThenByDescending(resident => resident.OldestOverdueDays)
            .ThenByDescending(resident => resident.OutstandingAmount)
            .ToArray();

        actionItems.AddRange(agingResidents
            .Where(resident => resident.IsHighRisk || resident.OverdueAmount > 0m || resident.UnderFinancialReviewAmount > 0m)
            .Take(10)
            .Select(resident => new FinancialClosureActionItemResponse(
                "Aging Risk",
                resident.IsHighRisk ? "High" : "Medium",
                resident.ResidentProfileId,
                resident.ResidentName,
                resident.ResidentProfileId,
                "ResidentFinancialAging",
                resident.OverdueAmount > 0m ? resident.OverdueAmount : resident.OutstandingAmount,
                resident.OldestOverdueDays,
                resident.RecommendedAction)));

        var collectionCases = await ApplyOptionalCompoundFilter(
                dbContext.CollectionCases
                    .AsNoTracking()
                    .Include(collectionCase => collectionCase.ResidentProfile)
                    .Include(collectionCase => collectionCase.PaymentPlans)
                        .ThenInclude(plan => plan.Installments)
                    .Include(collectionCase => collectionCase.LegalNotices)
                    .ApplyCompoundAccess(scope, collectionCase => collectionCase.CompoundId),
                query.CompoundId)
            .Where(collectionCase => collectionCase.Status != CollectionCaseStatus.Closed
                && collectionCase.Status != CollectionCaseStatus.Cancelled
                && collectionCase.Status != CollectionCaseStatus.Settled)
            .ToListAsync(cancellationToken);

        var collectionRisks = collectionCases.Select(collectionCase =>
        {
            var priority = DetermineFinancialClosureCollectionPriority(collectionCase, asOfDate, now);
            return new FinancialClosureCollectionRisk(
                collectionCase,
                priority,
                GetFinancialClosureCollectionAction(collectionCase, asOfDate, now));
        }).ToArray();

        actionItems.AddRange(collectionRisks
            .Where(item => item.Priority is "High" or "Medium")
            .OrderByDescending(item => FinancialClosureSeverityRank(item.Priority))
            .ThenByDescending(item => item.CollectionCase.AmountDue)
            .Take(10)
            .Select(item => new FinancialClosureActionItemResponse(
                "Collection Follow-up",
                item.Priority,
                item.CollectionCase.ResidentProfileId,
                item.CollectionCase.ResidentProfile.FullName,
                item.CollectionCase.Id,
                item.CollectionCase.Stage.ToString(),
                item.CollectionCase.AmountDue,
                item.CollectionCase.DueDate.HasValue ? Math.Max(0, asOfDate.DayNumber - item.CollectionCase.DueDate.Value.DayNumber) : null,
                item.RecommendedAction)));

        var orderedActions = actionItems
            .OrderByDescending(item => FinancialClosureSeverityRank(item.Severity))
            .ThenByDescending(item => item.AgeDays ?? 0)
            .ThenByDescending(item => item.Amount ?? 0m)
            .Take(50)
            .ToArray();

        var residentIdsRequiringAction = orderedActions
            .Where(item => item.ResidentProfileId.HasValue)
            .Select(item => item.ResidentProfileId!.Value)
            .Distinct()
            .Count();

        var response = new FinancialClosureSummaryResponse(
            query.CompoundId,
            asOfDate,
            reconciliationFromDate,
            reconciliationBatches.Count(batch => batch.Status == PaymentReconciliationBatchStatus.Open),
            reconciliationIssueItems.Length,
            unreviewedReconciliationItems.Length,
            reconciliationIssueItems.Sum(row => Math.Abs(row.Item.DifferenceAmount ?? 0m)),
            agingResidents.Length,
            agingResidents.Count(resident => resident.IsHighRisk),
            closureAgingItems.Where(item => item.IsOverdue).Sum(item => item.Amount),
            closureAgingItems.Where(item => item.HasActiveFinancialDispute).Sum(item => item.Amount),
            closureAgingItems.Where(item => item.PenaltyPauseRecommended).Sum(item => item.Amount),
            collectionRisks.Length,
            collectionRisks.Count(item => item.Priority == "High"),
            collectionRisks.Sum(item => item.CollectionCase.AmountDue),
            residentIdsRequiringAction,
            orderedActions);

        return ServiceResult<FinancialClosureSummaryResponse>.Success(response);
    }

    public async Task<ServiceResult<RevenueSummaryResponse>> GetRevenueSummaryAsync(
        RevenueSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateRange = NormalizeDateRange(query.FromDate, query.ToDate);
        if (dateRange.Error is not null)
        {
            return ServiceResult<RevenueSummaryResponse>.BadRequest(dateRange.Error);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<RevenueSummaryResponse>.Forbidden("Current user cannot access revenue summary.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<RevenueSummaryResponse>.NotFound("Revenue summary was not found.");
        }

        var paymentRows = await ApplyOptionalCompoundFilter(
                dbContext.Payments.AsNoTracking().ApplyCompoundAccess(scope, payment => payment.CompoundId),
                query.CompoundId)
            .Where(payment =>
                (payment.CompletedAt.HasValue
                    && payment.CompletedAt.Value >= dateRange.FromUtc
                    && payment.CompletedAt.Value < dateRange.ToExclusiveUtc
                    && (payment.PaymentStatus == PaymentStatus.Succeeded || payment.PaymentStatus == PaymentStatus.Refunded))
                || (payment.PaymentStatus == PaymentStatus.Refunded
                    && payment.RefundedAt.HasValue
                    && payment.RefundedAt.Value >= dateRange.FromUtc
                    && payment.RefundedAt.Value < dateRange.ToExclusiveUtc))
            .Select(payment => new
            {
                payment.PaymentMethod,
                payment.TargetType,
                payment.PaymentStatus,
                payment.Amount,
                payment.CompletedAt,
                payment.RefundedAt
            })
            .ToListAsync(cancellationToken);

        var collectedRows = paymentRows
            .Where(payment => payment.CompletedAt.HasValue
                && payment.CompletedAt.Value >= dateRange.FromUtc
                && payment.CompletedAt.Value < dateRange.ToExclusiveUtc)
            .ToArray();
        var refunded = paymentRows
            .Where(payment => payment.PaymentStatus == PaymentStatus.Refunded
                && payment.RefundedAt.HasValue
                && payment.RefundedAt.Value >= dateRange.FromUtc
                && payment.RefundedAt.Value < dateRange.ToExclusiveUtc)
            .Sum(payment => payment.Amount);
        var collected = collectedRows.Sum(payment => payment.Amount);

        var byMethod = collectedRows
            .GroupBy(payment => payment.PaymentMethod)
            .OrderBy(group => group.Key)
            .Select(group => new RevenueByPaymentMethodResponse(group.Key, group.Sum(item => item.Amount), group.Count()))
            .ToArray();

        var byTarget = collectedRows
            .GroupBy(payment => payment.TargetType)
            .OrderBy(group => group.Key)
            .Select(group => new RevenueByTargetTypeResponse(group.Key, group.Sum(item => item.Amount), group.Count()))
            .ToArray();

        return ServiceResult<RevenueSummaryResponse>.Success(new RevenueSummaryResponse(
            query.CompoundId,
            dateRange.FromDate,
            dateRange.ToDate,
            collected,
            refunded,
            collected - refunded,
            byMethod,
            byTarget));
    }

    private async Task<OutstandingSummary> CalculateOutstandingAsync(
        CompoundAccessScope scope,
        Guid? compoundId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var outstandingItems = await LoadOutstandingItemsAsync(scope, compoundId, asOfDate, cancellationToken);

        return new OutstandingSummary(
            outstandingItems.Sum(item => item.Amount),
            outstandingItems.Where(item => item.DueDate < asOfDate).Sum(item => item.Amount),
            outstandingItems.Where(item => item.SourceType == FinancialLedgerSourceType.UtilityBill).Sum(item => item.Amount),
            outstandingItems.Where(item => item.SourceType == FinancialLedgerSourceType.RentInvoice).Sum(item => item.Amount),
            outstandingItems.Where(item => item.SourceType == FinancialLedgerSourceType.PropertyInstallment).Sum(item => item.Amount),
            outstandingItems.Where(item => item.SourceType == FinancialLedgerSourceType.ViolationFine).Sum(item => item.Amount));
    }

    private async Task<RevenueSummary> CalculateRevenueAsync(
        CompoundAccessScope scope,
        Guid? compoundId,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var paymentRows = await ApplyOptionalCompoundFilter(
                dbContext.Payments.AsNoTracking().ApplyCompoundAccess(scope, payment => payment.CompoundId),
                compoundId)
            .Where(payment =>
                (payment.CompletedAt.HasValue
                    && payment.CompletedAt.Value >= fromUtc
                    && payment.CompletedAt.Value < toExclusiveUtc
                    && (payment.PaymentStatus == PaymentStatus.Succeeded || payment.PaymentStatus == PaymentStatus.Refunded))
                || (payment.PaymentStatus == PaymentStatus.Refunded
                    && payment.RefundedAt.HasValue
                    && payment.RefundedAt.Value >= fromUtc
                    && payment.RefundedAt.Value < toExclusiveUtc))
            .Select(payment => new { payment.PaymentStatus, payment.Amount, payment.CompletedAt, payment.RefundedAt })
            .ToListAsync(cancellationToken);

        var collected = paymentRows
            .Where(payment => payment.CompletedAt.HasValue
                && payment.CompletedAt.Value >= fromUtc
                && payment.CompletedAt.Value < toExclusiveUtc)
            .Sum(payment => payment.Amount);
        var refunded = paymentRows
            .Where(payment => payment.PaymentStatus == PaymentStatus.Refunded
                && payment.RefundedAt.HasValue
                && payment.RefundedAt.Value >= fromUtc
                && payment.RefundedAt.Value < toExclusiveUtc)
            .Sum(payment => payment.Amount);
        return new RevenueSummary(collected, refunded, collected - refunded);
    }

    private async Task<IReadOnlyList<OutstandingItem>> LoadOutstandingItemsAsync(
        CompoundAccessScope scope,
        Guid? compoundId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var utilityBills = await ApplyOptionalCompoundFilter(
                dbContext.UtilityBills.AsNoTracking().ApplyCompoundAccess(scope, bill => bill.CompoundId),
                compoundId)
            .Where(bill => bill.BillStatus != BillStatus.Cancelled && bill.BillStatus != BillStatus.Paid)
            .Select(bill => new OutstandingItem(
                FinancialLedgerSourceType.UtilityBill,
                bill.Id,
                bill.CompoundId,
                bill.ResidentProfileId,
                bill.DueDate,
                bill.TotalAmount - bill.PaidAmount))
            .ToListAsync(cancellationToken);

        var rentInvoices = await ApplyOptionalCompoundFilter(
                dbContext.RentInvoices.AsNoTracking().ApplyCompoundAccess(scope, invoice => invoice.CompoundId),
                compoundId)
            .Where(invoice => invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled && invoice.RentInvoiceStatus != RentInvoiceStatus.Paid)
            .Select(invoice => new OutstandingItem(
                FinancialLedgerSourceType.RentInvoice,
                invoice.Id,
                invoice.CompoundId,
                invoice.ResidentProfileId,
                invoice.DueDate,
                invoice.TotalAmount - invoice.PaidAmount))
            .ToListAsync(cancellationToken);

        var installments = await ApplyOptionalCompoundFilter(
                dbContext.InstallmentScheduleItems.AsNoTracking().ApplyCompoundAccess(scope, installment => installment.CompoundId),
                compoundId)
            .Where(installment => installment.InstallmentStatus != InstallmentStatus.Cancelled && installment.InstallmentStatus != InstallmentStatus.Paid)
            .Select(installment => new OutstandingItem(
                FinancialLedgerSourceType.PropertyInstallment,
                installment.Id,
                installment.CompoundId,
                installment.ResidentProfileId,
                installment.DueDate,
                installment.Amount - installment.PaidAmount))
            .ToListAsync(cancellationToken);

        var fines = await ApplyOptionalCompoundFilter(
                dbContext.ViolationFines.AsNoTracking().ApplyCompoundAccess(scope, fine => fine.CompoundId),
                compoundId)
            .Where(fine => fine.Status != ViolationFineStatus.Cancelled && fine.Status != ViolationFineStatus.Paid)
            .Select(fine => new OutstandingItem(
                FinancialLedgerSourceType.ViolationFine,
                fine.Id,
                fine.CompoundId,
                fine.ResidentProfileId,
                fine.DueDate,
                fine.Amount - fine.PaidAmount))
            .ToListAsync(cancellationToken);

        var debitAdjustmentRows = await ApplyOptionalCompoundFilter(
                dbContext.FinancialAdjustments.AsNoTracking().ApplyCompoundAccess(scope, adjustment => adjustment.CompoundId),
                compoundId)
            .Where(adjustment => adjustment.Status == FinancialAdjustmentStatus.Applied
                && adjustment.AdjustmentType == FinancialAdjustmentType.Debit)
            .Select(adjustment => new
            {
                adjustment.Id,
                adjustment.CompoundId,
                adjustment.ResidentProfileId,
                adjustment.AppliedAtUtc,
                adjustment.CreatedAtUtc,
                adjustment.Amount
            })
            .ToListAsync(cancellationToken);

        var debitAdjustments = debitAdjustmentRows
            .Select(adjustment => new OutstandingItem(
                FinancialLedgerSourceType.FinancialAdjustment,
                adjustment.Id,
                adjustment.CompoundId,
                adjustment.ResidentProfileId,
                DateOnly.FromDateTime(adjustment.AppliedAtUtc ?? adjustment.CreatedAtUtc),
                adjustment.Amount));

        return utilityBills
            .Concat(rentInvoices)
            .Concat(installments)
            .Concat(fines)
            .Concat(debitAdjustments)
            .Where(item => item.Amount > 0)
            .ToArray();
    }

    private async Task<IReadOnlyList<StatementLineSeed>> BuildResidentStatementLinesAsync(
        Guid residentProfileId,
        Guid compoundId,
        OptionalDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var lines = new List<StatementLineSeed>();

        var billRows = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(bill => bill.CompoundId == compoundId && bill.ResidentProfileId == residentProfileId)
            .Where(bill => bill.BillStatus != BillStatus.Cancelled)
            .Select(bill => new { bill.Id, bill.BillNumber, bill.TotalAmount, bill.IssueDate })
            .ToListAsync(cancellationToken);
        lines.AddRange(billRows.Select(bill => new StatementLineSeed(
            bill.IssueDate.ToDateTime(TimeOnly.MinValue),
            FinancialLedgerSourceType.UtilityBill,
            bill.Id,
            bill.BillNumber,
            "Utility bill issued",
            bill.TotalAmount,
            0m)));

        var rentRows = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(invoice => invoice.CompoundId == compoundId && invoice.ResidentProfileId == residentProfileId)
            .Where(invoice => invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .Select(invoice => new { invoice.Id, invoice.InvoiceNumber, invoice.TotalAmount, invoice.IssueDate })
            .ToListAsync(cancellationToken);
        lines.AddRange(rentRows.Select(invoice => new StatementLineSeed(
            invoice.IssueDate.ToDateTime(TimeOnly.MinValue),
            FinancialLedgerSourceType.RentInvoice,
            invoice.Id,
            invoice.InvoiceNumber,
            "Rent invoice issued",
            invoice.TotalAmount,
            0m)));

        var installmentRows = await dbContext.InstallmentScheduleItems
            .AsNoTracking()
            .Where(installment => installment.CompoundId == compoundId && installment.ResidentProfileId == residentProfileId)
            .Where(installment => installment.InstallmentStatus != InstallmentStatus.Cancelled)
            .Select(installment => new
            {
                installment.Id,
                installment.InstallmentNumber,
                installment.Amount,
                installment.DueDate,
                ContractNumber = installment.PropertySaleContract.ContractNumber
            })
            .ToListAsync(cancellationToken);
        lines.AddRange(installmentRows.Select(installment => new StatementLineSeed(
            installment.DueDate.ToDateTime(TimeOnly.MinValue),
            FinancialLedgerSourceType.PropertyInstallment,
            installment.Id,
            $"{installment.ContractNumber} / Installment {installment.InstallmentNumber}",
            "Property installment due",
            installment.Amount,
            0m)));

        var fineRows = await dbContext.ViolationFines
            .AsNoTracking()
            .Where(fine => fine.CompoundId == compoundId && fine.ResidentProfileId == residentProfileId)
            .Where(fine => fine.Status != ViolationFineStatus.Cancelled)
            .Select(fine => new { fine.Id, fine.Amount, fine.DueDate, fine.Reason })
            .ToListAsync(cancellationToken);
        lines.AddRange(fineRows.Select(fine => new StatementLineSeed(
            fine.DueDate.ToDateTime(TimeOnly.MinValue),
            FinancialLedgerSourceType.ViolationFine,
            fine.Id,
            $"FINE-{fine.Id.ToString("N")[..12].ToUpperInvariant()}",
            fine.Reason,
            fine.Amount,
            0m)));

        var paymentRows = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.CompoundId == compoundId && payment.ResidentProfileId == residentProfileId)
            .Where(payment => payment.PaymentStatus == PaymentStatus.Succeeded || payment.PaymentStatus == PaymentStatus.Refunded)
            .Select(payment => new
            {
                payment.Id,
                payment.PaymentReference,
                payment.PaymentStatus,
                payment.Amount,
                payment.CompletedAt,
                payment.RefundedAt
            })
            .ToListAsync(cancellationToken);
        foreach (var payment in paymentRows)
        {
            lines.Add(new StatementLineSeed(
                payment.CompletedAt ?? DateTime.UtcNow,
                FinancialLedgerSourceType.Payment,
                payment.Id,
                payment.PaymentReference,
                "Payment received",
                0m,
                payment.Amount));

            if (payment.PaymentStatus == PaymentStatus.Refunded)
            {
                lines.Add(new StatementLineSeed(
                    payment.RefundedAt ?? payment.CompletedAt ?? DateTime.UtcNow,
                    FinancialLedgerSourceType.Refund,
                    payment.Id,
                    payment.PaymentReference,
                    "Payment refunded",
                    payment.Amount,
                    0m));
            }
        }

        var ledgerRows = await dbContext.ResidentLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.CompoundId == compoundId && entry.ResidentProfileId == residentProfileId)
            .Select(entry => new
            {
                entry.OccurredAtUtc,
                entry.SourceType,
                entry.SourceId,
                entry.Reference,
                entry.Description,
                entry.Direction,
                entry.Amount
            })
            .ToListAsync(cancellationToken);

        var ledgerBackedPaymentSources = ledgerRows
            .Where(entry => entry.SourceType == FinancialLedgerSourceType.Payment
                || entry.SourceType == FinancialLedgerSourceType.Refund)
            .Select(entry => (entry.SourceType, entry.SourceId))
            .ToHashSet();
        if (ledgerBackedPaymentSources.Count > 0)
        {
            lines.RemoveAll(line => ledgerBackedPaymentSources.Contains((line.SourceType, line.SourceId)));
        }

        lines.AddRange(ledgerRows.Select(entry => new StatementLineSeed(
            entry.OccurredAtUtc,
            entry.SourceType,
            entry.SourceId,
            entry.Reference,
            entry.Description,
            entry.Direction == FinancialLedgerEntryDirection.Debit ? entry.Amount : 0m,
            entry.Direction == FinancialLedgerEntryDirection.Credit ? entry.Amount : 0m)));

        var dateScopedLines = lines
            .Where(line => IsWithinOptionalDateRange(line.OccurredAtUtc, dateRange))
            .ToArray();

        return await EnrichStatementLineGovernanceAsync(
            residentProfileId,
            compoundId,
            dateScopedLines,
            cancellationToken);
    }

    private async Task<IReadOnlyList<StatementLineSeed>> EnrichStatementLineGovernanceAsync(
        Guid residentProfileId,
        Guid compoundId,
        IReadOnlyCollection<StatementLineSeed> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var disputes = await dbContext.FinancialDisputes
            .AsNoTracking()
            .Where(dispute => dispute.CompoundId == compoundId && dispute.ResidentProfileId == residentProfileId)
            .Where(dispute => dispute.Status != FinancialDisputeStatus.Cancelled)
            .Select(dispute => new
            {
                dispute.Id,
                dispute.TargetType,
                dispute.TargetId,
                dispute.Status,
                dispute.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestDisputeBySource = disputes
            .Select(dispute => new
            {
                SourceType = ToLedgerSourceType(dispute.TargetType),
                dispute.TargetId,
                dispute.Id,
                dispute.Status,
                dispute.CreatedAtUtc
            })
            .Where(dispute => dispute.SourceType.HasValue)
            .GroupBy(dispute => (dispute.SourceType!.Value, dispute.TargetId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => IsClosedFinancialDisputeStatus(item.Status))
                    .ThenByDescending(item => item.CreatedAtUtc)
                    .First());

        var appeals = await dbContext.ViolationAppeals
            .AsNoTracking()
            .Where(appeal => appeal.CompoundId == compoundId && appeal.ResidentProfileId == residentProfileId)
            .Where(appeal => appeal.ViolationFineId.HasValue)
            .Where(appeal => appeal.Status != ViolationAppealStatus.Cancelled)
            .Select(appeal => new
            {
                appeal.Id,
                appeal.ViolationFineId,
                appeal.Status,
                appeal.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestAppealByFine = appeals
            .GroupBy(appeal => appeal.ViolationFineId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => IsClosedViolationAppealStatus(item.Status))
                    .ThenByDescending(item => item.CreatedAtUtc)
                    .First());

        var adjustmentIds = lines
            .Where(line => line.SourceType == FinancialLedgerSourceType.FinancialAdjustment)
            .Select(line => line.SourceId)
            .ToHashSet();

        var adjustmentStatusById = new Dictionary<Guid, FinancialAdjustmentStatus>();
        if (adjustmentIds.Count > 0)
        {
            var adjustments = await dbContext.FinancialAdjustments
                .AsNoTracking()
                .Where(adjustment => adjustment.CompoundId == compoundId
                    && adjustment.ResidentProfileId == residentProfileId
                    && adjustmentIds.Contains(adjustment.Id))
                .Select(adjustment => new
                {
                    adjustment.Id,
                    adjustment.Status
                })
                .ToListAsync(cancellationToken);

            adjustmentStatusById = adjustments.ToDictionary(adjustment => adjustment.Id, adjustment => adjustment.Status);
        }

        return lines.Select(line =>
        {
            latestDisputeBySource.TryGetValue((line.SourceType, line.SourceId), out var dispute);
            var appeal = line.SourceType == FinancialLedgerSourceType.ViolationFine
                && latestAppealByFine.TryGetValue(line.SourceId, out var foundAppeal)
                    ? foundAppeal
                    : null;
            var financialAdjustmentId = line.SourceType == FinancialLedgerSourceType.FinancialAdjustment
                ? line.SourceId
                : (Guid?)null;
            var financialAdjustmentStatus = financialAdjustmentId.HasValue
                && adjustmentStatusById.TryGetValue(financialAdjustmentId.Value, out var adjustmentStatus)
                    ? adjustmentStatus
                    : (FinancialAdjustmentStatus?)null;

            return line with
            {
                IsUnderFinancialReview = (dispute is not null && !IsClosedFinancialDisputeStatus(dispute.Status))
                    || (appeal is not null && !IsClosedViolationAppealStatus(appeal.Status)),
                FinancialDisputeId = dispute?.Id,
                FinancialDisputeStatus = dispute?.Status,
                ViolationAppealId = appeal?.Id,
                ViolationAppealStatus = appeal?.Status,
                FinancialAdjustmentId = financialAdjustmentId,
                FinancialAdjustmentStatus = financialAdjustmentStatus
            };
        }).ToArray();
    }

    private IQueryable<FinancialAdjustment> GetScopedAdjustmentQuery(CompoundAccessScope scope, bool asNoTracking)
    {
        var query = dbContext.FinancialAdjustments
            .Include(adjustment => adjustment.ResidentProfile)
            .ApplyCompoundAccess(scope, adjustment => adjustment.CompoundId);

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private static IQueryable<FinancialAdjustment> ApplyAdjustmentFilters(
        IQueryable<FinancialAdjustment> adjustments,
        FinancialAdjustmentSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            adjustments = adjustments.Where(adjustment => adjustment.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            adjustments = adjustments.Where(adjustment => adjustment.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.AdjustmentType.HasValue)
        {
            adjustments = adjustments.Where(adjustment => adjustment.AdjustmentType == query.AdjustmentType.Value);
        }

        if (query.Status.HasValue)
        {
            adjustments = adjustments.Where(adjustment => adjustment.Status == query.Status.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            adjustments = adjustments.Where(adjustment => adjustment.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            adjustments = adjustments.Where(adjustment => adjustment.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            adjustments = adjustments.Where(adjustment => adjustment.Reason.Contains(term));
        }

        return adjustments;
    }

    private static FinancialAdjustmentResponse ToAdjustmentResponse(FinancialAdjustment adjustment)
    {
        return new FinancialAdjustmentResponse(
            adjustment.Id,
            adjustment.CompoundId,
            adjustment.ResidentProfileId,
            adjustment.ResidentProfile.FullName,
            adjustment.AdjustmentType,
            adjustment.Status,
            adjustment.Amount,
            adjustment.Currency,
            adjustment.Reason,
            adjustment.RequestedByUserId,
            adjustment.ApprovalRequestId,
            adjustment.AppliedByUserId,
            adjustment.AppliedAtUtc,
            adjustment.CancelledByUserId,
            adjustment.CancelledAtUtc,
            adjustment.CancellationReason,
            adjustment.CreatedAtUtc,
            adjustment.UpdatedAtUtc);
    }


    private static FinancialDisputeTargetType? ToDisputeTargetType(FinancialLedgerSourceType sourceType)
    {
        return sourceType switch
        {
            FinancialLedgerSourceType.UtilityBill => FinancialDisputeTargetType.UtilityBill,
            FinancialLedgerSourceType.RentInvoice => FinancialDisputeTargetType.RentInvoice,
            FinancialLedgerSourceType.PropertyInstallment => FinancialDisputeTargetType.PropertyInstallment,
            FinancialLedgerSourceType.ViolationFine => FinancialDisputeTargetType.ViolationFine,
            FinancialLedgerSourceType.Payment => FinancialDisputeTargetType.Payment,
            FinancialLedgerSourceType.FinancialAdjustment => FinancialDisputeTargetType.FinancialAdjustment,
            _ => null
        };
    }

    private static PenaltyRuleTargetType? ToPenaltyRuleTargetType(FinancialLedgerSourceType sourceType)
    {
        return sourceType switch
        {
            FinancialLedgerSourceType.UtilityBill => PenaltyRuleTargetType.UtilityBill,
            FinancialLedgerSourceType.RentInvoice => PenaltyRuleTargetType.RentInvoice,
            FinancialLedgerSourceType.PropertyInstallment => PenaltyRuleTargetType.PropertyInstallment,
            FinancialLedgerSourceType.ViolationFine => PenaltyRuleTargetType.ViolationFine,
            _ => null
        };
    }

    private static int GetOverdueDays(DateOnly dueDate, DateOnly asOfDate)
    {
        return Math.Max(0, asOfDate.DayNumber - dueDate.DayNumber);
    }

    private static string GetAgingRiskItemAction(int daysOverdue, bool hasActiveDispute, bool penaltyPauseRecommended)
    {
        if (penaltyPauseRecommended)
        {
            return "Pause penalty and resolve the active financial dispute before escalation.";
        }

        if (hasActiveDispute)
        {
            return "Review the active financial dispute before collection action.";
        }

        return daysOverdue switch
        {
            >= 91 => "Escalate to collections/legal review.",
            >= 31 => "Advance the collection reminder workflow.",
            >= 1 => "Send an overdue payment reminder.",
            _ => "Monitor until the due date."
        };
    }

    private static string GetAgingRiskResidentAction(int oldestOverdueDays, int activeDisputeCount, decimal penaltyPauseRecommendedAmount)
    {
        if (penaltyPauseRecommendedAmount > 0)
        {
            return "Resolve disputed overdue items before applying penalties or legal escalation.";
        }

        if (activeDisputeCount > 0)
        {
            return "Review financial disputes before advancing collection action.";
        }

        return oldestOverdueDays switch
        {
            >= 91 => "Escalate resident account to collections/legal review.",
            >= 31 => "Advance resident to stronger payment reminder stage.",
            >= 1 => "Send resident an overdue payment reminder.",
            _ => "Monitor current outstanding balance."
        };
    }

    private static FinancialLedgerSourceType? ToLedgerSourceType(FinancialDisputeTargetType targetType)
    {
        return targetType switch
        {
            FinancialDisputeTargetType.UtilityBill => FinancialLedgerSourceType.UtilityBill,
            FinancialDisputeTargetType.Payment => FinancialLedgerSourceType.Payment,
            FinancialDisputeTargetType.ViolationFine => FinancialLedgerSourceType.ViolationFine,
            FinancialDisputeTargetType.RentInvoice => FinancialLedgerSourceType.RentInvoice,
            FinancialDisputeTargetType.PropertyInstallment => FinancialLedgerSourceType.PropertyInstallment,
            FinancialDisputeTargetType.FinancialAdjustment => FinancialLedgerSourceType.FinancialAdjustment,
            _ => null
        };
    }

    private static bool IsClosedFinancialDisputeStatus(FinancialDisputeStatus status)
    {
        return status is FinancialDisputeStatus.Resolved or FinancialDisputeStatus.Cancelled;
    }

    private static bool IsClosedViolationAppealStatus(ViolationAppealStatus status)
    {
        return status is ViolationAppealStatus.Rejected
            or ViolationAppealStatus.FineReduced
            or ViolationAppealStatus.FineCancelled
            or ViolationAppealStatus.Cancelled;
    }

    private static IQueryable<T> ApplyOptionalCompoundFilter<T>(IQueryable<T> query, Guid? compoundId)
        where T : class
    {
        return compoundId.HasValue
            ? query.Where(item => EF.Property<Guid>(item, "CompoundId") == compoundId.Value)
            : query;
    }

    private static string? ValidateCreateAdjustmentRequest(CreateFinancialAdjustmentRequest request)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return "Compound id is required.";
        }

        if (request.ResidentProfileId == Guid.Empty)
        {
            return "Resident profile id is required.";
        }

        if (request.Amount <= 0)
        {
            return "Adjustment amount must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "Adjustment reason is required.";
        }

        return null;
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        return normalized.Length == 3 ? normalized : DefaultCurrency;
    }

    private static string GetAgingBucket(DateOnly dueDate, DateOnly asOfDate)
    {
        var days = asOfDate.DayNumber - dueDate.DayNumber;
        if (days <= 0)
        {
            return "Current";
        }

        return days switch
        {
            <= 30 => "1-30",
            <= 60 => "31-60",
            <= 90 => "61-90",
            _ => "90+"
        };
    }

    private static bool IsWithinOptionalDateRange(DateTime date, OptionalDateRange range)
    {
        return (!range.FromUtc.HasValue || date >= range.FromUtc.Value)
            && (!range.ToExclusiveUtc.HasValue || date < range.ToExclusiveUtc.Value);
    }

    private static DateRange NormalizeDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = fromDate ?? new DateOnly(today.Year, today.Month, 1);
        var to = toDate ?? today;

        if (to < from)
        {
            return new DateRange(from, to, default, default, "To date cannot be earlier than from date.");
        }

        return new DateRange(
            from,
            to,
            from.ToDateTime(TimeOnly.MinValue),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            null);
    }

    private static OptionalDateRange NormalizeOptionalDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            return new OptionalDateRange(fromDate, toDate, null, null, "To date cannot be earlier than from date.");
        }

        return new OptionalDateRange(
            fromDate,
            toDate,
            fromDate?.ToDateTime(TimeOnly.MinValue),
            toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue),
            null);
    }

    private void AddActivityEvent(
        FinancialAdjustment adjustment,
        Guid actorUserId,
        ActivityEventType eventType,
        string title,
        string description)
    {
        dbContext.ActivityEvents.Add(new ActivityEvent
        {
            CompoundId = adjustment.CompoundId,
            ResidentProfileId = adjustment.ResidentProfileId,
            ActorUserId = actorUserId,
            EventType = eventType,
            Title = title,
            Description = description,
            EntityType = ActivityEntityType.FinancialAdjustment,
            EntityId = adjustment.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private void AddNotification(
        FinancialAdjustment adjustment,
        Guid actorUserId,
        NotificationEventType eventType,
        string subject,
        string body)
    {
        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            CompoundId = adjustment.CompoundId,
            ResidentProfileId = adjustment.ResidentProfileId,
            Channel = NotificationChannel.Email,
            EventType = eventType,
            Priority = NotificationPriority.High,
            RecipientName = "Finance Control Team",
            Subject = subject,
            Body = body,
            RelatedEntityType = NotificationRelatedEntityType.FinancialAdjustment,
            RelatedEntityId = adjustment.Id,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { adjustment.Id, adjustment.AdjustmentType, adjustment.Amount, adjustment.Currency })
        });
    }

    private async Task AddFinancialAdjustmentAuditAsync(
        FinancialAdjustment adjustment,
        Guid actorUserId,
        AuditActionType actionType,
        AuditSeverity severity,
        string description,
        string? reason,
        IReadOnlyCollection<AuditLogChangeRecord> changes,
        CancellationToken cancellationToken)
    {
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            adjustment.CompoundId,
            adjustment.ResidentProfileId,
            actorUserId,
            RoleNames.FinancialAdjustmentManagers,
            actionType,
            AuditEntityType.FinancialAdjustment,
            adjustment.Id,
            severity,
            "Finance",
            description,
            reason,
            AfterValuesJson: JsonSerializer.Serialize(new
            {
                adjustment.Id,
                adjustment.AdjustmentType,
                adjustment.Status,
                adjustment.Amount,
                adjustment.Currency,
                adjustment.ApprovalRequestId
            }),
            MetadataJson: JsonSerializer.Serialize(new
            {
                adjustment.CompoundId,
                adjustment.ResidentProfileId,
                adjustment.RequestedByUserId,
                adjustment.AppliedByUserId,
                adjustment.CancelledByUserId
            }),
            Changes: changes), cancellationToken);
    }


    private static string DetermineFinancialClosureCollectionPriority(
        CollectionCase collectionCase,
        DateOnly asOfDate,
        DateTime now)
    {
        var lastActionAt = collectionCase.LastActionAtUtc ?? collectionCase.OpenedAtUtc;
        var daysSinceLastAction = Math.Max(0, (int)Math.Floor((now - lastActionAt).TotalDays));
        var daysOverdue = collectionCase.DueDate.HasValue
            ? Math.Max(0, asOfDate.DayNumber - collectionCase.DueDate.Value.DayNumber)
            : (int?)null;

        var activePlans = collectionCase.PaymentPlans
            .Where(plan => plan.Status == PaymentPlanStatus.Active)
            .ToArray();

        var hasBrokenPlan = collectionCase.PaymentPlans.Any(plan => plan.Status == PaymentPlanStatus.Broken);
        var nextInstallment = activePlans
            .SelectMany(plan => plan.Installments)
            .Where(installment => installment.Status != PaymentPlanInstallmentStatus.Paid
                && installment.Status != PaymentPlanInstallmentStatus.Cancelled)
            .OrderBy(installment => installment.DueDate)
            .FirstOrDefault();

        var activeLegalNoticeCount = collectionCase.LegalNotices.Count(notice =>
            notice.Status == LegalNoticeStatus.Issued || notice.Status == LegalNoticeStatus.Delivered);

        if (collectionCase.Status == CollectionCaseStatus.LegalEscalated
            || hasBrokenPlan
            || activeLegalNoticeCount > 0
            || daysOverdue is >= 60
            || collectionCase.AmountDue >= 1_000_000m
            || (nextInstallment is not null && nextInstallment.DueDate < asOfDate))
        {
            return "High";
        }

        if (daysOverdue is >= 30
            || daysSinceLastAction >= 14
            || (nextInstallment is not null && nextInstallment.DueDate == asOfDate)
            || collectionCase.AmountDue >= 250_000m)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string GetFinancialClosureCollectionAction(
        CollectionCase collectionCase,
        DateOnly asOfDate,
        DateTime now)
    {
        _ = now;

        var activePlans = collectionCase.PaymentPlans
            .Where(plan => plan.Status == PaymentPlanStatus.Active)
            .ToArray();

        var hasActivePlan = activePlans.Length > 0;
        var hasBrokenPlan = collectionCase.PaymentPlans.Any(plan => plan.Status == PaymentPlanStatus.Broken);
        var nextInstallment = activePlans
            .SelectMany(plan => plan.Installments)
            .Where(installment => installment.Status != PaymentPlanInstallmentStatus.Paid
                && installment.Status != PaymentPlanInstallmentStatus.Cancelled)
            .OrderBy(installment => installment.DueDate)
            .FirstOrDefault();

        var activeLegalNoticeCount = collectionCase.LegalNotices.Count(notice =>
            notice.Status == LegalNoticeStatus.Issued || notice.Status == LegalNoticeStatus.Delivered);

        var daysOverdue = collectionCase.DueDate.HasValue
            ? Math.Max(0, asOfDate.DayNumber - collectionCase.DueDate.Value.DayNumber)
            : (int?)null;

        if (hasBrokenPlan)
        {
            return "Review the broken payment plan and decide whether to renegotiate, escalate, or issue a legal notice.";
        }

        if (nextInstallment is not null && nextInstallment.DueDate < asOfDate)
        {
            return "Contact the resident about the overdue payment plan installment before escalating the collection case.";
        }

        if (nextInstallment is not null && nextInstallment.DueDate == asOfDate)
        {
            return "Follow up today on the due payment plan installment.";
        }

        if (activeLegalNoticeCount > 0)
        {
            return "Track legal notice delivery and resident response before the next escalation.";
        }

        if (!hasActivePlan && daysOverdue is >= 30)
        {
            return "Offer a payment plan or advance the collection case to the next notice stage.";
        }

        if (collectionCase.Stage == CollectionStage.LegalReview || collectionCase.Status == CollectionCaseStatus.LegalEscalated)
        {
            return "Keep the case with legal review and record the next compliance action.";
        }

        return "Contact the resident, record the outcome, and schedule the next collection action.";
    }

    private static int FinancialClosureSeverityRank(string severity)
    {
        return severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            "Low" => 1,
            _ => 0
        };
    }

    private sealed record FinancialClosureAgingItem(
        Guid ResidentProfileId,
        FinancialLedgerSourceType SourceType,
        Guid SourceId,
        decimal Amount,
        int DaysOverdue,
        bool IsOverdue,
        bool HasActiveFinancialDispute,
        bool PenaltyPauseRecommended);

    private sealed record FinancialClosureResidentRisk(
        Guid ResidentProfileId,
        string ResidentName,
        decimal OutstandingAmount,
        decimal OverdueAmount,
        decimal UnderFinancialReviewAmount,
        decimal PenaltyPauseRecommendedAmount,
        int OldestOverdueDays,
        int ActiveFinancialDisputeCount,
        bool IsHighRisk,
        string RecommendedAction);

    private sealed record FinancialClosureCollectionRisk(
        CollectionCase CollectionCase,
        string Priority,
        string RecommendedAction);

    private sealed record OutstandingItem(
        FinancialLedgerSourceType SourceType,
        Guid SourceId,
        Guid CompoundId,
        Guid? ResidentProfileId,
        DateOnly DueDate,
        decimal Amount);


    private sealed record AgingRiskItemSeed(
        Guid ResidentProfileId,
        FinancialAgingRiskItemResponse Item);

    private sealed record OutstandingSummary(
        decimal TotalOutstanding,
        decimal TotalOverdue,
        decimal UtilityBillOutstanding,
        decimal RentInvoiceOutstanding,
        decimal InstallmentOutstanding,
        decimal ViolationFineOutstanding);

    private sealed record RevenueSummary(decimal Collected, decimal Refunded, decimal NetCollected);

    private sealed record StatementLineSeed(
        DateTime OccurredAtUtc,
        FinancialLedgerSourceType SourceType,
        Guid SourceId,
        string Reference,
        string Description,
        decimal DebitAmount,
        decimal CreditAmount)
    {
        public bool IsUnderFinancialReview { get; init; }

        public Guid? FinancialDisputeId { get; init; }

        public FinancialDisputeStatus? FinancialDisputeStatus { get; init; }

        public Guid? ViolationAppealId { get; init; }

        public ViolationAppealStatus? ViolationAppealStatus { get; init; }

        public Guid? FinancialAdjustmentId { get; init; }

        public FinancialAdjustmentStatus? FinancialAdjustmentStatus { get; init; }
    }

    private sealed record DateRange(
        DateOnly FromDate,
        DateOnly ToDate,
        DateTime FromUtc,
        DateTime ToExclusiveUtc,
        string? Error);

    private sealed record OptionalDateRange(
        DateOnly? FromDate,
        DateOnly? ToDate,
        DateTime? FromUtc,
        DateTime? ToExclusiveUtc,
        string? Error);
}
