using System.Data;
using System.Text.Json;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class PaymentService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null,
    IAuditLogService? auditLogService = null,
    ICurrentUserService? currentUserService = null)
    : IPaymentService
{
    private const string DefaultCurrency = "IQD";

    private static readonly PaymentMethod[] ResidentPaymentMethods =
    [
        PaymentMethod.ZainCashMock,
        PaymentMethod.MasterCardMock
    ];

    private static readonly PaymentMethod[] ManualPaymentMethods =
    [
        PaymentMethod.Cash,
        PaymentMethod.BankTransfer,
        PaymentMethod.ManualAdminPayment
    ];

    public async Task<PagedResult<PaymentResponse>> SearchPaymentsAsync(
        PaymentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var payments = await ApplyCurrentCompoundScopeAsync(
            ApplyPaymentFilters(GetPaymentDetailsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedPaymentResultAsync(payments, query, cancellationToken);
    }

    public async Task<ServiceResult<PaymentResponse>> GetPaymentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var payments = await ApplyCurrentCompoundScopeAsync(
            GetPaymentDetailsQuery(asNoTracking: true),
            cancellationToken);

        var payment = await payments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment is null)
        {
            return ServiceResult<PaymentResponse>.NotFound("Payment was not found.");
        }

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }

    public async Task<ServiceResult<PaymentResponse>> RecordManualPaymentAsync(
        ManualPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ManualPaymentMethods.Contains(request.PaymentMethod))
        {
            return ServiceResult<PaymentResponse>.BadRequest(
                "Manual payments must use Cash, BankTransfer, or ManualAdminPayment.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var idempotencyKey = TrimOrNull(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existingPayment = await GetPaymentDetailsQuery(asNoTracking: true)
                .FirstOrDefaultAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existingPayment is not null)
            {
                if (!await CanCurrentUserAccessCompoundAsync(existingPayment.CompoundId, cancellationToken))
                {
                    return ServiceResult<PaymentResponse>.Conflict("Idempotency key is already used by another payment.");
                }

                if (existingPayment.TargetType != request.TargetType
                    || existingPayment.TargetId != request.TargetId
                    || existingPayment.PaymentMethod != request.PaymentMethod
                    || existingPayment.Amount != request.Amount)
                {
                    return ServiceResult<PaymentResponse>.Conflict("Idempotency key is already used for a different payment.");
                }

                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<PaymentResponse>.Success(
                    await ToPaymentResponseAsync(existingPayment, cancellationToken));
            }
        }

        var targetResult = await GetPaymentTargetForPaymentAsync(
            request.TargetType,
            request.TargetId,
            request.Amount,
            cancellationToken);
        if (targetResult.Failure is not null)
        {
            return ToResult<PaymentResponse>(targetResult.Failure);
        }

        var target = targetResult.Target!;
        if (!await CanCurrentUserAccessCompoundAsync(target.CompoundId, cancellationToken))
        {
            return ServiceResult<PaymentResponse>.Forbidden(
                "Current user cannot access the payment target compound.");
        }

        var payment = new Payment
        {
            CompoundId = target.CompoundId,
            ResidentProfileId = target.ResidentProfileId,
            TargetType = target.TargetType,
            TargetId = target.TargetId,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = request.Amount,
            Currency = DefaultCurrency,
            IdempotencyKey = idempotencyKey,
            PaymentReference = await GenerateUniquePaymentReferenceAsync(cancellationToken),
            CompletedAt = DateTime.UtcNow,
            Attempts =
            [
                new PaymentAttempt
                {
                    AttemptStatus = PaymentStatus.Succeeded,
                    Provider = request.PaymentMethod.ToString(),
                    Message = TrimOrNull(request.Notes) ?? "Manual payment recorded."
                }
            ],
            Receipt = new Receipt
            {
                ReceiptNumber = await GenerateUniqueReceiptNumberAsync(cancellationToken),
                Amount = request.Amount
            }
        };
        payment.Receipt.PaymentId = payment.Id;

        ApplySuccessfulPaymentToTarget(target, request.Amount, payment.Id);
        await AddPaymentLedgerEntryIfMissingAsync(
            payment,
            target,
            FinancialLedgerSourceType.Payment,
            FinancialLedgerEntryDirection.Credit,
            payment.CompletedAt ?? DateTime.UtcNow,
            "Payment received",
            cancellationToken);

        dbContext.Payments.Add(payment);
        var saveFailure = await SavePaymentChangesWithConcurrencyGuardAsync(cancellationToken);
        if (saveFailure is not null)
        {
            return saveFailure;
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }

    public async Task<ServiceResult<PaymentResponse>> RefundPaymentAsync(
        Guid id,
        RefundPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<PaymentResponse>.BadRequest("Refund reason is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var payments = await ApplyCurrentCompoundScopeAsync(
            GetPaymentDetailsQuery(asNoTracking: false),
            cancellationToken);

        var payment = await payments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment is null)
        {
            return ServiceResult<PaymentResponse>.NotFound("Payment was not found.");
        }

        if (payment.PaymentStatus != PaymentStatus.Succeeded)
        {
            return ServiceResult<PaymentResponse>.BadRequest("Only succeeded payments can be refunded.");
        }

        var targetResult = await GetPaymentTargetForRefundAsync(payment, cancellationToken);
        if (targetResult.Failure is not null)
        {
            return ToResult<PaymentResponse>(targetResult.Failure);
        }

        var refundFailure = ApplyRefundToTarget(targetResult.Target!, payment.Amount);
        if (refundFailure is not null)
        {
            return ToResult<PaymentResponse>(refundFailure);
        }

        payment.PaymentStatus = PaymentStatus.Refunded;
        payment.RefundedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;
        dbContext.PaymentAttempts.Add(new PaymentAttempt
        {
            PaymentId = payment.Id,
            AttemptStatus = PaymentStatus.Refunded,
            Provider = "Refund",
            Message = reason
        });
        await AddPaymentLedgerEntryIfMissingAsync(
            payment,
            targetResult.Target!,
            FinancialLedgerSourceType.Refund,
            FinancialLedgerEntryDirection.Debit,
            payment.RefundedAt ?? DateTime.UtcNow,
            reason,
            cancellationToken);

        var saveFailure = await SavePaymentChangesWithConcurrencyGuardAsync(cancellationToken);
        if (saveFailure is not null)
        {
            return saveFailure;
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }

    public async Task<PagedResult<PaymentResponse>> SearchResidentPaymentsAsync(
        Guid userId,
        PaymentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetResidentPaymentScopeAsync(userId, cancellationToken);
        if (scope.ProfileIds.Length == 0)
        {
            return new PagedResult<PaymentResponse>([], query.PageNumber, query.PageSize, 0);
        }

        var profileIds = scope.ProfileIds;

        var payments = GetPaymentDetailsQuery(asNoTracking: true)
            .Where(payment => payment.ResidentProfileId.HasValue
                && profileIds.Contains(payment.ResidentProfileId.Value));

        payments = ApplyPaymentFilters(payments, query);

        return await ToPagedPaymentResultAsync(payments, query, cancellationToken);
    }

    public async Task<ServiceResult<PaymentResponse>> GetResidentPaymentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var payment = await GetVisibleResidentPaymentAsync(userId, id, asNoTracking: true, cancellationToken);
        if (payment is null)
        {
            return ServiceResult<PaymentResponse>.NotFound("Payment was not found.");
        }

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }

    public async Task<ServiceResult<PaymentResponse>> StartResidentPaymentAsync(
        Guid userId,
        StartPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ResidentPaymentMethods.Contains(request.PaymentMethod))
        {
            return ServiceResult<PaymentResponse>.BadRequest(
                "Residents can only use ZainCashMock or MasterCardMock.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var idempotencyKey = TrimOrNull(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existingPayment = await GetPaymentDetailsQuery(asNoTracking: true)
                .FirstOrDefaultAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existingPayment is not null)
            {
                if (!await IsPaymentVisibleToResidentAsync(userId, existingPayment, cancellationToken))
                {
                    return ServiceResult<PaymentResponse>.Conflict(
                        "Idempotency key is already used by another payment.");
                }

                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<PaymentResponse>.Success(
                    await ToPaymentResponseAsync(existingPayment, cancellationToken));
            }
        }

        var targetResult = await GetResidentPaymentTargetForPaymentAsync(
            userId,
            request.TargetType,
            request.TargetId,
            request.Amount,
            cancellationToken);
        if (targetResult.Failure is not null)
        {
            return ToResult<PaymentResponse>(targetResult.Failure);
        }

        var target = targetResult.Target!;
        var payment = new Payment
        {
            CompoundId = target.CompoundId,
            ResidentProfileId = target.ResidentProfileId,
            TargetType = target.TargetType,
            TargetId = target.TargetId,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            Amount = request.Amount,
            Currency = DefaultCurrency,
            IdempotencyKey = idempotencyKey,
            PaymentReference = await GenerateUniquePaymentReferenceAsync(cancellationToken),
            Attempts =
            [
                new PaymentAttempt
                {
                    AttemptStatus = PaymentStatus.Pending,
                    Provider = request.PaymentMethod.ToString(),
                    Message = "Payment started."
                }
            ]
        };

        dbContext.Payments.Add(payment);
        var saveFailure = await SavePaymentChangesWithConcurrencyGuardAsync(cancellationToken);
        if (saveFailure is not null)
        {
            return saveFailure;
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }

    public async Task<ServiceResult<PaymentResponse>> ConfirmResidentMockPaymentSuccessAsync(
        Guid userId,
        Guid id,
        PaymentMethod expectedPaymentMethod,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var payment = await GetVisibleResidentPaymentAsync(userId, id, asNoTracking: false, cancellationToken);
        if (payment is null)
        {
            return ServiceResult<PaymentResponse>.NotFound("Payment was not found.");
        }

        var validationFailure = ValidateConfirmableMockPayment(payment, expectedPaymentMethod);
        if (validationFailure is not null)
        {
            return ToResult<PaymentResponse>(validationFailure);
        }

        var targetResult = await GetPaymentTargetForPaymentAsync(
            payment.TargetType,
            payment.TargetId,
            payment.Amount,
            cancellationToken);
        if (targetResult.Failure is not null)
        {
            return ToResult<PaymentResponse>(targetResult.Failure);
        }

        var target = targetResult.Target!;
        ApplySuccessfulPaymentToTarget(target, payment.Amount, payment.Id);

        var providerTransactionId = await ResolveMockProviderTransactionIdAsync(
            payment,
            request.ProviderTransactionId,
            cancellationToken);
        if (providerTransactionId.Failure is not null)
        {
            return ToResult<PaymentResponse>(providerTransactionId.Failure);
        }

        payment.PaymentStatus = PaymentStatus.Succeeded;
        payment.CompletedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;
        dbContext.PaymentAttempts.Add(new PaymentAttempt
        {
            PaymentId = payment.Id,
            AttemptStatus = PaymentStatus.Succeeded,
            Provider = payment.PaymentMethod.ToString(),
            ProviderTransactionId = providerTransactionId.Value,
            Message = TrimOrNull(request.Message) ?? "Mock payment succeeded."
        });

        if (payment.Receipt is null)
        {
            var receipt = new Receipt
            {
                PaymentId = payment.Id,
                ReceiptNumber = await GenerateUniqueReceiptNumberAsync(cancellationToken),
                Amount = payment.Amount
            };
            dbContext.Receipts.Add(receipt);
            payment.Receipt = receipt;
        }
        await AddPaymentLedgerEntryIfMissingAsync(
            payment,
            target,
            FinancialLedgerSourceType.Payment,
            FinancialLedgerEntryDirection.Credit,
            payment.CompletedAt ?? DateTime.UtcNow,
            "Payment received",
            cancellationToken);

        var saveFailure = await SavePaymentChangesWithConcurrencyGuardAsync(cancellationToken);
        if (saveFailure is not null)
        {
            return saveFailure;
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }

    public async Task<ServiceResult<PaymentResponse>> ConfirmResidentMockPaymentFailureAsync(
        Guid userId,
        Guid id,
        PaymentMethod expectedPaymentMethod,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var payment = await GetVisibleResidentPaymentAsync(userId, id, asNoTracking: false, cancellationToken);
        if (payment is null)
        {
            return ServiceResult<PaymentResponse>.NotFound("Payment was not found.");
        }

        var validationFailure = ValidateConfirmableMockPayment(payment, expectedPaymentMethod);
        if (validationFailure is not null)
        {
            return ToResult<PaymentResponse>(validationFailure);
        }

        var failureReason = TrimOrNull(request.Message) ?? "Mock payment failed.";
        var providerTransactionId = await ResolveMockProviderTransactionIdAsync(
            payment,
            request.ProviderTransactionId,
            cancellationToken);
        if (providerTransactionId.Failure is not null)
        {
            return ToResult<PaymentResponse>(providerTransactionId.Failure);
        }

        payment.PaymentStatus = PaymentStatus.Failed;
        payment.FailureReason = failureReason;
        payment.UpdatedAt = DateTime.UtcNow;
        dbContext.PaymentAttempts.Add(new PaymentAttempt
        {
            PaymentId = payment.Id,
            AttemptStatus = PaymentStatus.Failed,
            Provider = payment.PaymentMethod.ToString(),
            ProviderTransactionId = providerTransactionId.Value,
            Message = failureReason
        });

        var saveFailure = await SavePaymentChangesWithConcurrencyGuardAsync(cancellationToken);
        if (saveFailure is not null)
        {
            return saveFailure;
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PaymentResponse>.Success(
            await ToPaymentResponseAsync(payment, cancellationToken));
    }


    private async Task AddPaymentLedgerEntryIfMissingAsync(
        Payment payment,
        PaymentTargetContext target,
        FinancialLedgerSourceType sourceType,
        FinancialLedgerEntryDirection direction,
        DateTime occurredAtUtc,
        string description,
        CancellationToken cancellationToken)
    {
        if (!target.ResidentProfileId.HasValue)
        {
            return;
        }

        var alreadyExists = await dbContext.ResidentLedgerEntries
            .AnyAsync(entry => entry.SourceType == sourceType && entry.SourceId == payment.Id, cancellationToken);
        if (alreadyExists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var referencePrefix = sourceType == FinancialLedgerSourceType.Refund ? "REF" : "PAY";
        var ledgerEntry = new ResidentLedgerEntry
        {
            CompoundId = target.CompoundId,
            ResidentProfileId = target.ResidentProfileId.Value,
            Direction = direction,
            SourceType = sourceType,
            SourceId = payment.Id,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Reference = string.IsNullOrWhiteSpace(payment.PaymentReference)
                ? $"{referencePrefix}-{payment.Id.ToString("N")[..12].ToUpperInvariant()}"
                : payment.PaymentReference,
            Description = TrimLedgerDescription(description),
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = now,
            CreatedByUserId = currentUserService?.UserId
        };

        dbContext.ResidentLedgerEntries.Add(ledgerEntry);
        await AddPaymentLedgerAuditAsync(payment, ledgerEntry, cancellationToken);
    }

    private async Task AddPaymentLedgerAuditAsync(
        Payment payment,
        ResidentLedgerEntry ledgerEntry,
        CancellationToken cancellationToken)
    {
        if (auditLogService is null)
        {
            return;
        }

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            ledgerEntry.CompoundId,
            ledgerEntry.ResidentProfileId,
            currentUserService?.UserId,
            RoleNames.PaymentManagers,
            AuditActionType.LedgerEntryCreated,
            AuditEntityType.ResidentLedgerEntry,
            ledgerEntry.Id,
            ledgerEntry.SourceType == FinancialLedgerSourceType.Refund ? AuditSeverity.High : AuditSeverity.Medium,
            "Payments",
            ledgerEntry.SourceType == FinancialLedgerSourceType.Refund
                ? "Refund ledger entry created."
                : "Payment ledger entry created.",
            ledgerEntry.Description,
            AfterValuesJson: JsonSerializer.Serialize(new
            {
                ledgerEntry.Id,
                ledgerEntry.CompoundId,
                ledgerEntry.ResidentProfileId,
                ledgerEntry.Direction,
                ledgerEntry.SourceType,
                ledgerEntry.SourceId,
                ledgerEntry.Amount,
                ledgerEntry.Currency,
                ledgerEntry.Reference,
                PaymentId = payment.Id,
                payment.TargetType,
                payment.TargetId,
                payment.PaymentStatus
            }),
            MetadataJson: JsonSerializer.Serialize(new
            {
                PaymentId = payment.Id,
                payment.PaymentReference,
                payment.PaymentMethod,
                payment.TargetType,
                payment.TargetId
            }),
            Changes:
            [
                new AuditLogChangeRecord(nameof(ResidentLedgerEntry.Direction), null, ledgerEntry.Direction.ToString()),
                new AuditLogChangeRecord(nameof(ResidentLedgerEntry.SourceType), null, ledgerEntry.SourceType.ToString()),
                new AuditLogChangeRecord(nameof(ResidentLedgerEntry.Amount), null, ledgerEntry.Amount.ToString("F2"))
            ]), cancellationToken);
    }


    private async Task<ServiceResult<PaymentResponse>?> SavePaymentChangesWithConcurrencyGuardAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<PaymentResponse>.Conflict(
                "The financial target was changed by another operation. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            return ServiceResult<PaymentResponse>.Conflict(
                "A duplicate payment idempotency key, payment reference, or provider transaction reference was detected.");
        }
    }

    private IQueryable<Payment> GetPaymentDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.Payments
            .Include(payment => payment.Compound)
            .Include(payment => payment.ResidentProfile)
            .Include(payment => payment.Attempts)
            .Include(payment => payment.Receipt)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private static IQueryable<Payment> ApplyPaymentFilters(
        IQueryable<Payment> payments,
        PaymentSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            payments = payments.Where(payment => payment.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            payments = payments.Where(payment => payment.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.TargetType.HasValue)
        {
            payments = payments.Where(payment => payment.TargetType == query.TargetType.Value);
        }

        if (query.TargetId.HasValue)
        {
            payments = payments.Where(payment => payment.TargetId == query.TargetId.Value);
        }

        if (query.PaymentMethod.HasValue)
        {
            payments = payments.Where(payment => payment.PaymentMethod == query.PaymentMethod.Value);
        }

        if (query.PaymentStatus.HasValue)
        {
            payments = payments.Where(payment => payment.PaymentStatus == query.PaymentStatus.Value);
        }

        if (query.CreatedFrom.HasValue)
        {
            payments = payments.Where(payment => payment.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            payments = payments.Where(payment => payment.CreatedAt <= query.CreatedTo.Value);
        }

        return payments;
    }

    private async Task<PagedResult<PaymentResponse>> ToPagedPaymentResultAsync(
        IQueryable<Payment> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var payments = await query
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenBy(payment => payment.PaymentReference)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);

        var targetReferences = await LoadPaymentTargetReferencesAsync(payments, cancellationToken);

        return new PagedResult<PaymentResponse>(
            payments.Select(payment => ToPaymentResponse(payment, targetReferences.GetValueOrDefault(payment.TargetId))).ToArray(),
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PaymentResponse> ToPaymentResponseAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        var paymentWithDetails = await GetPaymentDetailsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == payment.Id, cancellationToken)
            ?? payment;

        var targetReferences = await LoadPaymentTargetReferencesAsync([paymentWithDetails], cancellationToken);
        return ToPaymentResponse(paymentWithDetails, targetReferences.GetValueOrDefault(paymentWithDetails.TargetId));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadPaymentTargetReferencesAsync(
        IReadOnlyCollection<Payment> payments,
        CancellationToken cancellationToken)
    {
        var references = new Dictionary<Guid, string>();

        var utilityBillIds = payments
            .Where(payment => payment.TargetType == PaymentTargetType.UtilityBill)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        if (utilityBillIds.Length > 0)
        {
            foreach (var item in await dbContext.UtilityBills
                .AsNoTracking()
                .Where(bill => utilityBillIds.Contains(bill.Id))
                .Select(bill => new { bill.Id, bill.BillNumber })
                .ToArrayAsync(cancellationToken))
            {
                references[item.Id] = item.BillNumber;
            }
        }

        var installmentIds = payments
            .Where(payment => payment.TargetType == PaymentTargetType.PropertyInstallment)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        if (installmentIds.Length > 0)
        {
            foreach (var item in await dbContext.InstallmentScheduleItems
                .AsNoTracking()
                .Include(installment => installment.PropertySaleContract)
                .Where(installment => installmentIds.Contains(installment.Id))
                .Select(installment => new
                {
                    installment.Id,
                    Reference = installment.PropertySaleContract.ContractNumber + " / Installment " + installment.InstallmentNumber
                })
                .ToArrayAsync(cancellationToken))
            {
                references[item.Id] = item.Reference;
            }
        }

        var rentInvoiceIds = payments
            .Where(payment => payment.TargetType == PaymentTargetType.RentInvoice)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        if (rentInvoiceIds.Length > 0)
        {
            foreach (var item in await dbContext.RentInvoices
                .AsNoTracking()
                .Where(invoice => rentInvoiceIds.Contains(invoice.Id))
                .Select(invoice => new { invoice.Id, invoice.InvoiceNumber })
                .ToArrayAsync(cancellationToken))
            {
                references[item.Id] = item.InvoiceNumber;
            }
        }

        var violationFineIds = payments
            .Where(payment => payment.TargetType == PaymentTargetType.ViolationFine)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        if (violationFineIds.Length > 0)
        {
            foreach (var item in await dbContext.ViolationFines
                .AsNoTracking()
                .Include(fine => fine.Violation)
                .Where(fine => violationFineIds.Contains(fine.Id))
                .Select(fine => new
                {
                    fine.Id,
                    Reference = "Violation fine / " + fine.Violation.Title
                })
                .ToArrayAsync(cancellationToken))
            {
                references[item.Id] = item.Reference;
            }
        }

        var paymentPlanInstallmentIds = payments
            .Where(payment => payment.TargetType == PaymentTargetType.PaymentPlanInstallment)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        if (paymentPlanInstallmentIds.Length > 0)
        {
            foreach (var item in await dbContext.PaymentPlanInstallments
                .AsNoTracking()
                .Include(installment => installment.PaymentPlan)
                .Where(installment => paymentPlanInstallmentIds.Contains(installment.Id))
                .Select(installment => new
                {
                    installment.Id,
                    Reference = "Payment plan installment " + installment.InstallmentNumber
                })
                .ToArrayAsync(cancellationToken))
            {
                references[item.Id] = item.Reference;
            }
        }

        var propertySaleContractIds = payments
            .Where(payment => payment.TargetType == PaymentTargetType.PropertySaleContract)
            .Select(payment => payment.TargetId)
            .Distinct()
            .ToArray();
        if (propertySaleContractIds.Length > 0)
        {
            foreach (var item in await dbContext.PropertySaleContracts
                .AsNoTracking()
                .Where(contract => propertySaleContractIds.Contains(contract.Id))
                .Select(contract => new { contract.Id, contract.ContractNumber })
                .ToArrayAsync(cancellationToken))
            {
                references[item.Id] = item.ContractNumber;
            }
        }

        return references;
    }

    private static PaymentResponse ToPaymentResponse(Payment payment, string? targetReference)
    {
        var receipt = payment.Receipt is null
            ? null
            : new ReceiptResponse(
                payment.Receipt.Id,
                payment.Receipt.PaymentId,
                payment.Receipt.ReceiptNumber,
                payment.Receipt.Amount,
                payment.Receipt.IssuedAt);

        var attempts = payment.Attempts
            .OrderBy(attempt => attempt.CreatedAt)
            .Select(attempt => new PaymentAttemptResponse(
                attempt.Id,
                attempt.PaymentId,
                attempt.AttemptStatus,
                attempt.Provider,
                attempt.ProviderTransactionId,
                attempt.Message,
                attempt.CreatedAt))
            .ToArray();

        return new PaymentResponse(
            payment.Id,
            payment.CompoundId,
            payment.Compound.Name,
            payment.ResidentProfileId,
            payment.ResidentProfile?.FullName,
            payment.TargetType,
            payment.TargetId,
            targetReference,
            payment.PaymentMethod,
            payment.PaymentStatus,
            payment.Amount,
            payment.Currency,
            null,
            payment.PaymentReference,
            payment.FailureReason,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.CompletedAt,
            payment.CancelledAt,
            payment.RefundedAt,
            receipt,
            attempts);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetPaymentTargetForPaymentAsync(
        PaymentTargetType targetType,
        Guid targetId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (targetId == Guid.Empty)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Payment target id is required."));
        }

        if (amount <= 0)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Payment amount must be greater than zero."));
        }

        return targetType switch
        {
            PaymentTargetType.UtilityBill => await GetUtilityBillPaymentTargetAsync(targetId, amount, cancellationToken),
            PaymentTargetType.PropertyInstallment => await GetInstallmentPaymentTargetAsync(targetId, amount, cancellationToken),
            PaymentTargetType.RentInvoice => await GetRentInvoicePaymentTargetAsync(targetId, amount, cancellationToken),
            PaymentTargetType.ViolationFine => await GetViolationFinePaymentTargetAsync(targetId, amount, cancellationToken),
            _ => (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Unsupported payment target."))
        };
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetUtilityBillPaymentTargetAsync(
        Guid targetId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var bill = await dbContext.UtilityBills.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (bill is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Utility bill was not found."));
        }

        if (bill.BillStatus == BillStatus.Cancelled)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Utility bill is cancelled."));
        }

        var remainingAmount = Math.Max(0m, bill.TotalAmount - bill.PaidAmount);
        if (remainingAmount <= 0 || bill.BillStatus == BillStatus.Paid)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Utility bill is already paid."));
        }

        if (amount > remainingAmount)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Payment amount cannot exceed the remaining utility bill balance."));
        }

        return (PaymentTargetContext.ForUtilityBill(bill, bill.ResidentProfileId), null);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetInstallmentPaymentTargetAsync(
        Guid targetId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var installment = await dbContext.InstallmentScheduleItems
            .FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (installment is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Installment was not found."));
        }

        if (installment.InstallmentStatus == InstallmentStatus.Cancelled)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Installment is cancelled."));
        }

        var remainingAmount = Math.Max(0m, installment.Amount - installment.PaidAmount);
        if (remainingAmount <= 0 || installment.InstallmentStatus == InstallmentStatus.Paid)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Installment is already paid."));
        }

        if (amount > remainingAmount)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Payment amount cannot exceed the remaining installment balance."));
        }

        return (PaymentTargetContext.ForInstallment(installment), null);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetRentInvoicePaymentTargetAsync(
        Guid targetId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.RentInvoices.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (invoice is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Rent invoice was not found."));
        }

        if (invoice.RentInvoiceStatus == RentInvoiceStatus.Cancelled)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Rent invoice is cancelled."));
        }

        var remainingAmount = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);
        if (remainingAmount <= 0 || invoice.RentInvoiceStatus == RentInvoiceStatus.Paid)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Rent invoice is already paid."));
        }

        if (amount > remainingAmount)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Payment amount cannot exceed the remaining rent invoice balance."));
        }

        return (PaymentTargetContext.ForRentInvoice(invoice), null);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetViolationFinePaymentTargetAsync(
        Guid targetId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var fine = await dbContext.ViolationFines.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (fine is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Violation fine was not found."));
        }

        if (fine.Status == ViolationFineStatus.Cancelled)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Violation fine is cancelled."));
        }

        var remainingAmount = Math.Max(0m, fine.Amount - fine.PaidAmount);
        if (remainingAmount <= 0 || fine.Status == ViolationFineStatus.Paid)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Violation fine is already paid."));
        }

        if (amount > remainingAmount)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Payment amount cannot exceed the remaining violation fine balance."));
        }

        return (PaymentTargetContext.ForViolationFine(fine), null);
    }


    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetPaymentTargetForRefundAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        return payment.TargetType switch
        {
            PaymentTargetType.UtilityBill => await GetUtilityBillRefundTargetAsync(payment.TargetId, cancellationToken),
            PaymentTargetType.PropertyInstallment => await GetInstallmentRefundTargetAsync(payment.TargetId, cancellationToken),
            PaymentTargetType.RentInvoice => await GetRentInvoiceRefundTargetAsync(payment.TargetId, cancellationToken),
            PaymentTargetType.ViolationFine => await GetViolationFineRefundTargetAsync(payment.TargetId, cancellationToken),
            _ => (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Unsupported payment target."))
        };
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetUtilityBillRefundTargetAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var bill = await dbContext.UtilityBills.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (bill is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Utility bill was not found."));
        }

        return (PaymentTargetContext.ForUtilityBill(bill, bill.ResidentProfileId), null);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetInstallmentRefundTargetAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var installment = await dbContext.InstallmentScheduleItems.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (installment is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Installment was not found."));
        }

        return (PaymentTargetContext.ForInstallment(installment), null);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetRentInvoiceRefundTargetAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.RentInvoices.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (invoice is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Rent invoice was not found."));
        }

        return (PaymentTargetContext.ForRentInvoice(invoice), null);
    }

    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetViolationFineRefundTargetAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var fine = await dbContext.ViolationFines.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (fine is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Violation fine was not found."));
        }

        return (PaymentTargetContext.ForViolationFine(fine), null);
    }


    private async Task<(PaymentTargetContext? Target, ValidationFailure? Failure)> GetResidentPaymentTargetForPaymentAsync(
        Guid userId,
        PaymentTargetType targetType,
        Guid targetId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var targetResult = await GetPaymentTargetForPaymentAsync(targetType, targetId, amount, cancellationToken);
        if (targetResult.Failure is not null)
        {
            return targetResult;
        }

        var target = targetResult.Target!;
        var scope = await GetResidentPaymentScopeAsync(userId, cancellationToken);
        var isVisible = target.ResidentProfileId.HasValue
            && scope.ProfileIds.Contains(target.ResidentProfileId.Value);

        if (!isVisible)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Payment target was not found."));
        }

        if (!target.ResidentProfileId.HasValue)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Resident profile is required to start payment."));
        }

        return (target, null);
    }

    private async Task<ResidentPaymentScope> GetResidentPaymentScopeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profileIds = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => profile.Id)
            .ToArrayAsync(cancellationToken);

        return new ResidentPaymentScope(profileIds);
    }

    private async Task<Payment?> GetVisibleResidentPaymentAsync(
        Guid userId,
        Guid paymentId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var payment = await GetPaymentDetailsQuery(asNoTracking)
            .FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        return await IsPaymentVisibleToResidentAsync(userId, payment, cancellationToken)
            ? payment
            : null;
    }

    private async Task<bool> IsPaymentVisibleToResidentAsync(
        Guid userId,
        Payment payment,
        CancellationToken cancellationToken)
    {
        var scope = await GetResidentPaymentScopeAsync(userId, cancellationToken);

        return payment.ResidentProfileId.HasValue
            && scope.ProfileIds.Contains(payment.ResidentProfileId.Value);
    }

    private static ValidationFailure? ValidateConfirmableMockPayment(
        Payment payment,
        PaymentMethod expectedPaymentMethod)
    {
        if (!ResidentPaymentMethods.Contains(expectedPaymentMethod))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Invalid mock payment method.");
        }

        if (payment.PaymentMethod != expectedPaymentMethod)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Payment method does not match this confirmation endpoint.");
        }

        if (!ResidentPaymentMethods.Contains(payment.PaymentMethod))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Manual payments cannot be mock-confirmed.");
        }

        return payment.PaymentStatus switch
        {
            PaymentStatus.Pending => null,
            PaymentStatus.Succeeded => new ValidationFailure(ServiceResultStatus.Conflict, "Payment already succeeded."),
            PaymentStatus.Failed => new ValidationFailure(ServiceResultStatus.Conflict, "Failed payment cannot be confirmed again."),
            PaymentStatus.Cancelled => new ValidationFailure(ServiceResultStatus.Conflict, "Cancelled payment cannot be confirmed."),
            PaymentStatus.Refunded => new ValidationFailure(ServiceResultStatus.Conflict, "Refunded payment cannot be confirmed."),
            _ => new ValidationFailure(ServiceResultStatus.BadRequest, "Payment cannot be confirmed.")
        };
    }

    private async Task<(string? Value, ValidationFailure? Failure)> ResolveMockProviderTransactionIdAsync(
        Payment payment,
        string? requestedProviderTransactionId,
        CancellationToken cancellationToken)
    {
        var provider = payment.PaymentMethod.ToString();
        var providerTransactionId = TrimOrNull(requestedProviderTransactionId)
            ?? $"MOCK-{provider}-{payment.Id:N}";

        var duplicateExists = await dbContext.PaymentAttempts
            .AsNoTracking()
            .AnyAsync(attempt =>
                attempt.Provider == provider
                && attempt.ProviderTransactionId == providerTransactionId
                && attempt.PaymentId != payment.Id,
                cancellationToken);
        if (duplicateExists)
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.Conflict,
                "Provider transaction id is already linked to another payment."));
        }

        return (providerTransactionId, null);
    }

    private static void ApplySuccessfulPaymentToTarget(
        PaymentTargetContext target,
        decimal amount,
        Guid paymentId)
    {
        switch (target.TargetType)
        {
            case PaymentTargetType.UtilityBill:
                var bill = target.UtilityBill!;
                bill.PaidAmount += amount;
                bill.BillStatus = DetermineBillStatus(bill.PaidAmount, bill.TotalAmount, bill.DueDate);
                bill.UpdatedAt = DateTime.UtcNow;
                break;

            case PaymentTargetType.PropertyInstallment:
                var installment = target.Installment!;
                installment.PaidAmount += amount;
                installment.InstallmentStatus = DetermineInstallmentStatus(
                    installment.PaidAmount,
                    installment.Amount,
                    installment.DueDate);
                installment.PaidAt = installment.InstallmentStatus == InstallmentStatus.Paid
                    ? installment.PaidAt ?? DateTime.UtcNow
                    : null;
                installment.UpdatedAt = DateTime.UtcNow;
                break;

            case PaymentTargetType.RentInvoice:
                var invoice = target.RentInvoice!;
                invoice.PaidAmount += amount;
                invoice.RentInvoiceStatus = DetermineRentInvoiceStatus(
                    invoice.PaidAmount,
                    invoice.TotalAmount,
                    invoice.DueDate);
                invoice.UpdatedAt = DateTime.UtcNow;
                break;

            case PaymentTargetType.ViolationFine:
                var fine = target.ViolationFine!;
                fine.PaidAmount += amount;
                fine.Status = DetermineViolationFineStatus(fine.PaidAmount, fine.Amount);
                fine.UpdatedAt = DateTime.UtcNow;
                break;
        }
    }

    private static ValidationFailure? ApplyRefundToTarget(PaymentTargetContext target, decimal amount)
    {
        switch (target.TargetType)
        {
            case PaymentTargetType.UtilityBill:
                var bill = target.UtilityBill!;
                if (bill.BillStatus == BillStatus.Cancelled)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Payment for a cancelled utility bill cannot be refunded.");
                }

                if (bill.PaidAmount < amount)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Refund would make the utility bill paid amount negative.");
                }

                bill.PaidAmount -= amount;
                bill.BillStatus = DetermineBillStatus(bill.PaidAmount, bill.TotalAmount, bill.DueDate);
                bill.UpdatedAt = DateTime.UtcNow;
                return null;

            case PaymentTargetType.PropertyInstallment:
                var installment = target.Installment!;
                if (installment.InstallmentStatus == InstallmentStatus.Cancelled)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Payment for a cancelled installment cannot be refunded.");
                }

                if (installment.PaidAmount < amount)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Refund would make the installment paid amount negative.");
                }

                installment.PaidAmount -= amount;
                installment.InstallmentStatus = DetermineInstallmentStatus(
                    installment.PaidAmount,
                    installment.Amount,
                    installment.DueDate);
                installment.PaidAt = installment.InstallmentStatus == InstallmentStatus.Paid
                    ? installment.PaidAt
                    : null;
                installment.UpdatedAt = DateTime.UtcNow;
                return null;

            case PaymentTargetType.RentInvoice:
                var invoice = target.RentInvoice!;
                if (invoice.RentInvoiceStatus == RentInvoiceStatus.Cancelled)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Payment for a cancelled rent invoice cannot be refunded.");
                }

                if (invoice.PaidAmount < amount)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Refund would make the rent invoice paid amount negative.");
                }

                invoice.PaidAmount -= amount;
                invoice.RentInvoiceStatus = DetermineRentInvoiceStatus(
                    invoice.PaidAmount,
                    invoice.TotalAmount,
                    invoice.DueDate);
                invoice.UpdatedAt = DateTime.UtcNow;
                return null;

            case PaymentTargetType.ViolationFine:
                var fine = target.ViolationFine!;
                if (fine.Status == ViolationFineStatus.Cancelled)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Payment for a cancelled violation fine cannot be refunded.");
                }

                if (fine.PaidAmount < amount)
                {
                    return new ValidationFailure(ServiceResultStatus.BadRequest, "Refund would make the violation fine paid amount negative.");
                }

                fine.PaidAmount -= amount;
                fine.Status = DetermineViolationFineStatus(fine.PaidAmount, fine.Amount);
                fine.UpdatedAt = DateTime.UtcNow;
                return null;

            default:
                return new ValidationFailure(ServiceResultStatus.BadRequest, "Unsupported payment target.");
        }
    }

    private static BillStatus DetermineBillStatus(decimal paidAmount, decimal totalAmount, DateOnly dueDate)
    {
        if (paidAmount >= totalAmount)
        {
            return BillStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return BillStatus.PartiallyPaid;
        }

        return dueDate < DateOnly.FromDateTime(DateTime.UtcNow)
            ? BillStatus.Overdue
            : BillStatus.Unpaid;
    }

    private static InstallmentStatus DetermineInstallmentStatus(decimal paidAmount, decimal amount, DateOnly dueDate)
    {
        if (paidAmount >= amount)
        {
            return InstallmentStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return InstallmentStatus.PartiallyPaid;
        }

        return dueDate < DateOnly.FromDateTime(DateTime.UtcNow)
            ? InstallmentStatus.Overdue
            : InstallmentStatus.Pending;
    }

    private static RentInvoiceStatus DetermineRentInvoiceStatus(decimal paidAmount, decimal totalAmount, DateOnly dueDate)
    {
        if (paidAmount >= totalAmount)
        {
            return RentInvoiceStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return RentInvoiceStatus.PartiallyPaid;
        }

        return dueDate < DateOnly.FromDateTime(DateTime.UtcNow)
            ? RentInvoiceStatus.Overdue
            : RentInvoiceStatus.Unpaid;
    }

    private static ViolationFineStatus DetermineViolationFineStatus(decimal paidAmount, decimal amount)
    {
        if (paidAmount >= amount)
        {
            return ViolationFineStatus.Paid;
        }

        return paidAmount > 0
            ? ViolationFineStatus.PartiallyPaid
            : ViolationFineStatus.Unpaid;
    }

    private static string GenerateReference(string prefix)
    {
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";
    }

    private async Task<string> GenerateUniquePaymentReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var reference = GenerateReference("PAY");
            var exists = await dbContext.Payments
                .AsNoTracking()
                .AnyAsync(payment => payment.PaymentReference == reference, cancellationToken);
            if (!exists)
            {
                return reference;
            }
        }

        return $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private async Task<string> GenerateUniqueReceiptNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var reference = GenerateReference("REC");
            var exists = await dbContext.Receipts
                .AsNoTracking()
                .AnyAsync(receipt => receipt.ReceiptNumber == reference, cancellationToken);
            if (!exists)
            {
                return reference;
            }
        }

        return $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private async Task<IQueryable<Payment>> ApplyCurrentCompoundScopeAsync(
        IQueryable<Payment> payments,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return payments;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return payments.ApplyCompoundAccess(scope, payment => payment.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static string TrimLedgerDescription(string? value)
    {
        var trimmed = TrimOrNull(value);
        if (trimmed is null)
        {
            return "Payment ledger entry.";
        }

        return trimmed.Length <= 1000
            ? trimmed
            : trimmed[..1000];
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);

    private sealed record ResidentPaymentScope(Guid[] ProfileIds);

    private sealed record PaymentTargetContext(
        PaymentTargetType TargetType,
        Guid TargetId,
        Guid CompoundId,
        Guid? ResidentProfileId,
        UtilityBill? UtilityBill,
        InstallmentScheduleItem? Installment,
        RentInvoice? RentInvoice,
        ViolationFine? ViolationFine)
    {
        public static PaymentTargetContext ForUtilityBill(UtilityBill bill, Guid? residentProfileId)
        {
            return new PaymentTargetContext(
                PaymentTargetType.UtilityBill,
                bill.Id,
                bill.CompoundId,
                residentProfileId,
                bill,
                null,
                null,
                null);
        }

        public static PaymentTargetContext ForInstallment(InstallmentScheduleItem installment)
        {
            return new PaymentTargetContext(
                PaymentTargetType.PropertyInstallment,
                installment.Id,
                installment.CompoundId,
                installment.ResidentProfileId,
                null,
                installment,
                null,
                null);
        }

        public static PaymentTargetContext ForRentInvoice(RentInvoice invoice)
        {
            return new PaymentTargetContext(
                PaymentTargetType.RentInvoice,
                invoice.Id,
                invoice.CompoundId,
                invoice.ResidentProfileId,
                null,
                null,
                invoice,
                null);
        }

        public static PaymentTargetContext ForViolationFine(ViolationFine fine)
        {
            return new PaymentTargetContext(
                PaymentTargetType.ViolationFine,
                fine.Id,
                fine.CompoundId,
                fine.ResidentProfileId,
                null,
                null,
                null,
                fine);
        }
    }
}
