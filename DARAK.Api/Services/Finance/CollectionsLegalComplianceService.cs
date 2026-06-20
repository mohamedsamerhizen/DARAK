using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CollectionsLegalComplianceService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : ICollectionsLegalComplianceService
{
    private const int MaxNameLength = 150;
    private const int MaxTextLength = 4000;

    public async Task<ServiceResult<CollectionsLegalComplianceSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<CollectionsLegalComplianceSummaryResponse>.Forbidden("Current user cannot access collections and legal compliance.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<CollectionsLegalComplianceSummaryResponse>.NotFound("Compound was not found.");
        }

        var penaltyRules = dbContext.PenaltyRules.AsNoTracking().ApplyCompoundAccess(scope, rule => rule.CompoundId);
        var collectionCases = dbContext.CollectionCases.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        var legalNotices = dbContext.LegalNotices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        var paymentPlans = dbContext.PaymentPlans.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);

        if (compoundId.HasValue)
        {
            penaltyRules = penaltyRules.Where(item => item.CompoundId == compoundId.Value);
            collectionCases = collectionCases.Where(item => item.CompoundId == compoundId.Value);
            legalNotices = legalNotices.Where(item => item.CompoundId == compoundId.Value);
            paymentPlans = paymentPlans.Where(item => item.CompoundId == compoundId.Value);
        }

        var response = new CollectionsLegalComplianceSummaryResponse(
            await penaltyRules.CountAsync(item => item.Status == PenaltyRuleStatus.Active, cancellationToken),
            await collectionCases.CountAsync(item => item.Status == CollectionCaseStatus.Open
                || item.Status == CollectionCaseStatus.Paused
                || item.Status == CollectionCaseStatus.PaymentPlanActive, cancellationToken),
            await collectionCases.CountAsync(item => item.Status == CollectionCaseStatus.LegalEscalated, cancellationToken),
            await legalNotices.CountAsync(item => item.Status == LegalNoticeStatus.Draft, cancellationToken),
            await legalNotices.CountAsync(item => item.Status == LegalNoticeStatus.Issued, cancellationToken),
            await paymentPlans.CountAsync(item => item.Status == PaymentPlanStatus.Active, cancellationToken),
            await paymentPlans.CountAsync(item => item.Status == PaymentPlanStatus.Broken, cancellationToken),
            await collectionCases
                .Where(item => item.Status == CollectionCaseStatus.Open
                    || item.Status == CollectionCaseStatus.Paused
                    || item.Status == CollectionCaseStatus.PaymentPlanActive
                    || item.Status == CollectionCaseStatus.LegalEscalated)
                .SumAsync(item => item.AmountDue, cancellationToken));

        return ServiceResult<CollectionsLegalComplianceSummaryResponse>.Success(response);
    }

    public async Task<ServiceResult<PenaltyRuleResponse>> CreatePenaltyRuleAsync(
        Guid? currentUserId,
        CreatePenaltyRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = TrimOrNull(request.Name);
        if (name is null)
        {
            return ServiceResult<PenaltyRuleResponse>.BadRequest("Penalty rule name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            return ServiceResult<PenaltyRuleResponse>.BadRequest("Penalty rule name is too long.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<PenaltyRuleResponse>.NotFound("Compound was not found.");
        }

        if (request.EffectiveFrom.HasValue
            && request.EffectiveUntil.HasValue
            && request.EffectiveUntil.Value < request.EffectiveFrom.Value)
        {
            return ServiceResult<PenaltyRuleResponse>.BadRequest("Penalty rule effective end date cannot be before the start date.");
        }

        var calculationValidation = ValidatePenaltyRuleCalculation(request);
        if (calculationValidation is not null)
        {
            return calculationValidation;
        }

        var duplicate = await dbContext.PenaltyRules.AnyAsync(rule =>
            rule.CompoundId == request.CompoundId
            && rule.Name == name
            && rule.TargetType == request.TargetType
            && rule.Status == PenaltyRuleStatus.Active,
            cancellationToken);
        if (duplicate)
        {
            return ServiceResult<PenaltyRuleResponse>.Conflict("An active penalty rule with the same name and target already exists.");
        }

        var rule = new PenaltyRule
        {
            CompoundId = request.CompoundId,
            Name = name,
            TargetType = request.TargetType,
            CalculationType = request.CalculationType,
            Status = request.Status,
            GracePeriodDays = request.GracePeriodDays,
            Amount = Math.Round(request.Amount, 2),
            PercentageRate = request.PercentageRate,
            MaxAmount = request.MaxAmount,
            PauseWhenDisputed = request.PauseWhenDisputed,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            CreatedByUserId = currentUserId
        };

        dbContext.PenaltyRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PenaltyRuleResponse>.Success(ToPenaltyRuleResponse(rule));
    }

    public async Task<PagedResult<PenaltyRuleResponse>> SearchPenaltyRulesAsync(
        PenaltyRuleQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var rules = dbContext.PenaltyRules
            .AsNoTracking()
            .ApplyCompoundAccess(scope, rule => rule.CompoundId);

        if (query.CompoundId.HasValue)
        {
            rules = rules.Where(rule => rule.CompoundId == query.CompoundId.Value);
        }

        if (query.Status.HasValue)
        {
            rules = rules.Where(rule => rule.Status == query.Status.Value);
        }

        if (query.TargetType.HasValue)
        {
            rules = rules.Where(rule => rule.TargetType == query.TargetType.Value);
        }

        return await ToPagedAsync(
            rules.OrderByDescending(rule => rule.CreatedAtUtc).Select(rule => ToPenaltyRuleResponse(rule)),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<CollectionCaseResponse>> CreateCollectionCaseAsync(
        Guid? currentUserId,
        CreateCollectionCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<CollectionCaseResponse>.BadRequest("Collection case reason is required.");
        }

        if (request.AmountDue <= 0)
        {
            return ServiceResult<CollectionCaseResponse>.BadRequest("Collection case amount must be greater than zero.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == request.ResidentProfileId && profile.IsActive, cancellationToken);
        if (resident is null || resident.CompoundId != request.CompoundId || !await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<CollectionCaseResponse>.NotFound("Resident profile was not found.");
        }

        var sourceValidation = await ValidateCollectionCaseSourceAsync(request, cancellationToken);
        if (!sourceValidation.IsValid)
        {
            return sourceValidation.Status == ServiceResultStatus.NotFound
                ? ServiceResult<CollectionCaseResponse>.NotFound(sourceValidation.Message)
                : ServiceResult<CollectionCaseResponse>.BadRequest(sourceValidation.Message);
        }

        if (request.SourceId.HasValue)
        {
            var duplicate = await dbContext.CollectionCases.AnyAsync(item =>
                item.CompoundId == request.CompoundId
                && item.ResidentProfileId == request.ResidentProfileId
                && item.SourceType == request.SourceType
                && item.SourceId == request.SourceId
                && item.Status != CollectionCaseStatus.Closed
                && item.Status != CollectionCaseStatus.Cancelled
                && item.Status != CollectionCaseStatus.Settled,
                cancellationToken);
            if (duplicate)
            {
                return ServiceResult<CollectionCaseResponse>.Conflict("An active collection case already exists for this source.");
            }
        }

        var collectionCase = new CollectionCase
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            AmountDue = Math.Round(request.AmountDue, 2),
            Currency = NormalizeCurrency(request.Currency),
            DueDate = request.DueDate,
            Reason = reason,
            Notes = TrimOrNull(request.Notes),
            AssignedToUserId = request.AssignedToUserId,
            CreatedByUserId = currentUserId,
            OpenedAtUtc = DateTime.UtcNow,
            LastActionAtUtc = DateTime.UtcNow
        };

        dbContext.CollectionCases.Add(collectionCase);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CollectionCaseResponse>.Success(
            ToCollectionCaseResponse(collectionCase, resident.FullName, legalNoticeCount: 0, paymentPlanCount: 0));
    }

    public async Task<PagedResult<CollectionCaseResponse>> SearchCollectionCasesAsync(
        CollectionCaseQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var cases = dbContext.CollectionCases
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Include(item => item.LegalNotices)
            .Include(item => item.PaymentPlans)
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        if (query.CompoundId.HasValue)
        {
            cases = cases.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            cases = cases.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.SourceType.HasValue)
        {
            cases = cases.Where(item => item.SourceType == query.SourceType.Value);
        }

        if (query.Stage.HasValue)
        {
            cases = cases.Where(item => item.Stage == query.Stage.Value);
        }

        if (query.Status.HasValue)
        {
            cases = cases.Where(item => item.Status == query.Status.Value);
        }

        return await ToPagedAsync(
            cases
                .OrderByDescending(item => item.OpenedAtUtc)
                .Select(item => ToCollectionCaseResponse(
                    item,
                    item.ResidentProfile.FullName,
                    item.LegalNotices.Count,
                    item.PaymentPlans.Count)),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<CollectionCaseResponse>> AdvanceCollectionCaseAsync(
        Guid id,
        Guid? currentUserId,
        AdvanceCollectionCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var collectionCase = await dbContext.CollectionCases
            .Include(item => item.ResidentProfile)
            .Include(item => item.LegalNotices)
            .Include(item => item.PaymentPlans)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (collectionCase is null || !await CanAccessCompoundAsync(collectionCase.CompoundId, cancellationToken))
        {
            return ServiceResult<CollectionCaseResponse>.NotFound("Collection case was not found.");
        }

        if (collectionCase.Status == CollectionCaseStatus.Closed
            || collectionCase.Status == CollectionCaseStatus.Cancelled
            || collectionCase.Status == CollectionCaseStatus.Settled)
        {
            return ServiceResult<CollectionCaseResponse>.Conflict("Closed collection cases cannot be advanced.");
        }

        collectionCase.Stage = request.CloseCase ? CollectionStage.Closed : request.NewStage;
        collectionCase.Notes = CombineNotes(collectionCase.Notes, request.Notes);
        collectionCase.LastActionAtUtc = DateTime.UtcNow;

        if (request.CloseCase)
        {
            collectionCase.Status = CollectionCaseStatus.Closed;
            collectionCase.ClosedAtUtc = DateTime.UtcNow;
            collectionCase.ClosedByUserId = currentUserId;
        }
        else if (request.NewStage == CollectionStage.LegalReview)
        {
            collectionCase.Status = CollectionCaseStatus.LegalEscalated;
        }
        else if (request.NewStage == CollectionStage.PaymentPlan)
        {
            collectionCase.Status = CollectionCaseStatus.PaymentPlanActive;
        }
        else
        {
            collectionCase.Status = CollectionCaseStatus.Open;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<CollectionCaseResponse>.Success(ToCollectionCaseResponse(
            collectionCase,
            collectionCase.ResidentProfile.FullName,
            collectionCase.LegalNotices.Count,
            collectionCase.PaymentPlans.Count));
    }

    public async Task<ServiceResult<LegalNoticeResponse>> CreateLegalNoticeAsync(
        Guid? currentUserId,
        CreateLegalNoticeRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = TrimOrNull(request.Title);
        var body = TrimOrNull(request.Body);
        if (title is null || body is null)
        {
            return ServiceResult<LegalNoticeResponse>.BadRequest("Legal notice title and body are required.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == request.ResidentProfileId && profile.IsActive, cancellationToken);
        if (resident is null || resident.CompoundId != request.CompoundId || !await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<LegalNoticeResponse>.NotFound("Resident profile was not found.");
        }

        if (request.CollectionCaseId.HasValue)
        {
            var collectionCaseValid = await dbContext.CollectionCases.AnyAsync(item =>
                item.Id == request.CollectionCaseId.Value
                && item.CompoundId == request.CompoundId
                && item.ResidentProfileId == request.ResidentProfileId,
                cancellationToken);
            if (!collectionCaseValid)
            {
                return ServiceResult<LegalNoticeResponse>.BadRequest("Collection case was not found for this resident.");
            }
        }

        var notice = new LegalNotice
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            CollectionCaseId = request.CollectionCaseId,
            NoticeType = request.NoticeType,
            Status = request.Status,
            Title = title,
            Body = body,
            DeliveryChannel = TrimOrNull(request.DeliveryChannel),
            DeliveryReference = TrimOrNull(request.DeliveryReference),
            DeadlineDate = request.DeadlineDate,
            CreatedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (notice.Status == LegalNoticeStatus.Issued)
        {
            notice.IssuedAtUtc = DateTime.UtcNow;
            notice.IssuedByUserId = currentUserId;
        }

        dbContext.LegalNotices.Add(notice);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<LegalNoticeResponse>.Success(ToLegalNoticeResponse(notice, resident.FullName));
    }

    public async Task<PagedResult<LegalNoticeResponse>> SearchLegalNoticesAsync(
        LegalNoticeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var notices = dbContext.LegalNotices
            .AsNoTracking()
            .Include(notice => notice.ResidentProfile)
            .ApplyCompoundAccess(scope, notice => notice.CompoundId);

        if (query.CompoundId.HasValue)
        {
            notices = notices.Where(notice => notice.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            notices = notices.Where(notice => notice.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.CollectionCaseId.HasValue)
        {
            notices = notices.Where(notice => notice.CollectionCaseId == query.CollectionCaseId.Value);
        }

        if (query.NoticeType.HasValue)
        {
            notices = notices.Where(notice => notice.NoticeType == query.NoticeType.Value);
        }

        if (query.Status.HasValue)
        {
            notices = notices.Where(notice => notice.Status == query.Status.Value);
        }

        return await ToPagedAsync(
            notices
                .OrderByDescending(notice => notice.CreatedAtUtc)
                .Select(notice => ToLegalNoticeResponse(notice, notice.ResidentProfile.FullName)),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<LegalNoticeResponse>> IssueLegalNoticeAsync(
        Guid id,
        Guid? currentUserId,
        IssueLegalNoticeRequest request,
        CancellationToken cancellationToken = default)
    {
        var notice = await dbContext.LegalNotices
            .Include(item => item.ResidentProfile)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notice is null || !await CanAccessCompoundAsync(notice.CompoundId, cancellationToken))
        {
            return ServiceResult<LegalNoticeResponse>.NotFound("Legal notice was not found.");
        }

        if (notice.Status != LegalNoticeStatus.Draft)
        {
            return ServiceResult<LegalNoticeResponse>.Conflict("Only draft legal notices can be issued.");
        }

        notice.Status = LegalNoticeStatus.Issued;
        notice.IssuedAtUtc = DateTime.UtcNow;
        notice.IssuedByUserId = currentUserId;
        notice.DeliveryChannel = TrimOrNull(request.DeliveryChannel) ?? notice.DeliveryChannel;
        notice.DeliveryReference = TrimOrNull(request.DeliveryReference) ?? notice.DeliveryReference;
        notice.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<LegalNoticeResponse>.Success(ToLegalNoticeResponse(notice, notice.ResidentProfile.FullName));
    }

    public async Task<ServiceResult<PaymentPlanResponse>> CreatePaymentPlanAsync(
        Guid? currentUserId,
        CreatePaymentPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TotalAmount <= 0)
        {
            return ServiceResult<PaymentPlanResponse>.BadRequest("Payment plan total amount must be greater than zero.");
        }

        if (request.InstallmentCount <= 0)
        {
            return ServiceResult<PaymentPlanResponse>.BadRequest("Payment plan must have at least one installment.");
        }

        var collectionCase = await dbContext.CollectionCases
            .Include(item => item.ResidentProfile)
            .FirstOrDefaultAsync(item => item.Id == request.CollectionCaseId, cancellationToken);
        if (collectionCase is null || !await CanAccessCompoundAsync(collectionCase.CompoundId, cancellationToken))
        {
            return ServiceResult<PaymentPlanResponse>.NotFound("Collection case was not found.");
        }

        if (collectionCase.Status == CollectionCaseStatus.Closed
            || collectionCase.Status == CollectionCaseStatus.Cancelled
            || collectionCase.Status == CollectionCaseStatus.Settled)
        {
            return ServiceResult<PaymentPlanResponse>.Conflict("Payment plans cannot be created for closed collection cases.");
        }

        var plan = new PaymentPlan
        {
            CompoundId = collectionCase.CompoundId,
            ResidentProfileId = collectionCase.ResidentProfileId,
            CollectionCaseId = collectionCase.Id,
            TotalAmount = Math.Round(request.TotalAmount, 2),
            Currency = NormalizeCurrency(request.Currency),
            InstallmentCount = request.InstallmentCount,
            StartDate = request.StartDate,
            Notes = TrimOrNull(request.Notes),
            CreatedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var baseAmount = Math.Round(plan.TotalAmount / plan.InstallmentCount, 2);
        var allocated = 0m;
        for (var i = 1; i <= plan.InstallmentCount; i++)
        {
            var amount = i == plan.InstallmentCount
                ? Math.Round(plan.TotalAmount - allocated, 2)
                : baseAmount;
            allocated += amount;
            plan.Installments.Add(new PaymentPlanInstallment
            {
                InstallmentNumber = i,
                DueDate = plan.StartDate.AddMonths(i - 1),
                Amount = amount
            });
        }

        collectionCase.Stage = CollectionStage.PaymentPlan;
        collectionCase.Status = CollectionCaseStatus.PaymentPlanActive;
        collectionCase.LastActionAtUtc = DateTime.UtcNow;

        dbContext.PaymentPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PaymentPlanResponse>.Success(ToPaymentPlanResponse(plan));
    }

    public async Task<ServiceResult<PaymentPlanResponse>> PayPaymentPlanInstallmentAsync(
        Guid paymentPlanId,
        Guid installmentId,
        PayPaymentPlanInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return ServiceResult<PaymentPlanResponse>.BadRequest("Payment amount must be greater than zero.");
        }

        var plan = await dbContext.PaymentPlans
            .Include(item => item.Installments)
            .Include(item => item.CollectionCase)
            .FirstOrDefaultAsync(item => item.Id == paymentPlanId, cancellationToken);
        if (plan is null || !await CanAccessCompoundAsync(plan.CompoundId, cancellationToken))
        {
            return ServiceResult<PaymentPlanResponse>.NotFound("Payment plan was not found.");
        }

        if (plan.Status != PaymentPlanStatus.Active)
        {
            return ServiceResult<PaymentPlanResponse>.Conflict("Only active payment plans can receive installment payments.");
        }

        var installment = plan.Installments.SingleOrDefault(item => item.Id == installmentId);
        if (installment is null)
        {
            return ServiceResult<PaymentPlanResponse>.NotFound("Payment plan installment was not found.");
        }

        var paymentAmount = Math.Round(request.Amount, 2);
        var outstandingAmount = Math.Round(installment.Amount - installment.PaidAmount, 2);
        if (paymentAmount > outstandingAmount)
        {
            return ServiceResult<PaymentPlanResponse>.BadRequest("Payment amount cannot exceed the outstanding installment amount.");
        }

        installment.PaidAmount = Math.Round(installment.PaidAmount + paymentAmount, 2);
        installment.Status = installment.PaidAmount >= installment.Amount
            ? PaymentPlanInstallmentStatus.Paid
            : PaymentPlanInstallmentStatus.PartiallyPaid;
        if (installment.Status == PaymentPlanInstallmentStatus.Paid)
        {
            installment.PaidAtUtc = DateTime.UtcNow;
        }

        if (plan.Installments.All(item => item.Status == PaymentPlanInstallmentStatus.Paid))
        {
            plan.Status = PaymentPlanStatus.Completed;
            plan.CollectionCase.Status = CollectionCaseStatus.Settled;
            plan.CollectionCase.Stage = CollectionStage.Settlement;
            plan.CollectionCase.ClosedAtUtc = DateTime.UtcNow;
        }

        plan.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PaymentPlanResponse>.Success(ToPaymentPlanResponse(plan));
    }


    public async Task<PagedResult<CollectionFollowUpQueueItemResponse>> GetCollectionFollowUpQueueAsync(
        CollectionFollowUpQueueQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var casesQuery = dbContext.CollectionCases
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Include(item => item.LegalNotices)
            .Include(item => item.PaymentPlans)
                .ThenInclude(plan => plan.Installments)
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        if (query.CompoundId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.AssignedToUserId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.AssignedToUserId == query.AssignedToUserId.Value);
        }

        if (query.Stage.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.Stage == query.Stage.Value);
        }

        if (query.Status.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.Status == query.Status.Value);
        }
        else
        {
            casesQuery = casesQuery.Where(item => item.Status != CollectionCaseStatus.Closed
                && item.Status != CollectionCaseStatus.Cancelled
                && item.Status != CollectionCaseStatus.Settled);
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);
        var rows = (await casesQuery.ToListAsync(cancellationToken))
            .Select(item => ToFollowUpQueueItem(item, today, now))
            .Where(item => item.DaysSinceLastAction >= query.MinDaysSinceLastAction);

        if (query.OnlyActionRequired)
        {
            rows = rows.Where(item => item.FollowUpPriority != "Low" || item.Reasons.Count > 0);
        }

        var ordered = rows
            .OrderByDescending(item => PriorityWeight(item.FollowUpPriority))
            .ThenByDescending(item => item.DaysOverdue ?? 0)
            .ThenByDescending(item => item.AmountDue)
            .ThenBy(item => item.NextPaymentPlanDueDate ?? DateOnly.MaxValue)
            .ToArray();

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pageItems = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedResult<CollectionFollowUpQueueItemResponse>(pageItems, pageNumber, pageSize, ordered.Length);
    }

    public async Task<ServiceResult<ResidentComplianceProfileResponse>> GetResidentComplianceProfileAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default)
    {
        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == residentProfileId && profile.IsActive, cancellationToken);
        if (resident is null || !await CanAccessCompoundAsync(resident.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentComplianceProfileResponse>.NotFound("Resident compliance profile was not found.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var utilityBills = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(item => item.ResidentProfileId == resident.Id && item.BillStatus != BillStatus.Cancelled)
            .Select(item => new OutstandingItemSnapshot(item.TotalAmount, item.PaidAmount, item.DueDate, item.BillStatus == BillStatus.Overdue))
            .ToListAsync(cancellationToken);

        var rentInvoices = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(item => item.ResidentProfileId == resident.Id && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .Select(item => new OutstandingItemSnapshot(item.TotalAmount, item.PaidAmount, item.DueDate, item.RentInvoiceStatus == RentInvoiceStatus.Overdue))
            .ToListAsync(cancellationToken);

        var installments = await dbContext.InstallmentScheduleItems
            .AsNoTracking()
            .Where(item => item.ResidentProfileId == resident.Id && item.InstallmentStatus != InstallmentStatus.Cancelled)
            .Select(item => new OutstandingItemSnapshot(item.Amount, item.PaidAmount, item.DueDate, item.InstallmentStatus == InstallmentStatus.Overdue))
            .ToListAsync(cancellationToken);

        var fines = await dbContext.ViolationFines
            .AsNoTracking()
            .Where(item => item.ResidentProfileId == resident.Id && item.Status != ViolationFineStatus.Cancelled)
            .Select(item => new OutstandingItemSnapshot(item.Amount, item.PaidAmount, item.DueDate, false))
            .ToListAsync(cancellationToken);

        var allOutstanding = utilityBills.Concat(rentInvoices).Concat(installments).Concat(fines).ToArray();
        var totalOutstanding = allOutstanding.Sum(item => item.OutstandingAmount);
        var overdueItems = allOutstanding.Where(item => item.OutstandingAmount > 0 && (item.IsExplicitlyOverdue || item.DueDate < today)).ToArray();
        var overdueAmount = overdueItems.Sum(item => item.OutstandingAmount);

        var openDisputes = await dbContext.FinancialDisputes.CountAsync(item =>
            item.ResidentProfileId == resident.Id
            && item.Status != FinancialDisputeStatus.Resolved
            && item.Status != FinancialDisputeStatus.Rejected
            && item.Status != FinancialDisputeStatus.Cancelled,
            cancellationToken);

        var openAppeals = await dbContext.ViolationAppeals.CountAsync(item =>
            item.ResidentProfileId == resident.Id
            && item.Status != ViolationAppealStatus.Accepted
            && item.Status != ViolationAppealStatus.Rejected
            && item.Status != ViolationAppealStatus.FineCancelled
            && item.Status != ViolationAppealStatus.FineReduced
            && item.Status != ViolationAppealStatus.Cancelled,
            cancellationToken);

        var openCollections = await dbContext.CollectionCases.CountAsync(item =>
            item.ResidentProfileId == resident.Id
            && item.Status != CollectionCaseStatus.Closed
            && item.Status != CollectionCaseStatus.Cancelled
            && item.Status != CollectionCaseStatus.Settled,
            cancellationToken);

        var activeLegalNotices = await dbContext.LegalNotices.CountAsync(item =>
            item.ResidentProfileId == resident.Id
            && (item.Status == LegalNoticeStatus.Issued || item.Status == LegalNoticeStatus.Delivered),
            cancellationToken);

        var activePaymentPlans = await dbContext.PaymentPlans.CountAsync(item =>
            item.ResidentProfileId == resident.Id
            && item.Status == PaymentPlanStatus.Active,
            cancellationToken);

        var reasons = new List<string>();
        if (overdueItems.Length > 0)
        {
            reasons.Add($"{overdueItems.Length} overdue financial item(s).");
        }
        if (openDisputes > 0)
        {
            reasons.Add($"{openDisputes} open financial dispute(s).");
        }
        if (openAppeals > 0)
        {
            reasons.Add($"{openAppeals} open violation appeal(s).");
        }
        if (openCollections > 0)
        {
            reasons.Add($"{openCollections} active collection case(s).");
        }
        if (activeLegalNotices > 0)
        {
            reasons.Add($"{activeLegalNotices} active legal notice(s).");
        }

        var status = activeLegalNotices > 0 || openCollections > 0 || overdueAmount >= 1_000_000m
            ? "Critical"
            : overdueAmount > 0 || openDisputes > 0 || openAppeals > 0
                ? "Watch"
                : "Compliant";

        if (reasons.Count == 0)
        {
            reasons.Add("No active financial, collection, or legal compliance issues.");
        }

        return ServiceResult<ResidentComplianceProfileResponse>.Success(new ResidentComplianceProfileResponse(
            resident.Id,
            resident.FullName,
            resident.CompoundId,
            totalOutstanding,
            overdueAmount,
            overdueItems.Length,
            openDisputes,
            openAppeals,
            openCollections,
            activeLegalNotices,
            activePaymentPlans,
            status,
            reasons));
    }


    public async Task<ServiceResult<LegalCaseManagementDashboardResponse>> GetLegalCaseManagementDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<LegalCaseManagementDashboardResponse>.Forbidden("Current user cannot access legal case management.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<LegalCaseManagementDashboardResponse>.NotFound("Compound was not found.");
        }

        var casesQuery = dbContext.CollectionCases
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Include(item => item.LegalNotices)
            .Include(item => item.PaymentPlans)
                .ThenInclude(plan => plan.Installments)
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        var noticesQuery = dbContext.LegalNotices
            .AsNoTracking()
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        if (compoundId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.CompoundId == compoundId.Value);
            noticesQuery = noticesQuery.Where(item => item.CompoundId == compoundId.Value);
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);
        var cases = await casesQuery.ToListAsync(cancellationToken);
        var openCases = cases.Where(IsOpenCollectionCase).ToArray();
        var notices = await noticesQuery.ToListAsync(cancellationToken);
        var legalQueueItems = openCases.Select(item => ToLegalEscalationQueueItem(item, today, now)).ToArray();
        var executiveAlerts = BuildLegalDashboardAlerts(openCases, notices, legalQueueItems, today);

        var response = new LegalCaseManagementDashboardResponse(
            compoundId,
            openCases.Length,
            openCases.Count(item => item.Status == CollectionCaseStatus.LegalEscalated || item.Stage == CollectionStage.LegalReview),
            legalQueueItems.Count(item => item.IsReadyForLegalEscalation),
            notices.Count(IsActiveLegalNotice),
            notices.Count(item => IsActiveLegalNotice(item) && item.DeadlineDate.HasValue && item.DeadlineDate.Value < today),
            notices.Count(item => item.Status == LegalNoticeStatus.Draft),
            openCases.Count(item => item.PaymentPlans.Any(plan => plan.Status == PaymentPlanStatus.Broken)),
            legalQueueItems.Count(item => item.LegalPriority == "Critical" || item.LegalPriority == "High"),
            openCases.Sum(item => item.AmountDue),
            openCases.Length == 0 ? 0 : openCases.Max(item => Math.Max(0, (int)Math.Floor((now - item.OpenedAtUtc).TotalDays))),
            executiveAlerts);

        return ServiceResult<LegalCaseManagementDashboardResponse>.Success(response);
    }

    public async Task<PagedResult<LegalCaseEscalationQueueItemResponse>> GetLegalCaseEscalationQueueAsync(
        LegalCaseEscalationQueueQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var casesQuery = dbContext.CollectionCases
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Include(item => item.LegalNotices)
            .Include(item => item.PaymentPlans)
                .ThenInclude(plan => plan.Installments)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .Where(IsOpenCollectionCaseExpression());

        if (query.CompoundId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.AssignedToUserId.HasValue)
        {
            casesQuery = casesQuery.Where(item => item.AssignedToUserId == query.AssignedToUserId.Value);
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);
        var items = (await casesQuery.ToListAsync(cancellationToken))
            .Select(item => ToLegalEscalationQueueItem(item, today, now))
            .Where(item => (item.DaysOverdue ?? 0) >= query.MinDaysOverdue);

        if (query.OnlyReadyForEscalation)
        {
            items = items.Where(item => item.IsReadyForLegalEscalation);
        }

        var ordered = items
            .OrderByDescending(item => LegalPriorityWeight(item.LegalPriority))
            .ThenByDescending(item => item.DaysOverdue ?? 0)
            .ThenByDescending(item => item.AmountDue)
            .ThenBy(item => item.LastActionAtUtc ?? item.OpenedAtUtc)
            .ToArray();

        return ToPagedInMemory(ordered, query);
    }

    public async Task<PagedResult<LegalNoticeServiceQueueItemResponse>> GetLegalNoticeServiceQueueAsync(
        LegalNoticeServiceQueueQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var noticesQuery = dbContext.LegalNotices
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Include(item => item.CollectionCase)
            .ApplyCompoundAccess(scope, item => item.CompoundId);

        if (query.CompoundId.HasValue)
        {
            noticesQuery = noticesQuery.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            noticesQuery = noticesQuery.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.CollectionCaseId.HasValue)
        {
            noticesQuery = noticesQuery.Where(item => item.CollectionCaseId == query.CollectionCaseId.Value);
        }

        if (query.Status.HasValue)
        {
            noticesQuery = noticesQuery.Where(item => item.Status == query.Status.Value);
        }
        else
        {
            noticesQuery = noticesQuery.Where(item => item.Status != LegalNoticeStatus.Cancelled && item.Status != LegalNoticeStatus.Expired);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var items = (await noticesQuery.ToListAsync(cancellationToken))
            .Select(item => ToLegalNoticeServiceQueueItem(item, today, query.DeadlineWithinDays));

        if (query.OnlyActionRequired)
        {
            items = items.Where(item => item.IsActionRequired);
        }

        var ordered = items
            .OrderByDescending(item => LegalPriorityWeight(item.ServicePriority))
            .ThenBy(item => item.DaysToDeadline ?? int.MaxValue)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToArray();

        return ToPagedInMemory(ordered, query);
    }

    public async Task<ServiceResult<LegalCaseFileResponse>> GetLegalCaseFileAsync(
        Guid collectionCaseId,
        CancellationToken cancellationToken = default)
    {
        var collectionCase = await FindLegalCaseWithDetailsAsync(collectionCaseId, cancellationToken);
        if (collectionCase is null)
        {
            return ServiceResult<LegalCaseFileResponse>.NotFound("Legal case file was not found.");
        }

        if (!await CanAccessCompoundAsync(collectionCase.CompoundId, cancellationToken))
        {
            return ServiceResult<LegalCaseFileResponse>.NotFound("Legal case file was not found.");
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);
        return ServiceResult<LegalCaseFileResponse>.Success(ToLegalCaseFileResponse(collectionCase, today, now));
    }

    public async Task<ServiceResult<IReadOnlyCollection<LegalCaseTimelineEventResponse>>> GetLegalCaseTimelineAsync(
        Guid collectionCaseId,
        CancellationToken cancellationToken = default)
    {
        var collectionCase = await FindLegalCaseWithDetailsAsync(collectionCaseId, cancellationToken);
        if (collectionCase is null)
        {
            return ServiceResult<IReadOnlyCollection<LegalCaseTimelineEventResponse>>.NotFound("Legal case timeline was not found.");
        }

        if (!await CanAccessCompoundAsync(collectionCase.CompoundId, cancellationToken))
        {
            return ServiceResult<IReadOnlyCollection<LegalCaseTimelineEventResponse>>.NotFound("Legal case timeline was not found.");
        }

        var timeline = BuildLegalCaseTimeline(collectionCase).ToArray();
        return ServiceResult<IReadOnlyCollection<LegalCaseTimelineEventResponse>>.Success(timeline);
    }


    private static CollectionFollowUpQueueItemResponse ToFollowUpQueueItem(
        CollectionCase collectionCase,
        DateOnly today,
        DateTime now)
    {
        var lastActionAt = collectionCase.LastActionAtUtc ?? collectionCase.OpenedAtUtc;
        var daysSinceLastAction = Math.Max(0, (int)Math.Floor((now - lastActionAt).TotalDays));
        var daysOverdue = collectionCase.DueDate.HasValue
            ? Math.Max(0, today.DayNumber - collectionCase.DueDate.Value.DayNumber)
            : (int?)null;

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

        var reasons = new List<string>();
        if (daysOverdue is > 0)
        {
            reasons.Add($"Collection case is {daysOverdue.Value} day(s) overdue.");
        }

        if (daysSinceLastAction >= 14)
        {
            reasons.Add($"No collection action recorded for {daysSinceLastAction} day(s).");
        }

        if (hasBrokenPlan)
        {
            reasons.Add("A related payment plan is marked as broken.");
        }

        if (nextInstallment is not null && nextInstallment.DueDate <= today)
        {
            reasons.Add("The next payment plan installment is due or overdue.");
        }

        if (activeLegalNoticeCount > 0)
        {
            reasons.Add($"{activeLegalNoticeCount} active legal notice(s) require follow-up.");
        }

        if (collectionCase.Status == CollectionCaseStatus.LegalEscalated)
        {
            reasons.Add("Collection case is already legal-escalated.");
        }

        var priority = DetermineFollowUpPriority(collectionCase, daysOverdue, daysSinceLastAction, hasBrokenPlan, nextInstallment, activeLegalNoticeCount, today);
        var action = DetermineRecommendedAction(collectionCase, daysOverdue, hasActivePlan, hasBrokenPlan, nextInstallment, activeLegalNoticeCount, today);

        return new CollectionFollowUpQueueItemResponse(
            collectionCase.Id,
            collectionCase.CompoundId,
            collectionCase.ResidentProfileId,
            collectionCase.ResidentProfile.FullName,
            collectionCase.SourceType,
            collectionCase.SourceId,
            collectionCase.Stage,
            collectionCase.Status,
            collectionCase.AmountDue,
            collectionCase.Currency,
            collectionCase.DueDate,
            daysOverdue,
            collectionCase.OpenedAtUtc,
            collectionCase.LastActionAtUtc,
            daysSinceLastAction,
            collectionCase.AssignedToUserId,
            collectionCase.LegalNotices.Count,
            collectionCase.PaymentPlans.Count,
            hasActivePlan,
            hasBrokenPlan,
            nextInstallment?.DueDate,
            nextInstallment is null ? null : Math.Max(0, nextInstallment.Amount - nextInstallment.PaidAmount),
            priority,
            action,
            reasons);
    }

    private static string DetermineFollowUpPriority(
        CollectionCase collectionCase,
        int? daysOverdue,
        int daysSinceLastAction,
        bool hasBrokenPlan,
        PaymentPlanInstallment? nextInstallment,
        int activeLegalNoticeCount,
        DateOnly today)
    {
        if (collectionCase.Status == CollectionCaseStatus.LegalEscalated
            || hasBrokenPlan
            || activeLegalNoticeCount > 0
            || daysOverdue is >= 60
            || collectionCase.AmountDue >= 1_000_000m
            || (nextInstallment is not null && nextInstallment.DueDate < today))
        {
            return "High";
        }

        if (daysOverdue is >= 30
            || daysSinceLastAction >= 14
            || (nextInstallment is not null && nextInstallment.DueDate == today)
            || collectionCase.AmountDue >= 250_000m)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string DetermineRecommendedAction(
        CollectionCase collectionCase,
        int? daysOverdue,
        bool hasActivePlan,
        bool hasBrokenPlan,
        PaymentPlanInstallment? nextInstallment,
        int activeLegalNoticeCount,
        DateOnly today)
    {
        if (hasBrokenPlan)
        {
            return "Review the broken payment plan and decide whether to renegotiate, escalate, or issue a legal notice.";
        }

        if (nextInstallment is not null && nextInstallment.DueDate < today)
        {
            return "Contact the resident about the overdue payment plan installment before escalating the collection case.";
        }

        if (nextInstallment is not null && nextInstallment.DueDate == today)
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

    private async Task<CollectionCase?> FindLegalCaseWithDetailsAsync(
        Guid collectionCaseId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CollectionCases
            .Include(item => item.ResidentProfile)
            .Include(item => item.LegalNotices)
            .Include(item => item.PaymentPlans)
                .ThenInclude(plan => plan.Installments)
            .FirstOrDefaultAsync(item => item.Id == collectionCaseId, cancellationToken);
    }

    private static Expression<Func<CollectionCase, bool>> IsOpenCollectionCaseExpression()
    {
        return item => item.Status != CollectionCaseStatus.Closed
            && item.Status != CollectionCaseStatus.Cancelled
            && item.Status != CollectionCaseStatus.Settled;
    }

    private static bool IsOpenCollectionCase(CollectionCase collectionCase)
    {
        return collectionCase.Status != CollectionCaseStatus.Closed
            && collectionCase.Status != CollectionCaseStatus.Cancelled
            && collectionCase.Status != CollectionCaseStatus.Settled;
    }

    private static bool IsActiveLegalNotice(LegalNotice notice)
    {
        return notice.Status == LegalNoticeStatus.Issued
            || notice.Status == LegalNoticeStatus.Delivered
            || notice.Status == LegalNoticeStatus.Acknowledged;
    }

    private static LegalCaseEscalationQueueItemResponse ToLegalEscalationQueueItem(
        CollectionCase collectionCase,
        DateOnly today,
        DateTime now)
    {
        var activeLegalNoticeCount = collectionCase.LegalNotices.Count(IsActiveLegalNotice);
        var hasFinalNotice = collectionCase.LegalNotices.Any(notice =>
            notice.NoticeType == LegalNoticeType.FinalPaymentNotice
            || notice.NoticeType == LegalNoticeType.LegalEscalationNotice
            || notice.NoticeType == LegalNoticeType.EvictionNoticePlaceholder);
        var hasExpiredActiveNotice = collectionCase.LegalNotices.Any(notice =>
            IsActiveLegalNotice(notice)
            && notice.DeadlineDate.HasValue
            && notice.DeadlineDate.Value < today);
        var hasBrokenPaymentPlan = collectionCase.PaymentPlans.Any(plan => plan.Status == PaymentPlanStatus.Broken);
        var hasActivePaymentPlan = collectionCase.PaymentPlans.Any(plan => plan.Status == PaymentPlanStatus.Active);
        var daysOverdue = GetDaysOverdue(collectionCase.DueDate, today);
        var caseAgeDays = Math.Max(0, (int)Math.Floor((now - collectionCase.OpenedAtUtc).TotalDays));

        var readinessReasons = new List<string>();
        var blockingIssues = new List<string>();

        if (collectionCase.Status == CollectionCaseStatus.LegalEscalated || collectionCase.Stage == CollectionStage.LegalReview)
        {
            readinessReasons.Add("Collection case is already in legal review/escalated status.");
        }

        if (daysOverdue is >= 60)
        {
            readinessReasons.Add($"Collection case is {daysOverdue.Value} day(s) overdue.");
        }

        if (collectionCase.AmountDue >= 1_000_000m)
        {
            readinessReasons.Add("Outstanding amount is above the legal escalation threshold.");
        }

        if (hasExpiredActiveNotice)
        {
            readinessReasons.Add("An active legal notice deadline has expired.");
        }

        if (hasBrokenPaymentPlan)
        {
            readinessReasons.Add("A related payment plan is broken.");
        }

        if (hasActivePaymentPlan && !hasBrokenPaymentPlan && collectionCase.Status != CollectionCaseStatus.LegalEscalated)
        {
            blockingIssues.Add("An active payment plan exists; review plan compliance before legal escalation.");
        }

        if (!hasFinalNotice && collectionCase.Stage != CollectionStage.LegalReview && collectionCase.Status != CollectionCaseStatus.LegalEscalated)
        {
            blockingIssues.Add("No final/legal escalation notice exists for this case.");
        }

        if (collectionCase.AmountDue <= 0)
        {
            blockingIssues.Add("Collection case amount must be greater than zero before legal escalation.");
        }

        if (readinessReasons.Count == 0)
        {
            readinessReasons.Add("Case requires routine collection follow-up before legal escalation.");
        }

        var isReady = blockingIssues.Count == 0
            && (collectionCase.Status == CollectionCaseStatus.LegalEscalated
                || collectionCase.Stage == CollectionStage.LegalReview
                || daysOverdue is >= 60
                || collectionCase.AmountDue >= 1_000_000m
                || hasExpiredActiveNotice
                || hasBrokenPaymentPlan);

        var priority = DetermineLegalPriority(collectionCase, daysOverdue, activeLegalNoticeCount, hasExpiredActiveNotice, hasBrokenPaymentPlan, isReady);
        var action = DetermineLegalRecommendedAction(collectionCase, daysOverdue, hasFinalNotice, hasExpiredActiveNotice, hasBrokenPaymentPlan, hasActivePaymentPlan, isReady);

        return new LegalCaseEscalationQueueItemResponse(
            collectionCase.Id,
            collectionCase.CompoundId,
            collectionCase.ResidentProfileId,
            collectionCase.ResidentProfile.FullName,
            collectionCase.SourceType,
            collectionCase.SourceId,
            collectionCase.Stage,
            collectionCase.Status,
            collectionCase.AmountDue,
            collectionCase.Currency,
            collectionCase.DueDate,
            daysOverdue,
            caseAgeDays,
            collectionCase.OpenedAtUtc,
            collectionCase.LastActionAtUtc,
            collectionCase.AssignedToUserId,
            collectionCase.LegalNotices.Count,
            activeLegalNoticeCount,
            collectionCase.PaymentPlans.Count,
            hasBrokenPaymentPlan,
            isReady,
            priority,
            action,
            readinessReasons,
            blockingIssues);
    }

    private static LegalNoticeServiceQueueItemResponse ToLegalNoticeServiceQueueItem(
        LegalNotice notice,
        DateOnly today,
        int deadlineWithinDays)
    {
        var daysToDeadline = notice.DeadlineDate.HasValue
            ? notice.DeadlineDate.Value.DayNumber - today.DayNumber
            : (int?)null;
        var isOverdue = daysToDeadline.HasValue && daysToDeadline.Value < 0;
        var actionRequired = notice.Status == LegalNoticeStatus.Draft
            || notice.Status == LegalNoticeStatus.Issued
            || notice.Status == LegalNoticeStatus.Delivered
            || isOverdue
            || (daysToDeadline.HasValue && daysToDeadline.Value <= deadlineWithinDays);
        var priority = DetermineLegalNoticeServicePriority(notice, daysToDeadline, isOverdue);
        var action = DetermineLegalNoticeServiceAction(notice, daysToDeadline, isOverdue);

        return new LegalNoticeServiceQueueItemResponse(
            notice.Id,
            notice.CompoundId,
            notice.ResidentProfileId,
            notice.ResidentProfile.FullName,
            notice.CollectionCaseId,
            notice.NoticeType,
            notice.Status,
            notice.Title,
            notice.DeadlineDate,
            daysToDeadline,
            isOverdue,
            actionRequired,
            priority,
            action,
            notice.DeliveryChannel,
            notice.DeliveryReference,
            notice.CreatedAtUtc,
            notice.IssuedAtUtc);
    }

    private static LegalCaseFileResponse ToLegalCaseFileResponse(
        CollectionCase collectionCase,
        DateOnly today,
        DateTime now)
    {
        var queueItem = ToLegalEscalationQueueItem(collectionCase, today, now);
        var notices = collectionCase.LegalNotices
            .OrderByDescending(notice => notice.CreatedAtUtc)
            .Select(notice => new LegalCaseFileNoticeResponse(
                notice.Id,
                notice.NoticeType,
                notice.Status,
                notice.Title,
                notice.DeadlineDate,
                notice.DeliveryChannel,
                notice.DeliveryReference,
                notice.CreatedAtUtc,
                notice.IssuedAtUtc))
            .ToArray();
        var paymentPlans = collectionCase.PaymentPlans
            .OrderByDescending(plan => plan.CreatedAtUtc)
            .Select(plan => ToLegalCaseFilePaymentPlanResponse(plan, today))
            .ToArray();
        var brokenPlanCount = collectionCase.PaymentPlans.Count(plan => plan.Status == PaymentPlanStatus.Broken);

        return new LegalCaseFileResponse(
            collectionCase.Id,
            collectionCase.CompoundId,
            collectionCase.ResidentProfileId,
            collectionCase.ResidentProfile.FullName,
            collectionCase.SourceType,
            collectionCase.SourceId,
            collectionCase.Stage,
            collectionCase.Status,
            collectionCase.AmountDue,
            collectionCase.Currency,
            collectionCase.DueDate,
            queueItem.DaysOverdue,
            queueItem.CaseAgeDays,
            collectionCase.AssignedToUserId,
            collectionCase.Reason,
            collectionCase.Notes,
            collectionCase.LegalNotices.Count,
            queueItem.ActiveLegalNoticeCount,
            collectionCase.PaymentPlans.Count,
            brokenPlanCount,
            queueItem.IsReadyForLegalEscalation,
            queueItem.LegalPriority,
            queueItem.RecommendedLegalAction,
            queueItem.ReadinessReasons,
            queueItem.BlockingIssues,
            notices,
            paymentPlans,
            BuildLegalCaseTimeline(collectionCase).ToArray());
    }

    private static LegalCaseFilePaymentPlanResponse ToLegalCaseFilePaymentPlanResponse(
        PaymentPlan plan,
        DateOnly today)
    {
        var unpaidInstallments = plan.Installments
            .Where(installment => installment.Status != PaymentPlanInstallmentStatus.Paid
                && installment.Status != PaymentPlanInstallmentStatus.Cancelled)
            .ToArray();
        var outstanding = unpaidInstallments.Sum(installment => Math.Max(0, installment.Amount - installment.PaidAmount));
        var overdueInstallments = unpaidInstallments.Count(installment => installment.DueDate < today);
        var nextDueDate = unpaidInstallments
            .OrderBy(installment => installment.DueDate)
            .Select(installment => (DateOnly?)installment.DueDate)
            .FirstOrDefault();

        return new LegalCaseFilePaymentPlanResponse(
            plan.Id,
            plan.Status,
            plan.TotalAmount,
            outstanding,
            plan.InstallmentCount,
            overdueInstallments,
            nextDueDate);
    }

    private static IReadOnlyCollection<LegalCaseTimelineEventResponse> BuildLegalCaseTimeline(CollectionCase collectionCase)
    {
        var events = new List<LegalCaseTimelineEventResponse>
        {
            new(
                collectionCase.OpenedAtUtc,
                "CollectionCaseOpened",
                "Collection case opened",
                collectionCase.Reason,
                "Info")
        };

        if (collectionCase.DueDate.HasValue)
        {
            events.Add(new LegalCaseTimelineEventResponse(
                collectionCase.DueDate.Value.ToDateTime(TimeOnly.MinValue),
                "DebtDueDate",
                "Collection due date",
                $"Debt due date for {collectionCase.AmountDue:N2} {collectionCase.Currency}.",
                "Warning"));
        }

        foreach (var notice in collectionCase.LegalNotices)
        {
            events.Add(new LegalCaseTimelineEventResponse(
                notice.CreatedAtUtc,
                "LegalNoticeCreated",
                $"Legal notice created: {notice.NoticeType}",
                notice.Title,
                notice.Status == LegalNoticeStatus.Draft ? "Info" : "Warning"));

            if (notice.IssuedAtUtc.HasValue)
            {
                events.Add(new LegalCaseTimelineEventResponse(
                    notice.IssuedAtUtc.Value,
                    "LegalNoticeIssued",
                    $"Legal notice issued: {notice.NoticeType}",
                    notice.DeliveryReference ?? notice.DeliveryChannel ?? "Legal notice issued.",
                    "Warning"));
            }

            if (notice.DeadlineDate.HasValue)
            {
                events.Add(new LegalCaseTimelineEventResponse(
                    notice.DeadlineDate.Value.ToDateTime(TimeOnly.MinValue),
                    "LegalNoticeDeadline",
                    $"Legal notice deadline: {notice.NoticeType}",
                    notice.Title,
                    notice.Status == LegalNoticeStatus.Expired ? "Critical" : "Warning"));
            }
        }

        foreach (var plan in collectionCase.PaymentPlans)
        {
            events.Add(new LegalCaseTimelineEventResponse(
                plan.CreatedAtUtc,
                "PaymentPlanCreated",
                $"Payment plan created: {plan.Status}",
                $"Payment plan total: {plan.TotalAmount:N2} {plan.Currency}.",
                plan.Status == PaymentPlanStatus.Broken ? "Critical" : "Info"));

            foreach (var installment in plan.Installments)
            {
                events.Add(new LegalCaseTimelineEventResponse(
                    installment.DueDate.ToDateTime(TimeOnly.MinValue),
                    "PaymentPlanInstallmentDue",
                    $"Installment #{installment.InstallmentNumber} due",
                    $"Amount: {installment.Amount:N2}, paid: {installment.PaidAmount:N2}, status: {installment.Status}.",
                    installment.Status == PaymentPlanInstallmentStatus.Overdue ? "Critical" : "Info"));
            }
        }

        if (collectionCase.LastActionAtUtc.HasValue)
        {
            events.Add(new LegalCaseTimelineEventResponse(
                collectionCase.LastActionAtUtc.Value,
                "LastCollectionAction",
                "Last collection/legal action",
                collectionCase.Notes ?? "Collection case action was recorded.",
                "Info"));
        }

        if (collectionCase.ClosedAtUtc.HasValue)
        {
            events.Add(new LegalCaseTimelineEventResponse(
                collectionCase.ClosedAtUtc.Value,
                "CollectionCaseClosed",
                "Collection case closed",
                $"Final status: {collectionCase.Status}.",
                "Info"));
        }

        return events
            .OrderBy(item => item.EventAtUtc)
            .ThenBy(item => item.EventType)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildLegalDashboardAlerts(
        IReadOnlyCollection<CollectionCase> openCases,
        IReadOnlyCollection<LegalNotice> notices,
        IReadOnlyCollection<LegalCaseEscalationQueueItemResponse> queueItems,
        DateOnly today)
    {
        var alerts = new List<string>();
        var overdueNotices = notices.Count(item => IsActiveLegalNotice(item) && item.DeadlineDate.HasValue && item.DeadlineDate.Value < today);
        var criticalCases = queueItems.Count(item => item.LegalPriority == "Critical");
        var readyCases = queueItems.Count(item => item.IsReadyForLegalEscalation);
        var brokenPlans = openCases.Count(item => item.PaymentPlans.Any(plan => plan.Status == PaymentPlanStatus.Broken));

        if (criticalCases > 0)
        {
            alerts.Add($"{criticalCases} critical legal case(s) require immediate management attention.");
        }

        if (readyCases > 0)
        {
            alerts.Add($"{readyCases} case(s) are ready for legal escalation review.");
        }

        if (overdueNotices > 0)
        {
            alerts.Add($"{overdueNotices} active legal notice deadline(s) are overdue.");
        }

        if (brokenPlans > 0)
        {
            alerts.Add($"{brokenPlans} open case(s) have broken payment plans.");
        }

        if (alerts.Count == 0)
        {
            alerts.Add("No urgent legal case management alerts.");
        }

        return alerts;
    }

    private static int? GetDaysOverdue(DateOnly? dueDate, DateOnly today)
    {
        if (!dueDate.HasValue)
        {
            return null;
        }

        return Math.Max(0, today.DayNumber - dueDate.Value.DayNumber);
    }

    private static string DetermineLegalPriority(
        CollectionCase collectionCase,
        int? daysOverdue,
        int activeLegalNoticeCount,
        bool hasExpiredActiveNotice,
        bool hasBrokenPaymentPlan,
        bool isReady)
    {
        if (hasExpiredActiveNotice
            || hasBrokenPaymentPlan
            || collectionCase.Status == CollectionCaseStatus.LegalEscalated
            || daysOverdue is >= 120
            || collectionCase.AmountDue >= 2_500_000m)
        {
            return "Critical";
        }

        if (isReady || activeLegalNoticeCount > 0 || daysOverdue is >= 60 || collectionCase.AmountDue >= 1_000_000m)
        {
            return "High";
        }

        if (daysOverdue is >= 30 || collectionCase.AmountDue >= 250_000m)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string DetermineLegalRecommendedAction(
        CollectionCase collectionCase,
        int? daysOverdue,
        bool hasFinalNotice,
        bool hasExpiredActiveNotice,
        bool hasBrokenPaymentPlan,
        bool hasActivePaymentPlan,
        bool isReady)
    {
        if (hasBrokenPaymentPlan)
        {
            return "Review the broken payment plan and prepare the case for legal escalation or renegotiation approval.";
        }

        if (hasExpiredActiveNotice)
        {
            return "Escalate to legal case review because an active legal notice deadline has expired.";
        }

        if (collectionCase.Status == CollectionCaseStatus.LegalEscalated || collectionCase.Stage == CollectionStage.LegalReview)
        {
            return "Prepare the legal case file, verify documents, and record the next legal action.";
        }

        if (hasActivePaymentPlan)
        {
            return "Monitor active payment plan compliance before initiating legal escalation.";
        }

        if (!hasFinalNotice && daysOverdue is >= 30)
        {
            return "Create and issue a final legal notice before escalation.";
        }

        if (isReady)
        {
            return "Move the collection case to legal review after manager validation.";
        }

        return "Continue collection follow-up and update the case after the next resident contact.";
    }

    private static string DetermineLegalNoticeServicePriority(
        LegalNotice notice,
        int? daysToDeadline,
        bool isOverdue)
    {
        if (isOverdue || notice.Status == LegalNoticeStatus.Expired)
        {
            return "Critical";
        }

        if (notice.Status == LegalNoticeStatus.Draft
            || notice.NoticeType == LegalNoticeType.FinalPaymentNotice
            || notice.NoticeType == LegalNoticeType.LegalEscalationNotice
            || daysToDeadline is <= 3)
        {
            return "High";
        }

        if (daysToDeadline is <= 14 || notice.Status == LegalNoticeStatus.Issued)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string DetermineLegalNoticeServiceAction(
        LegalNotice notice,
        int? daysToDeadline,
        bool isOverdue)
    {
        if (notice.Status == LegalNoticeStatus.Draft)
        {
            return "Review the draft, verify resident and case information, then issue the notice.";
        }

        if (isOverdue)
        {
            return "Deadline is overdue; review resident response and escalate the related collection case if required.";
        }

        if (notice.Status == LegalNoticeStatus.Issued && string.IsNullOrWhiteSpace(notice.DeliveryReference))
        {
            return "Record delivery reference/proof for the issued legal notice.";
        }

        if (notice.Status == LegalNoticeStatus.Delivered || notice.Status == LegalNoticeStatus.Acknowledged)
        {
            return "Monitor resident response and prepare the next legal action after the deadline.";
        }

        if (daysToDeadline is <= 7)
        {
            return "Follow up before the legal notice deadline.";
        }

        return "Keep the notice under routine monitoring.";
    }

    private static int LegalPriorityWeight(string priority)
    {
        return priority switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            _ => 1
        };
    }

    private static PagedResult<T> ToPagedInMemory<T>(
        IReadOnlyCollection<T> orderedItems,
        PaginationQuery pagination)
    {
        var pageNumber = Math.Max(1, pagination.PageNumber);
        var pageSize = Math.Clamp(pagination.PageSize, 1, 100);
        var pageItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedResult<T>(pageItems, pageNumber, pageSize, orderedItems.Count);
    }


    private static int PriorityWeight(string priority)
    {
        return priority switch
        {
            "High" => 3,
            "Medium" => 2,
            _ => 1
        };
    }

    private static ServiceResult<PenaltyRuleResponse>? ValidatePenaltyRuleCalculation(CreatePenaltyRuleRequest request)
    {
        if (request.MaxAmount.HasValue && request.MaxAmount.Value < request.Amount)
        {
            return ServiceResult<PenaltyRuleResponse>.BadRequest("Penalty rule max amount cannot be lower than the base amount.");
        }

        return request.CalculationType switch
        {
            PenaltyCalculationType.FixedAmount when request.PercentageRate.HasValue && request.PercentageRate.Value > 0 =>
                ServiceResult<PenaltyRuleResponse>.BadRequest("Fixed amount penalty rules cannot define a percentage rate."),
            PenaltyCalculationType.Percentage when !request.PercentageRate.HasValue || request.PercentageRate.Value <= 0 =>
                ServiceResult<PenaltyRuleResponse>.BadRequest("Percentage penalty rules require a percentage rate greater than zero."),
            PenaltyCalculationType.Percentage when request.PercentageRate.Value > 100 =>
                ServiceResult<PenaltyRuleResponse>.BadRequest("Percentage penalty rate cannot exceed 100 percent."),
            _ => null
        };
    }

    private async Task<CollectionSourceValidationResult> ValidateCollectionCaseSourceAsync(
        CreateCollectionCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceType == FinancialCollectionSourceType.ManualBalance)
        {
            return request.SourceId.HasValue
                ? CollectionSourceValidationResult.Invalid(ServiceResultStatus.BadRequest, "Manual collection cases cannot reference a source id.")
                : CollectionSourceValidationResult.Valid();
        }

        if (!request.SourceId.HasValue)
        {
            return CollectionSourceValidationResult.Invalid(ServiceResultStatus.BadRequest, "Collection case source id is required for non-manual sources.");
        }

        var source = await ResolveCollectionSourceAsync(
            request.CompoundId,
            request.ResidentProfileId,
            request.SourceType,
            request.SourceId.Value,
            cancellationToken);

        if (source is null)
        {
            return CollectionSourceValidationResult.Invalid(ServiceResultStatus.NotFound, "Collection case source was not found for this resident.");
        }

        if (source.OutstandingAmount <= 0)
        {
            return CollectionSourceValidationResult.Invalid(ServiceResultStatus.BadRequest, "Collection case source has no outstanding amount.");
        }

        var requestedAmount = Math.Round(request.AmountDue, 2);
        var outstandingAmount = Math.Round(source.OutstandingAmount, 2);
        if (Math.Abs(requestedAmount - outstandingAmount) > 0.01m)
        {
            return CollectionSourceValidationResult.Invalid(ServiceResultStatus.BadRequest, "Collection case amount must match the source outstanding amount.");
        }

        return CollectionSourceValidationResult.Valid();
    }

    private async Task<OutstandingItemSnapshot?> ResolveCollectionSourceAsync(
        Guid compoundId,
        Guid residentProfileId,
        FinancialCollectionSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        return sourceType switch
        {
            FinancialCollectionSourceType.UtilityBill => await dbContext.UtilityBills
                .AsNoTracking()
                .Where(item => item.Id == sourceId
                    && item.CompoundId == compoundId
                    && item.ResidentProfileId == residentProfileId
                    && item.BillStatus != BillStatus.Cancelled)
                .Select(item => new OutstandingItemSnapshot(item.TotalAmount, item.PaidAmount, item.DueDate, item.BillStatus == BillStatus.Overdue))
                .FirstOrDefaultAsync(cancellationToken),

            FinancialCollectionSourceType.RentInvoice => await dbContext.RentInvoices
                .AsNoTracking()
                .Where(item => item.Id == sourceId
                    && item.CompoundId == compoundId
                    && item.ResidentProfileId == residentProfileId
                    && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
                .Select(item => new OutstandingItemSnapshot(item.TotalAmount, item.PaidAmount, item.DueDate, item.RentInvoiceStatus == RentInvoiceStatus.Overdue))
                .FirstOrDefaultAsync(cancellationToken),

            FinancialCollectionSourceType.PropertyInstallment => await dbContext.InstallmentScheduleItems
                .AsNoTracking()
                .Where(item => item.Id == sourceId
                    && item.CompoundId == compoundId
                    && item.ResidentProfileId == residentProfileId
                    && item.InstallmentStatus != InstallmentStatus.Cancelled)
                .Select(item => new OutstandingItemSnapshot(item.Amount, item.PaidAmount, item.DueDate, item.InstallmentStatus == InstallmentStatus.Overdue))
                .FirstOrDefaultAsync(cancellationToken),

            FinancialCollectionSourceType.ViolationFine => await dbContext.ViolationFines
                .AsNoTracking()
                .Where(item => item.Id == sourceId
                    && item.CompoundId == compoundId
                    && item.ResidentProfileId == residentProfileId
                    && item.Status != ViolationFineStatus.Cancelled)
                .Select(item => new OutstandingItemSnapshot(item.Amount, item.PaidAmount, item.DueDate, false))
                .FirstOrDefaultAsync(cancellationToken),

            _ => null
        };
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        if (compoundId == Guid.Empty)
        {
            return false;
        }

        if (compoundAccessService is null)
        {
            return true;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<CompoundAccessScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return new CompoundAccessScope(true, true, []);
        }

        return await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private static async Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, pagination.PageNumber);
        var pageSize = Math.Clamp(pagination.PageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<T>(items, pageNumber, pageSize, totalCount);
    }

    private static PenaltyRuleResponse ToPenaltyRuleResponse(PenaltyRule rule)
    {
        return new PenaltyRuleResponse(
            rule.Id,
            rule.CompoundId,
            rule.Name,
            rule.TargetType,
            rule.CalculationType,
            rule.Status,
            rule.GracePeriodDays,
            rule.Amount,
            rule.PercentageRate,
            rule.MaxAmount,
            rule.PauseWhenDisputed,
            rule.EffectiveFrom,
            rule.EffectiveUntil,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);
    }

    private static CollectionCaseResponse ToCollectionCaseResponse(
        CollectionCase collectionCase,
        string residentName,
        int legalNoticeCount,
        int paymentPlanCount)
    {
        return new CollectionCaseResponse(
            collectionCase.Id,
            collectionCase.CompoundId,
            collectionCase.ResidentProfileId,
            residentName,
            collectionCase.SourceType,
            collectionCase.SourceId,
            collectionCase.Stage,
            collectionCase.Status,
            collectionCase.AmountDue,
            collectionCase.Currency,
            collectionCase.DueDate,
            collectionCase.Reason,
            collectionCase.Notes,
            collectionCase.AssignedToUserId,
            collectionCase.CreatedByUserId,
            collectionCase.OpenedAtUtc,
            collectionCase.LastActionAtUtc,
            collectionCase.ClosedAtUtc,
            legalNoticeCount,
            paymentPlanCount);
    }

    private static LegalNoticeResponse ToLegalNoticeResponse(LegalNotice notice, string residentName)
    {
        return new LegalNoticeResponse(
            notice.Id,
            notice.CompoundId,
            notice.ResidentProfileId,
            residentName,
            notice.CollectionCaseId,
            notice.NoticeType,
            notice.Status,
            notice.Title,
            notice.Body,
            notice.DeliveryChannel,
            notice.DeliveryReference,
            notice.DeadlineDate,
            notice.CreatedByUserId,
            notice.IssuedByUserId,
            notice.CreatedAtUtc,
            notice.IssuedAtUtc,
            notice.UpdatedAtUtc);
    }

    private static PaymentPlanResponse ToPaymentPlanResponse(PaymentPlan plan)
    {
        return new PaymentPlanResponse(
            plan.Id,
            plan.CompoundId,
            plan.ResidentProfileId,
            plan.CollectionCaseId,
            plan.Status,
            plan.TotalAmount,
            plan.Currency,
            plan.InstallmentCount,
            plan.StartDate,
            plan.Notes,
            plan.CreatedByUserId,
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc,
            plan.Installments
                .OrderBy(item => item.InstallmentNumber)
                .Select(item => new PaymentPlanInstallmentResponse(
                    item.Id,
                    item.InstallmentNumber,
                    item.DueDate,
                    item.Amount,
                    item.PaidAmount,
                    item.Status,
                    item.PaidAtUtc))
                .ToArray());
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeCurrency(string? currency)
    {
        var value = TrimOrNull(currency)?.ToUpperInvariant() ?? "IQD";
        return value.Length == 3 ? value : "IQD";
    }

    private static string? CombineNotes(string? existing, string? added)
    {
        var addedNote = TrimOrNull(added);
        if (addedNote is null)
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return addedNote.Length > MaxTextLength ? addedNote[..MaxTextLength] : addedNote;
        }

        var combined = $"{existing.Trim()}\n{DateTime.UtcNow:O}: {addedNote}";
        return combined.Length > MaxTextLength ? combined[^MaxTextLength..] : combined;
    }

    private sealed record CollectionSourceValidationResult(
        bool IsValid,
        ServiceResultStatus Status,
        string Message)
    {
        public static CollectionSourceValidationResult Valid()
        {
            return new CollectionSourceValidationResult(true, ServiceResultStatus.Success, string.Empty);
        }

        public static CollectionSourceValidationResult Invalid(ServiceResultStatus status, string message)
        {
            return new CollectionSourceValidationResult(false, status, message);
        }
    }

    private sealed record OutstandingItemSnapshot(
        decimal TotalAmount,
        decimal PaidAmount,
        DateOnly DueDate,
        bool IsExplicitlyOverdue)
    {
        public decimal OutstandingAmount => Math.Max(0, TotalAmount - PaidAmount);
    }
}
