using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class PaymentReconciliationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IPaymentReconciliationService
{
    public async Task<PagedResult<PaymentReconciliationBatchSummaryResponse>> SearchBatchesAsync(
        PaymentReconciliationBatchSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var batches = await ApplyCurrentCompoundScopeAsync(
            dbContext.PaymentReconciliationBatches
                .AsNoTracking()
                .Include(batch => batch.Compound)
                .Include(batch => batch.Items)
                .AsQueryable(),
            cancellationToken);

        batches = ApplyBatchFilters(batches, query);

        var totalCount = await batches.CountAsync(cancellationToken);
        var batchItems = await batches
            .OrderByDescending(batch => batch.CreatedAtUtc)
            .ThenBy(batch => batch.StatementReference)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = batchItems.Select(ToSummaryResponse).ToArray();

        return new PagedResult<PaymentReconciliationBatchSummaryResponse>(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<PaymentReconciliationBatchResponse>> GetBatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var batches = await ApplyCurrentCompoundScopeAsync(
            GetBatchDetailsQuery(asNoTracking: true),
            cancellationToken);

        var batch = await batches.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.NotFound(
                "Payment reconciliation batch was not found.");
        }

        return ServiceResult<PaymentReconciliationBatchResponse>.Success(ToResponse(batch));
    }

    public async Task<ServiceResult<PaymentReconciliationBatchResponse>> CreateBatchAsync(
        Guid? createdByUserId,
        CreatePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = await ValidateCreateRequestAsync(request, cancellationToken);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var provider = TrimRequired(request.Provider);
        var statementReference = TrimRequired(request.StatementReference);

        var batchExists = await dbContext.PaymentReconciliationBatches.AnyAsync(
            batch => batch.CompoundId == request.CompoundId
                && batch.Provider == provider
                && batch.StatementReference == statementReference,
            cancellationToken);
        if (batchExists)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.Conflict(
                "A reconciliation batch with the same compound, provider, and statement reference already exists.");
        }

        var batch = new PaymentReconciliationBatch
        {
            CompoundId = request.CompoundId,
            Provider = provider,
            StatementReference = statementReference,
            StatementDate = request.StatementDate,
            Notes = TrimOrNull(request.Notes),
            CreatedByUserId = createdByUserId
        };

        foreach (var requestItem in request.Items)
        {
            batch.Items.Add(await BuildReconciliationItemAsync(
                request.CompoundId,
                provider,
                requestItem,
                cancellationToken));
        }

        dbContext.PaymentReconciliationBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PaymentReconciliationBatchResponse>.Success(
            await GetCreatedBatchResponseAsync(batch.Id, cancellationToken));
    }

    public async Task<ServiceResult<PaymentReconciliationItemResponse>> ReviewItemAsync(
        Guid? reviewedByUserId,
        Guid batchId,
        Guid itemId,
        ReviewPaymentReconciliationItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Decision == PaymentReconciliationReviewDecision.None)
        {
            return ServiceResult<PaymentReconciliationItemResponse>.BadRequest(
                "A reconciliation review decision is required.");
        }

        var reviewNotes = TrimOrNull(request.ReviewNotes);
        if (reviewNotes is null)
        {
            return ServiceResult<PaymentReconciliationItemResponse>.BadRequest(
                "Review notes are required.");
        }

        var batches = await ApplyCurrentCompoundScopeAsync(
            GetBatchDetailsQuery(asNoTracking: false),
            cancellationToken);

        var batch = await batches.FirstOrDefaultAsync(item => item.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return ServiceResult<PaymentReconciliationItemResponse>.NotFound(
                "Payment reconciliation batch was not found.");
        }

        if (batch.Status == PaymentReconciliationBatchStatus.Closed)
        {
            return ServiceResult<PaymentReconciliationItemResponse>.Conflict(
                "Closed reconciliation batches cannot be reviewed or changed.");
        }

        var item = batch.Items.FirstOrDefault(batchItem => batchItem.Id == itemId);
        if (item is null)
        {
            return ServiceResult<PaymentReconciliationItemResponse>.NotFound(
                "Payment reconciliation item was not found.");
        }

        if (!RequiresReview(item))
        {
            return ServiceResult<PaymentReconciliationItemResponse>.BadRequest(
                "Matched reconciliation items do not require exception review.");
        }

        item.ReviewDecision = request.Decision;
        item.ReviewNotes = reviewNotes;
        item.ReviewedAtUtc = DateTime.UtcNow;
        item.ReviewedByUserId = reviewedByUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PaymentReconciliationItemResponse>.Success(ToItemResponse(item));
    }

    public async Task<ServiceResult<PaymentReconciliationBatchResponse>> CloseBatchAsync(
        Guid? closedByUserId,
        Guid id,
        ClosePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batches = await ApplyCurrentCompoundScopeAsync(
            GetBatchDetailsQuery(asNoTracking: false),
            cancellationToken);

        var batch = await batches.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.NotFound(
                "Payment reconciliation batch was not found.");
        }

        if (batch.Status == PaymentReconciliationBatchStatus.Closed)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.Conflict(
                "Payment reconciliation batch is already closed.");
        }

        var unreviewedIssueItems = batch.Items.Count(item => RequiresReview(item) && !IsReviewed(item));
        if (unreviewedIssueItems > 0)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.Conflict(
                "All reconciliation exception items must be reviewed before the batch can be closed.");
        }

        var unresolvedCorrectionItems = batch.Items.Count(
            item => RequiresReview(item)
                && IsReviewed(item)
                && !IsClosureSafeReviewDecision(item.ReviewDecision));
        if (unresolvedCorrectionItems > 0)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.Conflict(
                "Correction-required reconciliation items must be resolved before the batch can be closed.");
        }

        batch.Status = PaymentReconciliationBatchStatus.Closed;
        batch.ClosedAtUtc = DateTime.UtcNow;
        batch.ClosedByUserId = closedByUserId;

        var notes = TrimOrNull(request.Notes);
        if (notes is not null)
        {
            batch.Notes = notes;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PaymentReconciliationBatchResponse>.Success(ToResponse(batch));
    }

    private async Task<ServiceResult<PaymentReconciliationBatchResponse>?> ValidateCreateRequestAsync(
        CreatePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest("CompoundId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest("Provider is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StatementReference))
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest("Statement reference is required.");
        }

        if (request.StatementDate == default)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest("Statement date is required.");
        }

        if (request.Items.Count == 0)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest(
                "At least one provider transaction item is required.");
        }

        var duplicateProviderTransactionIds = request.Items
            .Select(item => TrimOrNull(item.ProviderTransactionId))
            .Where(item => item is not null)
            .GroupBy(item => item!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateProviderTransactionIds.Length > 0)
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest(
                "Duplicate provider transaction ids are not allowed inside the same reconciliation batch.",
                new Dictionary<string, string[]>
                {
                    [nameof(CreatePaymentReconciliationBatchRequest.Items)] = duplicateProviderTransactionIds
                });
        }

        if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProviderTransactionId)))
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest(
                "Provider transaction id is required for every reconciliation item.");
        }

        if (request.Items.Any(item => item.ProviderAmount <= 0))
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.BadRequest(
                "Provider amount must be greater than zero for every reconciliation item.");
        }

        if (!await dbContext.Compounds.AnyAsync(compound => compound.Id == request.CompoundId, cancellationToken))
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.NotFound("Compound was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<PaymentReconciliationBatchResponse>.Forbidden(
                "Current user cannot access the reconciliation compound.");
        }

        return null;
    }

    private async Task<PaymentReconciliationItem> BuildReconciliationItemAsync(
        Guid compoundId,
        string provider,
        CreatePaymentReconciliationItemRequest request,
        CancellationToken cancellationToken)
    {
        var providerTransactionId = TrimRequired(request.ProviderTransactionId);
        var attempt = await dbContext.PaymentAttempts
            .Include(item => item.Payment)
                .ThenInclude(payment => payment.Receipt)
            .FirstOrDefaultAsync(
                item => item.Provider == provider
                    && item.ProviderTransactionId == providerTransactionId,
                cancellationToken);

        if (attempt is null)
        {
            return CreateItem(
                request,
                providerTransactionId,
                PaymentReconciliationItemStatus.MissingInDarak,
                "Provider transaction was not found inside DARAK payment attempts.");
        }

        var payment = attempt.Payment;
        var differenceAmount = request.ProviderAmount - payment.Amount;
        if (payment.CompoundId != compoundId)
        {
            return CreateItem(
                request,
                providerTransactionId,
                PaymentReconciliationItemStatus.CompoundMismatch,
                "Provider transaction matched a DARAK payment outside the reconciliation compound.",
                matchedPayment: null,
                matchedPaymentAttempt: null,
                differenceAmount: differenceAmount);
        }

        if (attempt.AttemptStatus != request.ProviderStatus || payment.PaymentStatus != request.ProviderStatus)
        {
            return CreateItem(
                request,
                providerTransactionId,
                PaymentReconciliationItemStatus.StatusMismatch,
                "Provider transaction status does not match DARAK payment status.",
                payment,
                attempt,
                differenceAmount);
        }

        if (differenceAmount != 0m)
        {
            return CreateItem(
                request,
                providerTransactionId,
                PaymentReconciliationItemStatus.AmountMismatch,
                "Provider transaction amount does not match DARAK payment amount.",
                payment,
                attempt,
                differenceAmount);
        }

        if (payment.PaymentStatus == PaymentStatus.Succeeded && payment.Receipt is null)
        {
            return CreateItem(
                request,
                providerTransactionId,
                PaymentReconciliationItemStatus.ReceiptMissing,
                "Succeeded DARAK payment does not have a receipt.",
                payment,
                attempt,
                0m);
        }

        if (payment.PaymentStatus == PaymentStatus.Succeeded
            && payment.ResidentProfileId.HasValue
            && !await HasPaymentLedgerEntryAsync(payment.Id, cancellationToken))
        {
            return CreateItem(
                request,
                providerTransactionId,
                PaymentReconciliationItemStatus.LedgerEntryMissing,
                "Succeeded DARAK payment does not have a resident ledger entry.",
                payment,
                attempt,
                0m);
        }

        return CreateItem(
            request,
            providerTransactionId,
            PaymentReconciliationItemStatus.Matched,
            null,
            payment,
            attempt,
            0m);
    }

    private async Task<bool> HasPaymentLedgerEntryAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ResidentLedgerEntries.AnyAsync(
            entry => entry.SourceType == FinancialLedgerSourceType.Payment
                && entry.SourceId == paymentId,
            cancellationToken);
    }

    private static PaymentReconciliationItem CreateItem(
        CreatePaymentReconciliationItemRequest request,
        string providerTransactionId,
        PaymentReconciliationItemStatus status,
        string? issueReason,
        Payment? matchedPayment = null,
        PaymentAttempt? matchedPaymentAttempt = null,
        decimal? differenceAmount = null)
    {
        return new PaymentReconciliationItem
        {
            ProviderTransactionId = providerTransactionId,
            ProviderAmount = request.ProviderAmount,
            ProviderStatus = request.ProviderStatus,
            MatchedPaymentId = matchedPayment?.Id,
            MatchedPaymentAttemptId = matchedPaymentAttempt?.Id,
            MatchStatus = status,
            DifferenceAmount = differenceAmount,
            IssueReason = issueReason
        };
    }

    private async Task<PaymentReconciliationBatchResponse> GetCreatedBatchResponseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var batch = await GetBatchDetailsQuery(asNoTracking: true)
            .SingleAsync(item => item.Id == id, cancellationToken);

        return ToResponse(batch);
    }

    private IQueryable<PaymentReconciliationBatch> GetBatchDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.PaymentReconciliationBatches
            .Include(batch => batch.Compound)
            .Include(batch => batch.Items)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<IQueryable<PaymentReconciliationBatch>> ApplyCurrentCompoundScopeAsync(
        IQueryable<PaymentReconciliationBatch> batches,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return batches;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return batches.ApplyCompoundAccess(scope, batch => batch.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static IQueryable<PaymentReconciliationBatch> ApplyBatchFilters(
        IQueryable<PaymentReconciliationBatch> batches,
        PaymentReconciliationBatchSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            batches = batches.Where(batch => batch.CompoundId == query.CompoundId.Value);
        }

        var provider = TrimOrNull(query.Provider);
        if (provider is not null)
        {
            batches = batches.Where(batch => batch.Provider == provider);
        }

        if (query.Status.HasValue)
        {
            batches = batches.Where(batch => batch.Status == query.Status.Value);
        }

        if (query.StatementDateFrom.HasValue)
        {
            batches = batches.Where(batch => batch.StatementDate >= query.StatementDateFrom.Value);
        }

        if (query.StatementDateTo.HasValue)
        {
            batches = batches.Where(batch => batch.StatementDate <= query.StatementDateTo.Value);
        }

        return batches;
    }

    private static PaymentReconciliationBatchSummaryResponse ToSummaryResponse(
        PaymentReconciliationBatch batch)
    {
        var metrics = CalculateMetrics(batch.Items);

        return new PaymentReconciliationBatchSummaryResponse(
            batch.Id,
            batch.CompoundId,
            batch.Compound.Name,
            batch.Provider,
            batch.StatementReference,
            batch.StatementDate,
            batch.Status,
            metrics.TotalItems,
            metrics.MatchedItems,
            metrics.IssueItems,
            metrics.ReviewRequiredItems,
            metrics.ReviewedIssueItems,
            metrics.UnreviewedIssueItems,
            metrics.TotalDifferenceAmount,
            batch.Notes,
            batch.CreatedAtUtc,
            batch.CreatedByUserId,
            batch.ClosedAtUtc,
            batch.ClosedByUserId);
    }

    private static PaymentReconciliationBatchResponse ToResponse(PaymentReconciliationBatch batch)
    {
        var orderedEntities = batch.Items
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.ProviderTransactionId)
            .ToArray();
        var orderedItems = orderedEntities.Select(ToItemResponse).ToArray();
        var metrics = CalculateMetrics(orderedEntities);

        return new PaymentReconciliationBatchResponse(
            batch.Id,
            batch.CompoundId,
            batch.Compound.Name,
            batch.Provider,
            batch.StatementReference,
            batch.StatementDate,
            batch.Status,
            metrics.TotalItems,
            metrics.MatchedItems,
            metrics.IssueItems,
            metrics.ReviewRequiredItems,
            metrics.ReviewedIssueItems,
            metrics.UnreviewedIssueItems,
            metrics.TotalDifferenceAmount,
            batch.Notes,
            batch.CreatedAtUtc,
            batch.CreatedByUserId,
            batch.ClosedAtUtc,
            batch.ClosedByUserId,
            orderedItems);
    }

    private static PaymentReconciliationItemResponse ToItemResponse(PaymentReconciliationItem item)
    {
        return new PaymentReconciliationItemResponse(
            item.Id,
            item.PaymentReconciliationBatchId,
            item.ProviderTransactionId,
            item.ProviderAmount,
            item.ProviderStatus,
            item.MatchedPaymentId,
            item.MatchedPaymentAttemptId,
            item.MatchStatus,
            item.DifferenceAmount,
            item.IssueReason,
            item.ReviewDecision,
            item.ReviewNotes,
            item.ReviewedAtUtc,
            item.ReviewedByUserId,
            item.CreatedAtUtc);
    }

    private static PaymentReconciliationMetrics CalculateMetrics(
        IEnumerable<PaymentReconciliationItem> items)
    {
        var array = items.ToArray();
        var matchedItems = array.Count(item => item.MatchStatus == PaymentReconciliationItemStatus.Matched);
        var reviewRequiredItems = array.Count(RequiresReview);
        var reviewedIssueItems = array.Count(item => RequiresReview(item) && IsReviewed(item));
        var totalDifferenceAmount = array
            .Where(item => RequiresReview(item) && item.DifferenceAmount.HasValue)
            .Sum(item => Math.Abs(item.DifferenceAmount!.Value));

        return new PaymentReconciliationMetrics(
            array.Length,
            matchedItems,
            array.Length - matchedItems,
            reviewRequiredItems,
            reviewedIssueItems,
            reviewRequiredItems - reviewedIssueItems,
            totalDifferenceAmount);
    }

    private static bool RequiresReview(PaymentReconciliationItem item)
    {
        return item.MatchStatus != PaymentReconciliationItemStatus.Matched;
    }

    private static bool IsReviewed(PaymentReconciliationItem item)
    {
        return item.ReviewDecision != PaymentReconciliationReviewDecision.None
            && item.ReviewedAtUtc.HasValue;
    }

    private static bool IsClosureSafeReviewDecision(
        PaymentReconciliationReviewDecision decision)
    {
        return decision is PaymentReconciliationReviewDecision.AcceptedAsProviderException
            or PaymentReconciliationReviewDecision.DuplicateProviderRecord
            or PaymentReconciliationReviewDecision.IgnoredAsNonFinancial;
    }

    private sealed record PaymentReconciliationMetrics(
        int TotalItems,
        int MatchedItems,
        int IssueItems,
        int ReviewRequiredItems,
        int ReviewedIssueItems,
        int UnreviewedIssueItems,
        decimal TotalDifferenceAmount);

    private static string TrimRequired(string value)
    {
        return value.Trim();
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
