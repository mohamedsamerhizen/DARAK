using System.Text.Json;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class FinancialGovernanceService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService,
    IFinancialControlService financialControlService)
    : IFinancialGovernanceService
{
    private static readonly FinancialDisputeStatus[] ActiveFinancialDisputeStatuses =
    [
        FinancialDisputeStatus.Open,
        FinancialDisputeStatus.UnderReview,
        FinancialDisputeStatus.NeedResidentResponse
    ];

    private static readonly ViolationAppealStatus[] ActiveViolationAppealStatuses =
    [
        ViolationAppealStatus.Submitted,
        ViolationAppealStatus.UnderReview,
        ViolationAppealStatus.NeedResidentResponse
    ];

    public async Task<ServiceResult<PagedResult<FinancialDisputeResponse>>> SearchFinancialDisputesAsync(
        FinancialDisputeSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<FinancialDisputeResponse>>.Forbidden("Current user cannot access financial disputes.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<FinancialDisputeResponse>>.Success(
                new PagedResult<FinancialDisputeResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var disputes = ApplyFinancialDisputeFilters(
            dbContext.FinancialDisputes
                .AsNoTracking()
                .Include(dispute => dispute.ResidentProfile)
                .ApplyCompoundAccess(scope, dispute => dispute.CompoundId),
            query);

        var totalCount = await disputes.CountAsync(cancellationToken);
        var items = await disputes
            .OrderByDescending(dispute => dispute.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var responses = new List<FinancialDisputeResponse>(items.Count);
        foreach (var dispute in items)
        {
            responses.Add(await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
        }

        return ServiceResult<PagedResult<FinancialDisputeResponse>>.Success(
            new PagedResult<FinancialDisputeResponse>(responses, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<FinancialDisputeResponse>> GetFinancialDisputeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest("Financial dispute id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var dispute = await GetScopedFinancialDisputeQuery(scope, asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (dispute is null)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute was not found.");
        }

        return ServiceResult<FinancialDisputeResponse>.Success(
            await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
    }

    public async Task<ServiceResult<FinancialDisputeResponse>> CreateFinancialDisputeAsync(
        Guid? currentUserId,
        CreateFinancialDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialDisputeResponse>.Forbidden("Current user is required.");
        }

        var validation = ValidateCreateFinancialDisputeRequest(request.CompoundId, request.ResidentProfileId, request.TargetId, request.Reason, request.Message);
        if (validation is not null)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest(validation);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute target was not found.");
        }

        var resident = await dbContext.ResidentProfiles
            .FirstOrDefaultAsync(item => item.Id == request.ResidentProfileId, cancellationToken);
        if (resident is null || resident.CompoundId != request.CompoundId)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Resident was not found.");
        }

        var target = await ResolveFinancialTargetAsync(request.TargetType, request.TargetId, cancellationToken);
        if (target is null || target.CompoundId != request.CompoundId || target.ResidentProfileId != request.ResidentProfileId)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute target was not found.");
        }

        if (!await IsValidConversationLinkAsync(request.ConversationId, request.CompoundId, request.ResidentProfileId, cancellationToken))
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Linked conversation was not found.");
        }

        return await CreateFinancialDisputeCoreAsync(
            currentUserId.Value,
            request.CompoundId,
            request.ResidentProfileId,
            request.TargetType,
            request.TargetId,
            request.ConversationId,
            request.Reason,
            request.Message,
            cancellationToken);
    }

    public async Task<ServiceResult<FinancialDisputeResponse>> CreateResidentFinancialDisputeAsync(
        Guid currentUserId,
        CreateResidentFinancialDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateFinancialDisputeRequest(Guid.NewGuid(), Guid.NewGuid(), request.TargetId, request.Reason, request.Message, validateCompoundAndResident: false);
        if (validation is not null)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest(validation);
        }

        var target = await ResolveFinancialTargetAsync(request.TargetType, request.TargetId, cancellationToken);
        if (target is null || !target.ResidentProfileId.HasValue)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute target was not found.");
        }

        var resident = await dbContext.ResidentProfiles
            .FirstOrDefaultAsync(item => item.Id == target.ResidentProfileId.Value && item.UserId == currentUserId, cancellationToken);
        if (resident is null)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute target was not found.");
        }

        if (!await IsValidConversationLinkAsync(request.ConversationId, target.CompoundId, resident.Id, cancellationToken))
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Linked conversation was not found.");
        }

        return await CreateFinancialDisputeCoreAsync(
            currentUserId,
            target.CompoundId,
            resident.Id,
            request.TargetType,
            request.TargetId,
            request.ConversationId,
            request.Reason,
            request.Message,
            cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<FinancialDisputeResponse>>> SearchResidentFinancialDisputesAsync(
        Guid currentUserId,
        FinancialDisputeSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var residentScopes = await GetResidentProfileScopesAsync(currentUserId, cancellationToken);
        if (residentScopes.Count == 0)
        {
            return ServiceResult<PagedResult<FinancialDisputeResponse>>.NotFound("Resident profile was not found.");
        }

        var allowedResidentIds = residentScopes.Select(item => item.ResidentProfileId).ToArray();
        if (query.ResidentProfileId.HasValue && !allowedResidentIds.Contains(query.ResidentProfileId.Value))
        {
            return ServiceResult<PagedResult<FinancialDisputeResponse>>.Success(
                new PagedResult<FinancialDisputeResponse>([], query.PageNumber, query.PageSize, 0));
        }

        if (query.CompoundId.HasValue && residentScopes.All(item => item.CompoundId != query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<FinancialDisputeResponse>>.Success(
                new PagedResult<FinancialDisputeResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var disputes = ApplyFinancialDisputeFilters(
            dbContext.FinancialDisputes
                .AsNoTracking()
                .Include(dispute => dispute.ResidentProfile)
                .Where(dispute => allowedResidentIds.Contains(dispute.ResidentProfileId)),
            query);

        var totalCount = await disputes.CountAsync(cancellationToken);
        var items = await disputes
            .OrderByDescending(dispute => dispute.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var responses = new List<FinancialDisputeResponse>(items.Count);
        foreach (var dispute in items)
        {
            responses.Add(await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
        }

        return ServiceResult<PagedResult<FinancialDisputeResponse>>.Success(
            new PagedResult<FinancialDisputeResponse>(responses, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<FinancialDisputeResponse>> GetResidentFinancialDisputeAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest("Financial dispute id is required.");
        }

        var dispute = await dbContext.FinancialDisputes
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .FirstOrDefaultAsync(item => item.Id == id && item.ResidentProfile.UserId == currentUserId, cancellationToken);
        if (dispute is null)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute was not found.");
        }

        return ServiceResult<FinancialDisputeResponse>.Success(
            await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
    }

    public async Task<ServiceResult<ResidentFinancialGovernanceSummaryResponse>> GetResidentFinancialGovernanceSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var residentScopes = await GetResidentProfileScopesAsync(currentUserId, cancellationToken);
        if (residentScopes.Count == 0)
        {
            return ServiceResult<ResidentFinancialGovernanceSummaryResponse>.NotFound("Resident profile was not found.");
        }

        var allowedResidentIds = residentScopes.Select(item => item.ResidentProfileId).ToArray();

        var disputeCounts = await dbContext.FinancialDisputes
            .AsNoTracking()
            .Where(dispute => allowedResidentIds.Contains(dispute.ResidentProfileId))
            .GroupBy(dispute => dispute.Status)
            .Select(group => new FinancialDisputeStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var appealCounts = await dbContext.ViolationAppeals
            .AsNoTracking()
            .Where(appeal => allowedResidentIds.Contains(appeal.ResidentProfileId))
            .GroupBy(appeal => appeal.Status)
            .Select(group => new ViolationAppealStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var linkedDisputeAdjustmentCount = await dbContext.FinancialDisputes
            .AsNoTracking()
            .CountAsync(dispute => allowedResidentIds.Contains(dispute.ResidentProfileId) && dispute.FinancialAdjustmentId.HasValue, cancellationToken);

        var linkedAppealAdjustmentCount = await dbContext.ViolationAppeals
            .AsNoTracking()
            .CountAsync(appeal => allowedResidentIds.Contains(appeal.ResidentProfileId) && appeal.FinancialAdjustmentId.HasValue, cancellationToken);

        var openDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Open);
        var underReviewDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.UnderReview);
        var needResidentResponseDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.NeedResidentResponse);
        var activeDisputeCount = openDisputes + underReviewDisputes + needResidentResponseDisputes;

        var submittedAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.Submitted);
        var underReviewAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.UnderReview);
        var needResidentResponseAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.NeedResidentResponse);
        var activeAppealCount = submittedAppeals + underReviewAppeals + needResidentResponseAppeals;

        return ServiceResult<ResidentFinancialGovernanceSummaryResponse>.Success(
            new ResidentFinancialGovernanceSummaryResponse(
                activeDisputeCount,
                openDisputes,
                underReviewDisputes,
                needResidentResponseDisputes,
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Accepted),
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Rejected),
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Resolved),
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Cancelled),
                activeAppealCount,
                submittedAppeals,
                underReviewAppeals,
                needResidentResponseAppeals,
                CountViolationAppeals(appealCounts, ViolationAppealStatus.Accepted),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.Rejected),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.FineReduced),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.FineCancelled),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.Cancelled),
                linkedDisputeAdjustmentCount + linkedAppealAdjustmentCount,
                activeDisputeCount + activeAppealCount));
    }

    public async Task<ServiceResult<AdminFinancialGovernanceSummaryResponse>> GetAdminFinancialGovernanceSummaryAsync(
        FinancialGovernanceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<AdminFinancialGovernanceSummaryResponse>.Forbidden("Current user cannot access financial governance summary.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<AdminFinancialGovernanceSummaryResponse>.Success(CreateEmptyAdminSummary(query.CompoundId));
        }

        var disputesQuery = dbContext.FinancialDisputes
            .AsNoTracking()
            .ApplyCompoundAccess(scope, dispute => dispute.CompoundId);
        var appealsQuery = dbContext.ViolationAppeals
            .AsNoTracking()
            .ApplyCompoundAccess(scope, appeal => appeal.CompoundId);

        if (query.CompoundId.HasValue)
        {
            disputesQuery = disputesQuery.Where(dispute => dispute.CompoundId == query.CompoundId.Value);
            appealsQuery = appealsQuery.Where(appeal => appeal.CompoundId == query.CompoundId.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            disputesQuery = disputesQuery.Where(dispute => dispute.CreatedAtUtc >= query.CreatedFromUtc.Value);
            appealsQuery = appealsQuery.Where(appeal => appeal.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            disputesQuery = disputesQuery.Where(dispute => dispute.CreatedAtUtc <= query.CreatedToUtc.Value);
            appealsQuery = appealsQuery.Where(appeal => appeal.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        var disputeCounts = await disputesQuery
            .GroupBy(dispute => dispute.Status)
            .Select(group => new FinancialDisputeStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var appealCounts = await appealsQuery
            .GroupBy(appeal => appeal.Status)
            .Select(group => new ViolationAppealStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var linkedDisputeAdjustmentIds = disputesQuery
            .Where(dispute => dispute.FinancialAdjustmentId.HasValue)
            .Select(dispute => dispute.FinancialAdjustmentId!.Value);
        var linkedAppealAdjustmentIds = appealsQuery
            .Where(appeal => appeal.FinancialAdjustmentId.HasValue)
            .Select(appeal => appeal.FinancialAdjustmentId!.Value);
        var linkedAdjustmentIds = await linkedDisputeAdjustmentIds
            .Concat(linkedAppealAdjustmentIds)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        List<FinancialAdjustmentStatusCount> adjustmentCounts;
        if (linkedAdjustmentIds.Length == 0)
        {
            adjustmentCounts = new List<FinancialAdjustmentStatusCount>();
        }
        else
        {
            adjustmentCounts = await dbContext.FinancialAdjustments
                .AsNoTracking()
                .Where(adjustment => linkedAdjustmentIds.Contains(adjustment.Id))
                .GroupBy(adjustment => adjustment.Status)
                .Select(group => new FinancialAdjustmentStatusCount(group.Key, group.Count()))
                .ToListAsync(cancellationToken);
        }

        var openDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Open);
        var underReviewDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.UnderReview);
        var needResidentResponseDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.NeedResidentResponse);
        var activeDisputes = openDisputes + underReviewDisputes + needResidentResponseDisputes;
        var submittedAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.Submitted);
        var underReviewAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.UnderReview);
        var needResidentResponseAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.NeedResidentResponse);
        var activeAppeals = submittedAppeals + underReviewAppeals + needResidentResponseAppeals;

        return ServiceResult<AdminFinancialGovernanceSummaryResponse>.Success(
            new AdminFinancialGovernanceSummaryResponse(
                query.CompoundId,
                disputeCounts.Sum(item => item.Count),
                activeDisputes,
                openDisputes,
                underReviewDisputes,
                needResidentResponseDisputes,
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Accepted),
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Rejected),
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Resolved),
                CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Cancelled),
                appealCounts.Sum(item => item.Count),
                activeAppeals,
                submittedAppeals,
                underReviewAppeals,
                needResidentResponseAppeals,
                CountViolationAppeals(appealCounts, ViolationAppealStatus.Accepted),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.Rejected),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.FineReduced),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.FineCancelled),
                CountViolationAppeals(appealCounts, ViolationAppealStatus.Cancelled),
                linkedAdjustmentIds.Length,
                CountFinancialAdjustments(adjustmentCounts, FinancialAdjustmentStatus.PendingApproval),
                CountFinancialAdjustments(adjustmentCounts, FinancialAdjustmentStatus.Applied),
                CountFinancialAdjustments(adjustmentCounts, FinancialAdjustmentStatus.Cancelled),
                activeDisputes + activeAppeals));
    }

    public async Task<ServiceResult<AdminResidentFinancialGovernanceSnapshotResponse>> GetAdminResidentFinancialGovernanceSnapshotAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default)
    {
        if (residentProfileId == Guid.Empty)
        {
            return ServiceResult<AdminResidentFinancialGovernanceSnapshotResponse>.BadRequest("Resident profile id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<AdminResidentFinancialGovernanceSnapshotResponse>.Forbidden("Current user cannot access resident financial governance snapshot.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == residentProfileId, cancellationToken);
        if (resident is null)
        {
            return ServiceResult<AdminResidentFinancialGovernanceSnapshotResponse>.NotFound("Resident profile was not found.");
        }

        var summary = await BuildResidentFinancialGovernanceSummaryAsync(new[] { residentProfileId }, cancellationToken);
        var latestDisputeCreatedAtUtc = await dbContext.FinancialDisputes
            .AsNoTracking()
            .Where(dispute => dispute.ResidentProfileId == residentProfileId)
            .OrderByDescending(dispute => dispute.CreatedAtUtc)
            .Select(dispute => (DateTime?)dispute.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var latestAppealCreatedAtUtc = await dbContext.ViolationAppeals
            .AsNoTracking()
            .Where(appeal => appeal.ResidentProfileId == residentProfileId)
            .OrderByDescending(appeal => appeal.CreatedAtUtc)
            .Select(appeal => (DateTime?)appeal.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<AdminResidentFinancialGovernanceSnapshotResponse>.Success(
            new AdminResidentFinancialGovernanceSnapshotResponse(
                resident.Id,
                resident.CompoundId,
                resident.FullName,
                summary,
                latestDisputeCreatedAtUtc,
                latestAppealCreatedAtUtc));
    }

    public async Task<ServiceResult<FinancialDisputeResponse>> TransitionFinancialDisputeAsync(
        Guid? currentUserId,
        Guid id,
        TransitionFinancialDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialDisputeResponse>.Forbidden("Current user is required.");
        }

        if (id == Guid.Empty)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest("Financial dispute id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var dispute = await GetScopedFinancialDisputeQuery(scope, asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (dispute is null)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute was not found.");
        }

        var oldStatus = dispute.Status;
        var newStatus = GetFinancialDisputeStatusAfterTransition(dispute.Status, request.Transition);
        if (newStatus is null)
        {
            return ServiceResult<FinancialDisputeResponse>.Conflict("Financial dispute transition is not allowed from the current status.");
        }

        var now = DateTime.UtcNow;
        dispute.Status = newStatus.Value;
        dispute.UpdatedAtUtc = now;
        dispute.AdminDecisionNotes = string.IsNullOrWhiteSpace(request.Notes) ? dispute.AdminDecisionNotes : request.Notes.Trim();

        if (newStatus.Value == FinancialDisputeStatus.UnderReview || newStatus.Value == FinancialDisputeStatus.NeedResidentResponse)
        {
            dispute.ReviewedByUserId = currentUserId.Value;
            dispute.ReviewedAtUtc ??= now;
        }

        if (newStatus.Value is FinancialDisputeStatus.Accepted or FinancialDisputeStatus.Rejected)
        {
            dispute.ReviewedByUserId = currentUserId.Value;
            dispute.ReviewedAtUtc = now;
            dispute.ResolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary)
                ? dispute.ResolutionSummary
                : request.ResolutionSummary.Trim();
        }

        if (newStatus.Value == FinancialDisputeStatus.Resolved)
        {
            dispute.ResolvedByUserId = currentUserId.Value;
            dispute.ResolvedAtUtc = now;
            dispute.ResolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary)
                ? dispute.ResolutionSummary ?? request.Notes?.Trim()
                : request.ResolutionSummary.Trim();
        }

        if (newStatus.Value == FinancialDisputeStatus.Cancelled)
        {
            dispute.CancelledByUserId = currentUserId.Value;
            dispute.CancelledAtUtc = now;
            dispute.ResolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary)
                ? request.Notes?.Trim()
                : request.ResolutionSummary.Trim();
        }

        await AddFinancialDisputeAuditAsync(
            dispute,
            currentUserId.Value,
            AuditActionType.FinancialDisputeStatusChanged,
            AuditSeverity.High,
            $"Financial dispute status changed from {oldStatus} to {newStatus.Value}.",
            request.Notes,
            [new AuditLogChangeRecord(nameof(FinancialDispute.Status), oldStatus.ToString(), newStatus.Value.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FinancialDisputeResponse>.Success(
            await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
    }

    public async Task<ServiceResult<FinancialDisputeResponse>> CreateAdjustmentForFinancialDisputeAsync(
        Guid? currentUserId,
        Guid id,
        CreateGovernanceFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<FinancialDisputeResponse>.Forbidden("Current user is required.");
        }

        if (id == Guid.Empty)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest("Financial dispute id is required.");
        }

        var validation = ValidateGovernanceAdjustmentRequest(request);
        if (validation is not null)
        {
            return ServiceResult<FinancialDisputeResponse>.BadRequest(validation);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var dispute = await GetScopedFinancialDisputeQuery(scope, asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (dispute is null)
        {
            return ServiceResult<FinancialDisputeResponse>.NotFound("Financial dispute was not found.");
        }

        if (dispute.Status != FinancialDisputeStatus.Accepted)
        {
            return ServiceResult<FinancialDisputeResponse>.Conflict("Financial dispute adjustments can only be requested after the dispute is accepted and before it is resolved.");
        }

        if (dispute.FinancialAdjustmentId.HasValue)
        {
            return ServiceResult<FinancialDisputeResponse>.Conflict("A financial adjustment is already linked to this dispute.");
        }

        var adjustmentResult = await financialControlService.CreateAdjustmentAsync(
            currentUserId.Value,
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = dispute.CompoundId,
                ResidentProfileId = dispute.ResidentProfileId,
                AdjustmentType = request.AdjustmentType,
                Amount = request.Amount,
                Currency = request.Currency,
                Reason = $"Financial dispute {dispute.Id:N}: {request.Reason.Trim()}"
            },
            cancellationToken);
        if (!adjustmentResult.IsSuccess)
        {
            return MapAdjustmentFailure<FinancialDisputeResponse>(adjustmentResult);
        }

        dispute.FinancialAdjustmentId = adjustmentResult.Value!.Id;
        dispute.UpdatedAtUtc = DateTime.UtcNow;

        await AddFinancialDisputeAuditAsync(
            dispute,
            currentUserId.Value,
            AuditActionType.FinancialDisputeStatusChanged,
            AuditSeverity.High,
            "Financial dispute linked to a requested financial adjustment.",
            request.Reason,
            [new AuditLogChangeRecord(nameof(FinancialDispute.FinancialAdjustmentId), null, adjustmentResult.Value.Id.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FinancialDisputeResponse>.Success(
            await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<ViolationAppealResponse>>> SearchViolationAppealsAsync(
        ViolationAppealSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<ViolationAppealResponse>>.Forbidden("Current user cannot access violation appeals.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<ViolationAppealResponse>>.Success(
                new PagedResult<ViolationAppealResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var appeals = ApplyViolationAppealFilters(
            dbContext.ViolationAppeals
                .AsNoTracking()
                .Include(appeal => appeal.ResidentProfile)
                .Include(appeal => appeal.Violation)
                .Include(appeal => appeal.ViolationFine)
                .ApplyCompoundAccess(scope, appeal => appeal.CompoundId),
            query);

        var totalCount = await appeals.CountAsync(cancellationToken);
        var items = await appeals
            .OrderByDescending(appeal => appeal.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var responses = items.Select(ToViolationAppealResponse).ToArray();

        return ServiceResult<PagedResult<ViolationAppealResponse>>.Success(
            new PagedResult<ViolationAppealResponse>(responses, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<ViolationAppealResponse>> GetViolationAppealAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest("Violation appeal id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var appeal = await GetScopedViolationAppealQuery(scope, asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (appeal is null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation appeal was not found.");
        }

        return ServiceResult<ViolationAppealResponse>.Success(ToViolationAppealResponse(appeal));
    }

    public async Task<ServiceResult<ViolationAppealResponse>> CreateViolationAppealAsync(
        Guid? currentUserId,
        CreateViolationAppealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ViolationAppealResponse>.Forbidden("Current user is required.");
        }

        var validation = ValidateCreateViolationAppealRequest(request.CompoundId, request.ResidentProfileId, request.ViolationId, request.Reason, request.Message);
        if (validation is not null)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest(validation);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation was not found.");
        }

        var validationResult = await ValidateViolationAppealTargetAsync(
            request.CompoundId,
            request.ResidentProfileId,
            request.ViolationId,
            request.ViolationFineId,
            cancellationToken);
        if (validationResult.Error is not null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound(validationResult.Error);
        }

        return await CreateViolationAppealCoreAsync(
            currentUserId.Value,
            request.CompoundId,
            request.ResidentProfileId,
            request.ViolationId,
            request.ViolationFineId,
            request.Reason,
            request.Message,
            cancellationToken);
    }

    public async Task<ServiceResult<ViolationAppealResponse>> CreateResidentViolationAppealAsync(
        Guid currentUserId,
        CreateResidentViolationAppealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ViolationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.Message))
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest("Violation, reason, and message are required.");
        }

        var violation = await dbContext.Violations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ViolationId, cancellationToken);
        if (violation is null || !violation.ResidentProfileId.HasValue)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation was not found.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == violation.ResidentProfileId.Value && item.UserId == currentUserId, cancellationToken);
        if (resident is null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation was not found.");
        }

        var validationResult = await ValidateViolationAppealTargetAsync(
            violation.CompoundId,
            resident.Id,
            request.ViolationId,
            request.ViolationFineId,
            cancellationToken);
        if (validationResult.Error is not null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound(validationResult.Error);
        }

        return await CreateViolationAppealCoreAsync(
            currentUserId,
            violation.CompoundId,
            resident.Id,
            request.ViolationId,
            request.ViolationFineId,
            request.Reason,
            request.Message,
            cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<ViolationAppealResponse>>> SearchResidentViolationAppealsAsync(
        Guid currentUserId,
        ViolationAppealSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var residentScopes = await GetResidentProfileScopesAsync(currentUserId, cancellationToken);
        if (residentScopes.Count == 0)
        {
            return ServiceResult<PagedResult<ViolationAppealResponse>>.NotFound("Resident profile was not found.");
        }

        var allowedResidentIds = residentScopes.Select(item => item.ResidentProfileId).ToArray();
        if (query.ResidentProfileId.HasValue && !allowedResidentIds.Contains(query.ResidentProfileId.Value))
        {
            return ServiceResult<PagedResult<ViolationAppealResponse>>.Success(
                new PagedResult<ViolationAppealResponse>([], query.PageNumber, query.PageSize, 0));
        }

        if (query.CompoundId.HasValue && residentScopes.All(item => item.CompoundId != query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<ViolationAppealResponse>>.Success(
                new PagedResult<ViolationAppealResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var appeals = ApplyViolationAppealFilters(
            dbContext.ViolationAppeals
                .AsNoTracking()
                .Include(appeal => appeal.ResidentProfile)
                .Include(appeal => appeal.Violation)
                .Include(appeal => appeal.ViolationFine)
                .Where(appeal => allowedResidentIds.Contains(appeal.ResidentProfileId)),
            query);

        var totalCount = await appeals.CountAsync(cancellationToken);
        var items = await appeals
            .OrderByDescending(appeal => appeal.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResult<ViolationAppealResponse>>.Success(
            new PagedResult<ViolationAppealResponse>(items.Select(ToViolationAppealResponse).ToList(), query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<ViolationAppealResponse>> GetResidentViolationAppealAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest("Violation appeal id is required.");
        }

        var appeal = await dbContext.ViolationAppeals
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Include(item => item.Violation)
            .Include(item => item.ViolationFine)
            .FirstOrDefaultAsync(item => item.Id == id && item.ResidentProfile.UserId == currentUserId, cancellationToken);
        if (appeal is null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation appeal was not found.");
        }

        return ServiceResult<ViolationAppealResponse>.Success(ToViolationAppealResponse(appeal));
    }

    public async Task<ServiceResult<ViolationAppealResponse>> TransitionViolationAppealAsync(
        Guid? currentUserId,
        Guid id,
        TransitionViolationAppealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ViolationAppealResponse>.Forbidden("Current user is required.");
        }

        if (id == Guid.Empty)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest("Violation appeal id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var appeal = await GetScopedViolationAppealQuery(scope, asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (appeal is null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation appeal was not found.");
        }

        var oldStatus = appeal.Status;
        var newStatus = GetViolationAppealStatusAfterTransition(appeal.Status, request.Transition);
        if (newStatus is null)
        {
            return ServiceResult<ViolationAppealResponse>.Conflict("Violation appeal transition is not allowed from the current status.");
        }

        if (newStatus.Value == ViolationAppealStatus.FineReduced)
        {
            if (appeal.ViolationFine is null)
            {
                return ServiceResult<ViolationAppealResponse>.BadRequest("A linked fine is required to reduce a violation fine.");
            }

            if (!request.ReducedFineAmount.HasValue || request.ReducedFineAmount.Value <= 0 || request.ReducedFineAmount.Value >= appeal.ViolationFine.Amount)
            {
                return ServiceResult<ViolationAppealResponse>.BadRequest("Reduced fine amount must be greater than zero and lower than the original fine amount.");
            }

            appeal.ReducedFineAmount = request.ReducedFineAmount.Value;
        }

        var now = DateTime.UtcNow;
        appeal.Status = newStatus.Value;
        appeal.UpdatedAtUtc = now;
        appeal.ReviewedByUserId = currentUserId.Value;
        appeal.ReviewedAtUtc = now;
        appeal.AdminDecisionNotes = string.IsNullOrWhiteSpace(request.Notes) ? appeal.AdminDecisionNotes : request.Notes.Trim();

        if (newStatus.Value == ViolationAppealStatus.FineCancelled && appeal.ViolationFine is not null)
        {
            if (appeal.ViolationFine.Status == ViolationFineStatus.Paid)
            {
                return ServiceResult<ViolationAppealResponse>.Conflict("Paid violation fines cannot be cancelled by an appeal workflow.");
            }

            appeal.ViolationFine.Status = ViolationFineStatus.Cancelled;
            appeal.ViolationFine.CancelledAt = now;
            appeal.ViolationFine.CancellationReason = string.IsNullOrWhiteSpace(request.Notes)
                ? "Fine cancelled by accepted violation appeal."
                : request.Notes.Trim();
            appeal.ViolationFine.UpdatedAt = now;
        }

        await AddViolationAppealAuditAsync(
            appeal,
            currentUserId.Value,
            AuditActionType.ViolationAppealStatusChanged,
            AuditSeverity.High,
            $"Violation appeal status changed from {oldStatus} to {newStatus.Value}.",
            request.Notes,
            [new AuditLogChangeRecord(nameof(ViolationAppeal.Status), oldStatus.ToString(), newStatus.Value.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ViolationAppealResponse>.Success(ToViolationAppealResponse(appeal));
    }

    public async Task<ServiceResult<ViolationAppealResponse>> CreateAdjustmentForViolationAppealAsync(
        Guid? currentUserId,
        Guid id,
        CreateGovernanceFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ViolationAppealResponse>.Forbidden("Current user is required.");
        }

        if (id == Guid.Empty)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest("Violation appeal id is required.");
        }

        var validation = ValidateGovernanceAdjustmentRequest(request);
        if (validation is not null)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest(validation);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var appeal = await GetScopedViolationAppealQuery(scope, asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (appeal is null)
        {
            return ServiceResult<ViolationAppealResponse>.NotFound("Violation appeal was not found.");
        }

        if (appeal.Status is not (ViolationAppealStatus.Accepted or ViolationAppealStatus.FineReduced or ViolationAppealStatus.FineCancelled))
        {
            return ServiceResult<ViolationAppealResponse>.Conflict("Violation appeal adjustments can only be requested after the appeal reaches an accepted financial outcome.");
        }

        if (appeal.FinancialAdjustmentId.HasValue)
        {
            return ServiceResult<ViolationAppealResponse>.Conflict("A financial adjustment is already linked to this violation appeal.");
        }

        var amountValidation = ValidateViolationAppealAdjustmentAmount(appeal, request);
        if (amountValidation is not null)
        {
            return ServiceResult<ViolationAppealResponse>.BadRequest(amountValidation);
        }

        var adjustmentResult = await financialControlService.CreateAdjustmentAsync(
            currentUserId.Value,
            new CreateFinancialAdjustmentRequest
            {
                CompoundId = appeal.CompoundId,
                ResidentProfileId = appeal.ResidentProfileId,
                AdjustmentType = request.AdjustmentType,
                Amount = request.Amount,
                Currency = request.Currency,
                Reason = $"Violation appeal {appeal.Id:N}: {request.Reason.Trim()}"
            },
            cancellationToken);
        if (!adjustmentResult.IsSuccess)
        {
            return MapAdjustmentFailure<ViolationAppealResponse>(adjustmentResult);
        }

        appeal.FinancialAdjustmentId = adjustmentResult.Value!.Id;
        appeal.UpdatedAtUtc = DateTime.UtcNow;

        await AddViolationAppealAuditAsync(
            appeal,
            currentUserId.Value,
            AuditActionType.ViolationAppealStatusChanged,
            AuditSeverity.High,
            "Violation appeal linked to a requested financial adjustment.",
            request.Reason,
            [new AuditLogChangeRecord(nameof(ViolationAppeal.FinancialAdjustmentId), null, adjustmentResult.Value.Id.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ViolationAppealResponse>.Success(ToViolationAppealResponse(appeal));
    }

    private async Task<ServiceResult<FinancialDisputeResponse>> CreateFinancialDisputeCoreAsync(
        Guid currentUserId,
        Guid compoundId,
        Guid residentProfileId,
        FinancialDisputeTargetType targetType,
        Guid targetId,
        Guid? conversationId,
        string reason,
        string message,
        CancellationToken cancellationToken)
    {
        var duplicateExists = await dbContext.FinancialDisputes.AnyAsync(dispute =>
            dispute.CompoundId == compoundId
            && dispute.ResidentProfileId == residentProfileId
            && dispute.TargetType == targetType
            && dispute.TargetId == targetId
            && ActiveFinancialDisputeStatuses.Contains(dispute.Status),
            cancellationToken);
        if (duplicateExists)
        {
            return ServiceResult<FinancialDisputeResponse>.Conflict("An active financial dispute already exists for this target.");
        }

        var dispute = new FinancialDispute
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            TargetType = targetType,
            TargetId = targetId,
            ConversationId = conversationId,
            Reason = reason.Trim(),
            ResidentMessage = message.Trim(),
            CreatedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.FinancialDisputes.Add(dispute);
        await AddFinancialDisputeAuditAsync(
            dispute,
            currentUserId,
            AuditActionType.FinancialDisputeOpened,
            AuditSeverity.High,
            "Financial dispute opened.",
            dispute.Reason,
            [new AuditLogChangeRecord(nameof(FinancialDispute.Status), null, FinancialDisputeStatus.Open.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        dispute.ResidentProfile = (await dbContext.ResidentProfiles.FindAsync([residentProfileId], cancellationToken))!;

        return ServiceResult<FinancialDisputeResponse>.Success(
            await ToFinancialDisputeResponseAsync(dispute, cancellationToken));
    }

    private async Task<ServiceResult<ViolationAppealResponse>> CreateViolationAppealCoreAsync(
        Guid currentUserId,
        Guid compoundId,
        Guid residentProfileId,
        Guid violationId,
        Guid? violationFineId,
        string reason,
        string message,
        CancellationToken cancellationToken)
    {
        var duplicateExists = await dbContext.ViolationAppeals.AnyAsync(appeal =>
            appeal.CompoundId == compoundId
            && appeal.ResidentProfileId == residentProfileId
            && appeal.ViolationId == violationId
            && appeal.ViolationFineId == violationFineId
            && ActiveViolationAppealStatuses.Contains(appeal.Status),
            cancellationToken);
        if (duplicateExists)
        {
            return ServiceResult<ViolationAppealResponse>.Conflict("An active violation appeal already exists for this violation.");
        }

        var appeal = new ViolationAppeal
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            ViolationId = violationId,
            ViolationFineId = violationFineId,
            Reason = reason.Trim(),
            ResidentMessage = message.Trim(),
            CreatedByUserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ViolationAppeals.Add(appeal);
        await AddViolationAppealAuditAsync(
            appeal,
            currentUserId,
            AuditActionType.ViolationAppealOpened,
            AuditSeverity.High,
            "Violation appeal opened.",
            appeal.Reason,
            [new AuditLogChangeRecord(nameof(ViolationAppeal.Status), null, ViolationAppealStatus.Submitted.ToString())],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        var created = await dbContext.ViolationAppeals
            .Include(item => item.ResidentProfile)
            .Include(item => item.Violation)
            .Include(item => item.ViolationFine)
            .SingleAsync(item => item.Id == appeal.Id, cancellationToken);

        return ServiceResult<ViolationAppealResponse>.Success(ToViolationAppealResponse(created));
    }

    private async Task<TargetSnapshot?> ResolveFinancialTargetAsync(
        FinancialDisputeTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        switch (targetType)
        {
            case FinancialDisputeTargetType.UtilityBill:
            {
                var bill = await dbContext.UtilityBills
                    .AsNoTracking()
                    .Where(item => item.Id == targetId)
                    .Select(item => new { item.CompoundId, item.ResidentProfileId, item.BillNumber, item.TotalAmount })
                    .FirstOrDefaultAsync(cancellationToken);
                return bill is null ? null : new TargetSnapshot(bill.CompoundId, bill.ResidentProfileId, bill.BillNumber, bill.TotalAmount);
            }

            case FinancialDisputeTargetType.Payment:
            {
                var payment = await dbContext.Payments
                    .AsNoTracking()
                    .Where(item => item.Id == targetId)
                    .Select(item => new { item.CompoundId, item.ResidentProfileId, item.PaymentReference, item.Amount })
                    .FirstOrDefaultAsync(cancellationToken);
                return payment is null ? null : new TargetSnapshot(payment.CompoundId, payment.ResidentProfileId, payment.PaymentReference, payment.Amount);
            }

            case FinancialDisputeTargetType.ViolationFine:
            {
                var fine = await dbContext.ViolationFines
                    .AsNoTracking()
                    .Where(item => item.Id == targetId)
                    .Select(item => new { item.CompoundId, item.ResidentProfileId, item.Id, item.Amount })
                    .FirstOrDefaultAsync(cancellationToken);
                return fine is null ? null : new TargetSnapshot(fine.CompoundId, fine.ResidentProfileId, $"FINE-{fine.Id.ToString("N")[..12].ToUpperInvariant()}", fine.Amount);
            }

            case FinancialDisputeTargetType.RentInvoice:
            {
                var invoice = await dbContext.RentInvoices
                    .AsNoTracking()
                    .Where(item => item.Id == targetId)
                    .Select(item => new { item.CompoundId, item.ResidentProfileId, item.InvoiceNumber, item.TotalAmount })
                    .FirstOrDefaultAsync(cancellationToken);
                return invoice is null ? null : new TargetSnapshot(invoice.CompoundId, invoice.ResidentProfileId, invoice.InvoiceNumber, invoice.TotalAmount);
            }

            case FinancialDisputeTargetType.PropertyInstallment:
            {
                var installment = await dbContext.InstallmentScheduleItems
                    .AsNoTracking()
                    .Where(item => item.Id == targetId)
                    .Select(item => new { item.CompoundId, item.ResidentProfileId, item.InstallmentNumber, item.Amount })
                    .FirstOrDefaultAsync(cancellationToken);
                return installment is null ? null : new TargetSnapshot(installment.CompoundId, installment.ResidentProfileId, $"INSTALLMENT-{installment.InstallmentNumber}", installment.Amount);
            }

            case FinancialDisputeTargetType.FinancialAdjustment:
            {
                var adjustment = await dbContext.FinancialAdjustments
                    .AsNoTracking()
                    .Where(item => item.Id == targetId)
                    .Select(item => new { item.CompoundId, item.ResidentProfileId, item.Id, item.Amount })
                    .FirstOrDefaultAsync(cancellationToken);
                return adjustment is null ? null : new TargetSnapshot(adjustment.CompoundId, adjustment.ResidentProfileId, $"ADJ-{adjustment.Id.ToString("N")[..12].ToUpperInvariant()}", adjustment.Amount);
            }

            default:
                return null;
        }
    }

    private async Task<FinancialDisputeResponse> ToFinancialDisputeResponseAsync(
        FinancialDispute dispute,
        CancellationToken cancellationToken)
    {
        var target = await ResolveFinancialTargetAsync(dispute.TargetType, dispute.TargetId, cancellationToken);
        return new FinancialDisputeResponse(
            dispute.Id,
            dispute.CompoundId,
            dispute.ResidentProfileId,
            dispute.ResidentProfile.FullName,
            dispute.TargetType,
            dispute.TargetId,
            target?.Reference ?? $"{dispute.TargetType}-{dispute.TargetId.ToString("N")[..12].ToUpperInvariant()}",
            target?.Amount,
            dispute.Status,
            dispute.Reason,
            dispute.ResidentMessage,
            dispute.AdminDecisionNotes,
            dispute.ResolutionSummary,
            dispute.ConversationId,
            dispute.FinancialAdjustmentId,
            dispute.CreatedByUserId,
            dispute.ReviewedByUserId,
            dispute.ResolvedByUserId,
            dispute.CancelledByUserId,
            dispute.CreatedAtUtc,
            dispute.UpdatedAtUtc,
            dispute.ReviewedAtUtc,
            dispute.ResolvedAtUtc,
            dispute.CancelledAtUtc);
    }

    private static ViolationAppealResponse ToViolationAppealResponse(ViolationAppeal appeal)
    {
        return new ViolationAppealResponse(
            appeal.Id,
            appeal.CompoundId,
            appeal.ResidentProfileId,
            appeal.ResidentProfile.FullName,
            appeal.ViolationId,
            appeal.ViolationFineId,
            appeal.Violation.Title,
            appeal.ViolationFine?.Amount,
            appeal.Status,
            appeal.Reason,
            appeal.ResidentMessage,
            appeal.AdminDecisionNotes,
            appeal.ReducedFineAmount,
            appeal.FinancialAdjustmentId,
            appeal.CreatedByUserId,
            appeal.ReviewedByUserId,
            appeal.CreatedAtUtc,
            appeal.UpdatedAtUtc,
            appeal.ReviewedAtUtc);
    }

    private IQueryable<FinancialDispute> GetScopedFinancialDisputeQuery(CompoundAccessScope scope, bool asNoTracking)
    {
        var query = dbContext.FinancialDisputes
            .Include(dispute => dispute.ResidentProfile)
            .ApplyCompoundAccess(scope, dispute => dispute.CompoundId);

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<ViolationAppeal> GetScopedViolationAppealQuery(CompoundAccessScope scope, bool asNoTracking)
    {
        var query = dbContext.ViolationAppeals
            .Include(appeal => appeal.ResidentProfile)
            .Include(appeal => appeal.Violation)
            .Include(appeal => appeal.ViolationFine)
            .ApplyCompoundAccess(scope, appeal => appeal.CompoundId);

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private static IQueryable<FinancialDispute> ApplyFinancialDisputeFilters(
        IQueryable<FinancialDispute> disputes,
        FinancialDisputeSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.TargetType.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.TargetType == query.TargetType.Value);
        }

        if (query.TargetId.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.TargetId == query.TargetId.Value);
        }

        if (query.Status.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.Status == query.Status.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            disputes = disputes.Where(dispute => dispute.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            disputes = disputes.Where(dispute => dispute.Reason.Contains(term) || dispute.ResidentMessage.Contains(term));
        }

        return disputes;
    }

    private static IQueryable<ViolationAppeal> ApplyViolationAppealFilters(
        IQueryable<ViolationAppeal> appeals,
        ViolationAppealSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.ViolationId.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.ViolationId == query.ViolationId.Value);
        }

        if (query.ViolationFineId.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.ViolationFineId == query.ViolationFineId.Value);
        }

        if (query.Status.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.Status == query.Status.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            appeals = appeals.Where(appeal => appeal.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            appeals = appeals.Where(appeal => appeal.Reason.Contains(term) || appeal.ResidentMessage.Contains(term));
        }

        return appeals;
    }

    private async Task<ResidentFinancialGovernanceSummaryResponse> BuildResidentFinancialGovernanceSummaryAsync(
        Guid[] allowedResidentIds,
        CancellationToken cancellationToken)
    {
        var disputeCounts = await dbContext.FinancialDisputes
            .AsNoTracking()
            .Where(dispute => allowedResidentIds.Contains(dispute.ResidentProfileId))
            .GroupBy(dispute => dispute.Status)
            .Select(group => new FinancialDisputeStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var appealCounts = await dbContext.ViolationAppeals
            .AsNoTracking()
            .Where(appeal => allowedResidentIds.Contains(appeal.ResidentProfileId))
            .GroupBy(appeal => appeal.Status)
            .Select(group => new ViolationAppealStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var linkedDisputeAdjustmentCount = await dbContext.FinancialDisputes
            .AsNoTracking()
            .CountAsync(dispute => allowedResidentIds.Contains(dispute.ResidentProfileId) && dispute.FinancialAdjustmentId.HasValue, cancellationToken);
        var linkedAppealAdjustmentCount = await dbContext.ViolationAppeals
            .AsNoTracking()
            .CountAsync(appeal => allowedResidentIds.Contains(appeal.ResidentProfileId) && appeal.FinancialAdjustmentId.HasValue, cancellationToken);

        var openDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Open);
        var underReviewDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.UnderReview);
        var needResidentResponseDisputes = CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.NeedResidentResponse);
        var activeDisputeCount = openDisputes + underReviewDisputes + needResidentResponseDisputes;
        var submittedAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.Submitted);
        var underReviewAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.UnderReview);
        var needResidentResponseAppeals = CountViolationAppeals(appealCounts, ViolationAppealStatus.NeedResidentResponse);
        var activeAppealCount = submittedAppeals + underReviewAppeals + needResidentResponseAppeals;

        return new ResidentFinancialGovernanceSummaryResponse(
            activeDisputeCount,
            openDisputes,
            underReviewDisputes,
            needResidentResponseDisputes,
            CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Accepted),
            CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Rejected),
            CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Resolved),
            CountFinancialDisputes(disputeCounts, FinancialDisputeStatus.Cancelled),
            activeAppealCount,
            submittedAppeals,
            underReviewAppeals,
            needResidentResponseAppeals,
            CountViolationAppeals(appealCounts, ViolationAppealStatus.Accepted),
            CountViolationAppeals(appealCounts, ViolationAppealStatus.Rejected),
            CountViolationAppeals(appealCounts, ViolationAppealStatus.FineReduced),
            CountViolationAppeals(appealCounts, ViolationAppealStatus.FineCancelled),
            CountViolationAppeals(appealCounts, ViolationAppealStatus.Cancelled),
            linkedDisputeAdjustmentCount + linkedAppealAdjustmentCount,
            activeDisputeCount + activeAppealCount);
    }

    private async Task<List<ResidentProfileScope>> GetResidentProfileScopesAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(item => item.UserId == currentUserId && item.IsActive)
            .Select(item => new ResidentProfileScope(item.Id, item.CompoundId))
            .ToListAsync(cancellationToken);
    }

    private static int CountFinancialDisputes(
        IEnumerable<FinancialDisputeStatusCount> counts,
        FinancialDisputeStatus status)
    {
        return counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;
    }

    private static int CountViolationAppeals(
        IEnumerable<ViolationAppealStatusCount> counts,
        ViolationAppealStatus status)
    {
        return counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;
    }

    private static int CountFinancialAdjustments(
        IEnumerable<FinancialAdjustmentStatusCount> counts,
        FinancialAdjustmentStatus status)
    {
        return counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;
    }

    private static AdminFinancialGovernanceSummaryResponse CreateEmptyAdminSummary(Guid? compoundId)
    {
        return new AdminFinancialGovernanceSummaryResponse(
            compoundId,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private async Task<bool> IsValidConversationLinkAsync(
        Guid? conversationId,
        Guid compoundId,
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        if (!conversationId.HasValue)
        {
            return true;
        }

        return await dbContext.Conversations.AnyAsync(conversation =>
            conversation.Id == conversationId.Value
            && conversation.CompoundId == compoundId
            && conversation.ResidentProfileId == residentProfileId,
            cancellationToken);
    }

    private static string? ValidateCreateFinancialDisputeRequest(
        Guid compoundId,
        Guid residentProfileId,
        Guid targetId,
        string reason,
        string message,
        bool validateCompoundAndResident = true)
    {
        if (validateCompoundAndResident && compoundId == Guid.Empty)
        {
            return "Compound id is required.";
        }

        if (validateCompoundAndResident && residentProfileId == Guid.Empty)
        {
            return "Resident profile id is required.";
        }

        if (targetId == Guid.Empty)
        {
            return "Financial dispute target id is required.";
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Financial dispute reason is required.";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Financial dispute message is required.";
        }

        return null;
    }

    private static string? ValidateCreateViolationAppealRequest(
        Guid compoundId,
        Guid residentProfileId,
        Guid violationId,
        string reason,
        string message)
    {
        if (compoundId == Guid.Empty)
        {
            return "Compound id is required.";
        }

        if (residentProfileId == Guid.Empty)
        {
            return "Resident profile id is required.";
        }

        if (violationId == Guid.Empty)
        {
            return "Violation id is required.";
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Violation appeal reason is required.";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Violation appeal message is required.";
        }

        return null;
    }

    private async Task<ViolationAppealTargetValidation> ValidateViolationAppealTargetAsync(
        Guid compoundId,
        Guid residentProfileId,
        Guid violationId,
        Guid? violationFineId,
        CancellationToken cancellationToken)
    {
        var violation = await dbContext.Violations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == violationId, cancellationToken);
        if (violation is null || violation.CompoundId != compoundId || violation.ResidentProfileId != residentProfileId)
        {
            return new ViolationAppealTargetValidation("Violation was not found.");
        }

        if (violationFineId.HasValue)
        {
            var fine = await dbContext.ViolationFines
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == violationFineId.Value, cancellationToken);
            if (fine is null || fine.ViolationId != violationId || fine.CompoundId != compoundId || fine.ResidentProfileId != residentProfileId)
            {
                return new ViolationAppealTargetValidation("Violation fine was not found.");
            }
        }

        return new ViolationAppealTargetValidation(null);
    }

    private static FinancialDisputeStatus? GetFinancialDisputeStatusAfterTransition(
        FinancialDisputeStatus currentStatus,
        FinancialDisputeTransition transition)
    {
        if (currentStatus is FinancialDisputeStatus.Resolved or FinancialDisputeStatus.Cancelled)
        {
            return null;
        }

        return transition switch
        {
            FinancialDisputeTransition.StartReview when currentStatus is FinancialDisputeStatus.Open or FinancialDisputeStatus.NeedResidentResponse => FinancialDisputeStatus.UnderReview,
            FinancialDisputeTransition.RequestResidentResponse when currentStatus is FinancialDisputeStatus.Open or FinancialDisputeStatus.UnderReview => FinancialDisputeStatus.NeedResidentResponse,
            FinancialDisputeTransition.Accept when currentStatus is FinancialDisputeStatus.Open or FinancialDisputeStatus.UnderReview or FinancialDisputeStatus.NeedResidentResponse => FinancialDisputeStatus.Accepted,
            FinancialDisputeTransition.Reject when currentStatus is FinancialDisputeStatus.Open or FinancialDisputeStatus.UnderReview or FinancialDisputeStatus.NeedResidentResponse => FinancialDisputeStatus.Rejected,
            FinancialDisputeTransition.Resolve when currentStatus is FinancialDisputeStatus.Accepted or FinancialDisputeStatus.Rejected => FinancialDisputeStatus.Resolved,
            FinancialDisputeTransition.Cancel when currentStatus is FinancialDisputeStatus.Open or FinancialDisputeStatus.UnderReview or FinancialDisputeStatus.NeedResidentResponse => FinancialDisputeStatus.Cancelled,
            _ => null
        };
    }

    private static ViolationAppealStatus? GetViolationAppealStatusAfterTransition(
        ViolationAppealStatus currentStatus,
        ViolationAppealTransition transition)
    {
        if (currentStatus is ViolationAppealStatus.Accepted
            or ViolationAppealStatus.Rejected
            or ViolationAppealStatus.FineReduced
            or ViolationAppealStatus.FineCancelled
            or ViolationAppealStatus.Cancelled)
        {
            return null;
        }

        return transition switch
        {
            ViolationAppealTransition.StartReview when currentStatus is ViolationAppealStatus.Submitted or ViolationAppealStatus.NeedResidentResponse => ViolationAppealStatus.UnderReview,
            ViolationAppealTransition.RequestResidentResponse when currentStatus is ViolationAppealStatus.Submitted or ViolationAppealStatus.UnderReview => ViolationAppealStatus.NeedResidentResponse,
            ViolationAppealTransition.Accept => ViolationAppealStatus.Accepted,
            ViolationAppealTransition.Reject => ViolationAppealStatus.Rejected,
            ViolationAppealTransition.ReduceFine => ViolationAppealStatus.FineReduced,
            ViolationAppealTransition.CancelFine => ViolationAppealStatus.FineCancelled,
            ViolationAppealTransition.Cancel => ViolationAppealStatus.Cancelled,
            _ => null
        };
    }

    private static string? ValidateGovernanceAdjustmentRequest(CreateGovernanceFinancialAdjustmentRequest request)
    {
        if (request.Amount <= 0)
        {
            return "Adjustment amount must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            return "Adjustment currency must be a valid 3-letter currency code.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "Adjustment reason is required.";
        }

        return null;
    }

    private static string? ValidateViolationAppealAdjustmentAmount(
        ViolationAppeal appeal,
        CreateGovernanceFinancialAdjustmentRequest request)
    {
        if (request.AdjustmentType != FinancialAdjustmentType.Credit)
        {
            return "Violation appeal financial outcomes must create credit adjustments.";
        }

        if (appeal.ViolationFine is null)
        {
            return null;
        }

        var maximumAllowed = appeal.Status switch
        {
            ViolationAppealStatus.FineReduced when appeal.ReducedFineAmount.HasValue => appeal.ViolationFine.Amount - appeal.ReducedFineAmount.Value,
            ViolationAppealStatus.FineCancelled => appeal.ViolationFine.Amount,
            _ => appeal.ViolationFine.Amount
        };

        return request.Amount > maximumAllowed
            ? $"Adjustment amount cannot exceed the appeal financial impact of {maximumAllowed:N2}."
            : null;
    }

    private static ServiceResult<T> MapAdjustmentFailure<T>(ServiceResult<FinancialAdjustmentResponse> adjustmentResult)
    {
        return adjustmentResult.Status switch
        {
            ServiceResultStatus.BadRequest => ServiceResult<T>.BadRequest(adjustmentResult.Message ?? "Financial adjustment request is invalid."),
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(adjustmentResult.Message ?? "Financial adjustment target was not found."),
            ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(adjustmentResult.Message ?? "Current user cannot create the financial adjustment."),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(adjustmentResult.Message ?? "Financial adjustment could not be created."),
            _ => ServiceResult<T>.BadRequest(adjustmentResult.Message ?? "Financial adjustment could not be created.")
        };
    }

    private async Task AddFinancialDisputeAuditAsync(
        FinancialDispute dispute,
        Guid actorUserId,
        AuditActionType actionType,
        AuditSeverity severity,
        string description,
        string? reason,
        IReadOnlyCollection<AuditLogChangeRecord> changes,
        CancellationToken cancellationToken)
    {
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            dispute.CompoundId,
            dispute.ResidentProfileId,
            actorUserId,
            RoleNames.FinanceManagers,
            actionType,
            AuditEntityType.FinancialDispute,
            dispute.Id,
            severity,
            "FinancialGovernance",
            description,
            reason,
            AfterValuesJson: JsonSerializer.Serialize(new
            {
                dispute.Id,
                dispute.TargetType,
                dispute.TargetId,
                dispute.Status,
                dispute.ConversationId
            }),
            MetadataJson: JsonSerializer.Serialize(new
            {
                dispute.CompoundId,
                dispute.ResidentProfileId,
                dispute.CreatedByUserId,
                dispute.ReviewedByUserId,
                dispute.ResolvedByUserId,
                dispute.CancelledByUserId
            }),
            Changes: changes), cancellationToken);
    }

    private async Task AddViolationAppealAuditAsync(
        ViolationAppeal appeal,
        Guid actorUserId,
        AuditActionType actionType,
        AuditSeverity severity,
        string description,
        string? reason,
        IReadOnlyCollection<AuditLogChangeRecord> changes,
        CancellationToken cancellationToken)
    {
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            appeal.CompoundId,
            appeal.ResidentProfileId,
            actorUserId,
            RoleNames.ViolationFineManagers,
            actionType,
            AuditEntityType.ViolationAppeal,
            appeal.Id,
            severity,
            "FinancialGovernance",
            description,
            reason,
            AfterValuesJson: JsonSerializer.Serialize(new
            {
                appeal.Id,
                appeal.ViolationId,
                appeal.ViolationFineId,
                appeal.Status,
                appeal.ReducedFineAmount
            }),
            MetadataJson: JsonSerializer.Serialize(new
            {
                appeal.CompoundId,
                appeal.ResidentProfileId,
                appeal.CreatedByUserId,
                appeal.ReviewedByUserId
            }),
            Changes: changes), cancellationToken);
    }

    private sealed record TargetSnapshot(
        Guid CompoundId,
        Guid? ResidentProfileId,
        string Reference,
        decimal Amount);

    private sealed record ViolationAppealTargetValidation(string? Error);

    private sealed record ResidentProfileScope(Guid ResidentProfileId, Guid CompoundId);

    private sealed record FinancialDisputeStatusCount(FinancialDisputeStatus Status, int Count);

    private sealed record ViolationAppealStatusCount(ViolationAppealStatus Status, int Count);

    private sealed record FinancialAdjustmentStatusCount(FinancialAdjustmentStatus Status, int Count);

}
