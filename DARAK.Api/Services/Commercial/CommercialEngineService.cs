using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Commercial;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CommercialEngineService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : ICommercialEngineService
{
    public async Task<ServiceResult<CommercialEngineDashboardResponse>> GetDashboardAsync(
        CommercialDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<CommercialEngineDashboardResponse>.Forbidden("Current user cannot access commercial dashboard.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<CommercialEngineDashboardResponse>.NotFound("Commercial dashboard was not found.");
        }

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        var billingRules = ApplyOptionalCompoundFilter(
            dbContext.BillingRules.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var meterCorrections = ApplyOptionalCompoundFilter(
            dbContext.MeterReadingCorrections.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var ownershipTransfers = ApplyOptionalCompoundFilter(
            dbContext.OwnershipTransferRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var installmentReschedules = ApplyOptionalCompoundFilter(
            dbContext.InstallmentRescheduleRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var handovers = ApplyOptionalCompoundFilter(
            dbContext.UnitHandoverChecklists.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var lifecycleEvents = ApplyOptionalCompoundFilter(
            dbContext.ContractLifecycleEvents.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var utilityBills = ApplyOptionalCompoundFilter(
            dbContext.UtilityBills.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var rentInvoices = ApplyOptionalCompoundFilter(
            dbContext.RentInvoices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);

        var potentialUtilityLateFees = await utilityBills
            .Where(item => item.DueDate < today && item.TotalAmount > item.PaidAmount && item.BillStatus != BillStatus.Cancelled)
            .SumAsync(item => item.LateFeeAmount, cancellationToken);
        var potentialRentLateFees = await rentInvoices
            .Where(item => item.DueDate < today && item.TotalAmount > item.PaidAmount && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .SumAsync(item => item.LateFeeAmount, cancellationToken);
        var potentialLateFees = potentialUtilityLateFees + potentialRentLateFees;

        var response = new CommercialEngineDashboardResponse(
            query.CompoundId,
            await billingRules.CountAsync(item => item.Status == BillingRuleStatus.Active, cancellationToken),
            await meterCorrections.CountAsync(item => item.Status == MeterReadingCorrectionStatus.PendingReview, cancellationToken),
            await ownershipTransfers.CountAsync(item => item.Status == OwnershipTransferStatus.PendingApproval, cancellationToken),
            await installmentReschedules.CountAsync(item => item.Status == InstallmentRescheduleStatus.PendingApproval, cancellationToken),
            await handovers.CountAsync(item => item.Status == UnitHandoverStatus.Draft || item.Status == UnitHandoverStatus.InProgress, cancellationToken),
            await lifecycleEvents.CountAsync(item => item.CreatedAtUtc >= monthStart, cancellationToken),
            potentialLateFees,
            now);

        return ServiceResult<CommercialEngineDashboardResponse>.Success(response);
    }

    public async Task<ServiceResult<PagedResult<BillingRuleResponse>>> SearchBillingRulesAsync(
        BillingRuleSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<BillingRuleResponse>>.Forbidden("Current user cannot search billing rules.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return ServiceResult<PagedResult<BillingRuleResponse>>.Success(new PagedResult<BillingRuleResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var rules = ApplyOptionalCompoundFilter(
            dbContext.BillingRules.AsNoTracking().Include(rule => rule.Tiers).ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);

        if (query.CompoundServiceId.HasValue)
        {
            rules = rules.Where(item => item.CompoundServiceId == query.CompoundServiceId.Value);
        }

        if (query.Status.HasValue)
        {
            rules = rules.Where(item => item.Status == query.Status.Value);
        }

        if (query.ChargeMode.HasValue)
        {
            rules = rules.Where(item => item.ChargeMode == query.ChargeMode.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            rules = rules.Where(item => item.Name.Contains(term) || (item.Description != null && item.Description.Contains(term)));
        }

        var totalCount = await rules.CountAsync(cancellationToken);
        var ruleItems = await rules
            .OrderBy(item => item.CompoundId)
            .ThenBy(item => item.Name)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = ruleItems.Select(ToBillingRuleResponse).ToArray();

        return ServiceResult<PagedResult<BillingRuleResponse>>.Success(new PagedResult<BillingRuleResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<BillingRuleResponse>> GetBillingRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var rule = await dbContext.BillingRules
            .AsNoTracking()
            .Include(item => item.Tiers)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return rule is null
            ? ServiceResult<BillingRuleResponse>.NotFound("Billing rule was not found.")
            : ServiceResult<BillingRuleResponse>.Success(ToBillingRuleResponse(rule));
    }

    public async Task<ServiceResult<BillingRuleResponse>> CreateBillingRuleAsync(
        Guid? currentUserId,
        CreateBillingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<BillingRuleResponse>.Forbidden("Current user is required.");
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<BillingRuleResponse>.NotFound("Compound was not found.");
        }

        var validation = ValidateBillingRule(request);
        if (validation is not null)
        {
            return ServiceResult<BillingRuleResponse>.BadRequest(validation);
        }

        if (request.CompoundServiceId.HasValue)
        {
            var serviceExists = await dbContext.CompoundServices.AnyAsync(
                item => item.Id == request.CompoundServiceId.Value && item.CompoundId == request.CompoundId,
                cancellationToken);
            if (!serviceExists)
            {
                return ServiceResult<BillingRuleResponse>.BadRequest("Compound service must belong to the selected compound.");
            }
        }

        var rule = new BillingRule
        {
            CompoundId = request.CompoundId,
            CompoundServiceId = request.CompoundServiceId,
            Name = request.Name.Trim(),
            Description = TrimOptional(request.Description),
            Status = request.Status,
            ChargeMode = request.ChargeMode,
            FixedChargeAmount = request.FixedChargeAmount,
            RatePerUnit = request.RatePerUnit,
            MinimumChargeAmount = request.MinimumChargeAmount,
            LateFeeFlatAmount = request.LateFeeFlatAmount,
            LateFeePercentage = request.LateFeePercentage,
            GracePeriodDays = request.GracePeriodDays,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Notes = TrimOptional(request.Notes),
            CreatedByUserId = currentUserId.Value
        };

        dbContext.BillingRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AppendAuditAsync(
            request.CompoundId,
            null,
            currentUserId,
            AuditActionType.BillingRuleCreated,
            AuditEntityType.BillingRule,
            rule.Id,
            AuditSeverity.Medium,
            "Billing rule created",
            request.Name,
            cancellationToken);

        return ServiceResult<BillingRuleResponse>.Success(ToBillingRuleResponse(rule));
    }

    public async Task<ServiceResult<BillingRuleResponse>> AddBillingRuleTierAsync(
        Guid? currentUserId,
        Guid id,
        AddBillingRuleTierRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var rule = await dbContext.BillingRules
            .Include(item => item.Tiers)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (rule is null)
        {
            return ServiceResult<BillingRuleResponse>.NotFound("Billing rule was not found.");
        }

        if (request.FromQuantity < 0
            || (request.ToQuantity.HasValue && request.ToQuantity.Value <= request.FromQuantity)
            || request.RatePerUnit < 0
            || request.FixedAmount < 0)
        {
            return ServiceResult<BillingRuleResponse>.BadRequest("Invalid billing tier range or amount.");
        }

        var tier = new BillingRuleTier
        {
            BillingRuleId = rule.Id,
            FromQuantity = request.FromQuantity,
            ToQuantity = request.ToQuantity,
            RatePerUnit = request.RatePerUnit,
            FixedAmount = request.FixedAmount,
            SortOrder = request.SortOrder
        };

        dbContext.BillingRuleTiers.Add(tier);
        rule.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await AppendAuditAsync(
            rule.CompoundId,
            null,
            currentUserId,
            AuditActionType.BillingRuleTierAdded,
            AuditEntityType.BillingRule,
            rule.Id,
            AuditSeverity.Low,
            "Billing rule tier added",
            $"Tier {request.FromQuantity}-{request.ToQuantity?.ToString() ?? "open"}",
            cancellationToken);

        return ServiceResult<BillingRuleResponse>.Success(ToBillingRuleResponse(rule));
    }

    public async Task<ServiceResult<PagedResult<MeterReadingCorrectionResponse>>> SearchMeterCorrectionsAsync(
        MeterReadingCorrectionSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<PagedResult<MeterReadingCorrectionResponse>>.Forbidden("Current user cannot search meter corrections.");
        }

        var corrections = ApplyOptionalCompoundFilter(
            dbContext.MeterReadingCorrections.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);

        if (query.MeterReadingId.HasValue)
        {
            corrections = corrections.Where(item => item.MeterReadingId == query.MeterReadingId.Value);
        }

        if (query.Status.HasValue)
        {
            corrections = corrections.Where(item => item.Status == query.Status.Value);
        }

        var totalCount = await corrections.CountAsync(cancellationToken);
        var correctionItems = await corrections
            .OrderByDescending(item => item.RequestedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = correctionItems.Select(ToMeterCorrectionResponse).ToArray();

        return ServiceResult<PagedResult<MeterReadingCorrectionResponse>>.Success(new PagedResult<MeterReadingCorrectionResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<MeterReadingCorrectionResponse>> CreateMeterCorrectionAsync(
        Guid? currentUserId,
        CreateMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<MeterReadingCorrectionResponse>.Forbidden("Current user is required.");
        }

        var reading = await dbContext.MeterReadings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.MeterReadingId, cancellationToken);
        if (reading is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(reading.CompoundId, cancellationToken))
        {
            return ServiceResult<MeterReadingCorrectionResponse>.NotFound("Meter reading was not found.");
        }

        if (reading.IsBilled)
        {
            return ServiceResult<MeterReadingCorrectionResponse>.Conflict("Billed meter readings require a bill adjustment workflow.");
        }

        if (request.CorrectedCurrentReading < request.CorrectedPreviousReading)
        {
            return ServiceResult<MeterReadingCorrectionResponse>.BadRequest("Corrected current reading cannot be lower than corrected previous reading.");
        }

        var rate = request.CorrectedRatePerUnit ?? reading.RatePerUnit;
        if (rate < 0)
        {
            return ServiceResult<MeterReadingCorrectionResponse>.BadRequest("Corrected rate cannot be negative.");
        }

        var correctedConsumption = request.CorrectedCurrentReading - request.CorrectedPreviousReading;
        var correction = new MeterReadingCorrection
        {
            CompoundId = reading.CompoundId,
            MeterReadingId = reading.Id,
            MeterId = reading.MeterId,
            PropertyUnitId = reading.PropertyUnitId,
            RequestedByUserId = currentUserId.Value,
            OriginalPreviousReading = reading.PreviousReading,
            OriginalCurrentReading = reading.CurrentReading,
            OriginalConsumption = reading.Consumption,
            OriginalAmount = reading.Amount,
            CorrectedPreviousReading = request.CorrectedPreviousReading,
            CorrectedCurrentReading = request.CorrectedCurrentReading,
            CorrectedConsumption = correctedConsumption,
            CorrectedAmount = correctedConsumption * rate,
            Reason = request.Reason.Trim()
        };

        dbContext.MeterReadingCorrections.Add(correction);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AppendAuditAsync(reading.CompoundId, null, currentUserId, AuditActionType.MeterReadingCorrectionRequested, AuditEntityType.MeterReadingCorrection, correction.Id, AuditSeverity.High, "Meter reading correction requested", correction.Reason, cancellationToken);

        return ServiceResult<MeterReadingCorrectionResponse>.Success(ToMeterCorrectionResponse(correction));
    }

    public async Task<ServiceResult<MeterReadingCorrectionResponse>> ApproveMeterCorrectionAsync(
        Guid? currentUserId,
        Guid id,
        DecideMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await DecideMeterCorrectionAsync(currentUserId, id, request, approve: true, cancellationToken);
    }

    public async Task<ServiceResult<MeterReadingCorrectionResponse>> RejectMeterCorrectionAsync(
        Guid? currentUserId,
        Guid id,
        DecideMeterReadingCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await DecideMeterCorrectionAsync(currentUserId, id, request, approve: false, cancellationToken);
    }

    public async Task<ServiceResult<ContractLifecycleEventResponse>> CreateContractLifecycleEventAsync(
        Guid? currentUserId,
        CreateContractLifecycleEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ContractLifecycleEventResponse>.Forbidden("Current user is required.");
        }

        var target = await ResolveContractTargetAsync(request.ContractType, request.ContractId, cancellationToken);
        if (target is null || target.Value.CompoundId != request.CompoundId || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ContractLifecycleEventResponse>.NotFound("Contract was not found.");
        }

        var entry = new ContractLifecycleEvent
        {
            CompoundId = request.CompoundId,
            ContractType = request.ContractType,
            ContractId = request.ContractId,
            PropertyUnitId = target.Value.PropertyUnitId,
            ResidentProfileId = target.Value.ResidentProfileId,
            EventType = request.EventType,
            ActorUserId = currentUserId.Value,
            EffectiveDate = request.EffectiveDate,
            Reason = request.Reason.Trim(),
            Notes = TrimOptional(request.Notes),
            MetadataJson = TrimOptional(request.MetadataJson)
        };

        dbContext.ContractLifecycleEvents.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(entry.CompoundId, entry.ResidentProfileId, currentUserId, AuditActionType.ContractLifecycleEventRecorded, AuditEntityType.ContractLifecycleEvent, entry.Id, AuditSeverity.Medium, "Contract lifecycle event recorded", entry.Reason, cancellationToken);

        return ServiceResult<ContractLifecycleEventResponse>.Success(ToContractLifecycleEventResponse(entry));
    }

    public async Task<ServiceResult<PagedResult<ContractLifecycleEventResponse>>> GetContractTimelineAsync(
        CommercialContractType contractType,
        Guid contractId,
        ContractLifecycleTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var timeline = ApplyOptionalCompoundFilter(
            dbContext.ContractLifecycleEvents.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId)
            .Where(item => item.ContractType == contractType && item.ContractId == contractId);

        var totalCount = await timeline.CountAsync(cancellationToken);
        var eventItems = await timeline
            .OrderByDescending(item => item.EffectiveDate)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = eventItems.Select(ToContractLifecycleEventResponse).ToArray();

        return ServiceResult<PagedResult<ContractLifecycleEventResponse>>.Success(new PagedResult<ContractLifecycleEventResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<UnitHandoverChecklistResponse>> CreateUnitHandoverChecklistAsync(
        Guid? currentUserId,
        CreateUnitHandoverChecklistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitHandoverChecklistResponse>.Forbidden("Current user is required.");
        }

        var unit = await dbContext.PropertyUnits.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.PropertyUnitId, cancellationToken);
        var resident = await dbContext.ResidentProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.ResidentProfileId, cancellationToken);
        if (unit is null || resident is null || unit.CompoundId != resident.CompoundId || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(unit.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitHandoverChecklistResponse>.NotFound("Unit or resident was not found.");
        }

        var checklist = new UnitHandoverChecklist
        {
            CompoundId = unit.CompoundId,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            HandoverType = request.HandoverType,
            Status = UnitHandoverStatus.InProgress,
            ScheduledDate = request.ScheduledDate,
            CreatedByUserId = currentUserId.Value,
            Notes = TrimOptional(request.Notes),
            Items = request.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new UnitHandoverChecklistItem
                {
                    Title = item.Title.Trim(),
                    Description = TrimOptional(item.Description),
                    SortOrder = item.SortOrder
                })
                .ToList()
        };

        if (checklist.Items.Count == 0)
        {
            checklist.Items.Add(new UnitHandoverChecklistItem { Title = "Unit condition verified", SortOrder = 1 });
            checklist.Items.Add(new UnitHandoverChecklistItem { Title = "Keys/access cards verified", SortOrder = 2 });
            checklist.Items.Add(new UnitHandoverChecklistItem { Title = "Meters documented", SortOrder = 3 });
        }

        dbContext.UnitHandoverChecklists.Add(checklist);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(checklist.CompoundId, checklist.ResidentProfileId, currentUserId, AuditActionType.UnitHandoverChecklistCreated, AuditEntityType.UnitHandoverChecklist, checklist.Id, AuditSeverity.Medium, "Unit handover checklist created", checklist.HandoverType.ToString(), cancellationToken);

        return ServiceResult<UnitHandoverChecklistResponse>.Success(ToUnitHandoverChecklistResponse(checklist));
    }

    public async Task<ServiceResult<UnitHandoverChecklistResponse>> CompleteUnitHandoverChecklistAsync(
        Guid? currentUserId,
        Guid id,
        CompleteUnitHandoverChecklistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitHandoverChecklistResponse>.Forbidden("Current user is required.");
        }

        var checklist = await dbContext.UnitHandoverChecklists
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (checklist is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(checklist.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitHandoverChecklistResponse>.NotFound("Handover checklist was not found.");
        }

        if (checklist.Status == UnitHandoverStatus.Completed)
        {
            return ServiceResult<UnitHandoverChecklistResponse>.Conflict("Handover checklist is already completed.");
        }

        checklist.Status = UnitHandoverStatus.Completed;
        checklist.CompletedDate = request.CompletedDate;
        checklist.CompletedByUserId = currentUserId.Value;
        checklist.UpdatedAtUtc = DateTime.UtcNow;
        checklist.Notes = TrimOptional(request.Notes) ?? checklist.Notes;
        foreach (var item in checklist.Items.Where(item => item.Status == UnitHandoverItemStatus.Pending))
        {
            item.Status = UnitHandoverItemStatus.Passed;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(checklist.CompoundId, checklist.ResidentProfileId, currentUserId, AuditActionType.UnitHandoverChecklistCompleted, AuditEntityType.UnitHandoverChecklist, checklist.Id, AuditSeverity.Medium, "Unit handover checklist completed", checklist.HandoverType.ToString(), cancellationToken);

        return ServiceResult<UnitHandoverChecklistResponse>.Success(ToUnitHandoverChecklistResponse(checklist));
    }

    public async Task<ServiceResult<OwnershipTransferRequestResponse>> CreateOwnershipTransferAsync(Guid? currentUserId, CreateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<OwnershipTransferRequestResponse>.Forbidden("Current user is required.");
        }

        var validation = await ValidateOwnershipTransferAsync(request.PropertyUnitId, request.CurrentOwnerResidentProfileId, request.NewOwnerResidentProfileId, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<OwnershipTransferRequestResponse>.BadRequest(validation.Error!);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(validation.CompoundId, cancellationToken))
        {
            return ServiceResult<OwnershipTransferRequestResponse>.NotFound("Property unit was not found.");
        }

        var pendingTransferExists = await dbContext.OwnershipTransferRequests.AnyAsync(item =>
            item.PropertyUnitId == request.PropertyUnitId
            && item.Status == OwnershipTransferStatus.PendingApproval,
            cancellationToken);
        if (pendingTransferExists)
        {
            return ServiceResult<OwnershipTransferRequestResponse>.Conflict(
                "A pending ownership transfer already exists for this property unit.");
        }

        var transfer = new OwnershipTransferRequest
        {
            CompoundId = validation.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            CurrentOwnerResidentProfileId = request.CurrentOwnerResidentProfileId,
            NewOwnerResidentProfileId = request.NewOwnerResidentProfileId,
            RequestedByUserId = currentUserId.Value,
            RequestedTransferDate = request.RequestedTransferDate,
            Reason = request.Reason.Trim()
        };

        dbContext.OwnershipTransferRequests.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(transfer.CompoundId, transfer.NewOwnerResidentProfileId, currentUserId, AuditActionType.OwnershipTransferRequested, AuditEntityType.OwnershipTransferRequest, transfer.Id, AuditSeverity.High, "Ownership transfer requested", transfer.Reason, cancellationToken);

        return ServiceResult<OwnershipTransferRequestResponse>.Success(ToOwnershipTransferResponse(transfer));
    }

    public async Task<ServiceResult<OwnershipTransferRequestResponse>> ApproveOwnershipTransferAsync(Guid? currentUserId, Guid id, DecideOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        return await DecideOwnershipTransferAsync(currentUserId, id, request, approve: true, cancellationToken);
    }

    public async Task<ServiceResult<OwnershipTransferRequestResponse>> RejectOwnershipTransferAsync(Guid? currentUserId, Guid id, DecideOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        return await DecideOwnershipTransferAsync(currentUserId, id, request, approve: false, cancellationToken);
    }

    public async Task<ServiceResult<InstallmentRescheduleRequestResponse>> CreateInstallmentRescheduleAsync(Guid? currentUserId, CreateInstallmentRescheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.Forbidden("Current user is required.");
        }

        var installment = await dbContext.InstallmentScheduleItems.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.InstallmentScheduleItemId, cancellationToken);
        if (installment is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(installment.CompoundId, cancellationToken))
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.NotFound("Installment was not found.");
        }

        if (installment.InstallmentStatus is InstallmentStatus.Paid or InstallmentStatus.Cancelled)
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.Conflict("Paid or cancelled installments cannot be rescheduled.");
        }

        if (request.RequestedAmount.HasValue && request.RequestedAmount.Value <= 0)
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.BadRequest("Requested amount must be greater than zero.");
        }

        var reschedule = new InstallmentRescheduleRequest
        {
            CompoundId = installment.CompoundId,
            InstallmentScheduleItemId = installment.Id,
            PropertySaleContractId = installment.PropertySaleContractId,
            ResidentProfileId = installment.ResidentProfileId,
            RequestedByUserId = currentUserId.Value,
            OriginalDueDate = installment.DueDate,
            RequestedDueDate = request.RequestedDueDate,
            OriginalAmount = installment.Amount,
            RequestedAmount = request.RequestedAmount,
            Reason = request.Reason.Trim()
        };

        dbContext.InstallmentRescheduleRequests.Add(reschedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(reschedule.CompoundId, reschedule.ResidentProfileId, currentUserId, AuditActionType.InstallmentRescheduleRequested, AuditEntityType.InstallmentRescheduleRequest, reschedule.Id, AuditSeverity.High, "Installment reschedule requested", reschedule.Reason, cancellationToken);

        return ServiceResult<InstallmentRescheduleRequestResponse>.Success(ToInstallmentRescheduleResponse(reschedule));
    }

    public async Task<ServiceResult<InstallmentRescheduleRequestResponse>> ApproveInstallmentRescheduleAsync(Guid? currentUserId, Guid id, DecideInstallmentRescheduleRequest request, CancellationToken cancellationToken = default)
    {
        return await DecideInstallmentRescheduleAsync(currentUserId, id, request, approve: true, cancellationToken);
    }

    public async Task<ServiceResult<InstallmentRescheduleRequestResponse>> RejectInstallmentRescheduleAsync(Guid? currentUserId, Guid id, DecideInstallmentRescheduleRequest request, CancellationToken cancellationToken = default)
    {
        return await DecideInstallmentRescheduleAsync(currentUserId, id, request, approve: false, cancellationToken);
    }

    private async Task<ServiceResult<MeterReadingCorrectionResponse>> DecideMeterCorrectionAsync(Guid? currentUserId, Guid id, DecideMeterReadingCorrectionRequest request, bool approve, CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<MeterReadingCorrectionResponse>.Forbidden("Current user is required.");
        }

        var correction = await dbContext.MeterReadingCorrections.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (correction is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(correction.CompoundId, cancellationToken))
        {
            return ServiceResult<MeterReadingCorrectionResponse>.NotFound("Meter correction was not found.");
        }

        if (correction.Status != MeterReadingCorrectionStatus.PendingReview)
        {
            return ServiceResult<MeterReadingCorrectionResponse>.Conflict("Meter correction was already decided.");
        }

        correction.Status = approve ? MeterReadingCorrectionStatus.Approved : MeterReadingCorrectionStatus.Rejected;
        correction.ReviewedByUserId = currentUserId.Value;
        correction.ReviewedAtUtc = DateTime.UtcNow;
        correction.DecisionReason = request.Reason.Trim();

        if (approve)
        {
            var reading = await dbContext.MeterReadings.FirstAsync(item => item.Id == correction.MeterReadingId, cancellationToken);
            reading.PreviousReading = correction.CorrectedPreviousReading;
            reading.CurrentReading = correction.CorrectedCurrentReading;
            reading.Consumption = correction.CorrectedConsumption;
            reading.Amount = correction.CorrectedAmount;
            reading.UpdatedAt = DateTime.UtcNow;
            correction.Status = MeterReadingCorrectionStatus.Applied;
            correction.AppliedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(correction.CompoundId, null, currentUserId, approve ? AuditActionType.MeterReadingCorrectionApproved : AuditActionType.MeterReadingCorrectionRejected, AuditEntityType.MeterReadingCorrection, correction.Id, approve ? AuditSeverity.High : AuditSeverity.Medium, approve ? "Meter reading correction approved" : "Meter reading correction rejected", correction.DecisionReason, cancellationToken);

        return ServiceResult<MeterReadingCorrectionResponse>.Success(ToMeterCorrectionResponse(correction));
    }

    private async Task<ServiceResult<OwnershipTransferRequestResponse>> DecideOwnershipTransferAsync(Guid? currentUserId, Guid id, DecideOwnershipTransferRequest request, bool approve, CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<OwnershipTransferRequestResponse>.Forbidden("Current user is required.");
        }

        var transfer = await dbContext.OwnershipTransferRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transfer is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(transfer.CompoundId, cancellationToken))
        {
            return ServiceResult<OwnershipTransferRequestResponse>.NotFound("Ownership transfer was not found.");
        }

        if (transfer.Status != OwnershipTransferStatus.PendingApproval)
        {
            return ServiceResult<OwnershipTransferRequestResponse>.Conflict("Ownership transfer was already decided.");
        }

        transfer.Status = approve ? OwnershipTransferStatus.Approved : OwnershipTransferStatus.Rejected;
        transfer.ReviewedByUserId = currentUserId.Value;
        transfer.ReviewedAtUtc = DateTime.UtcNow;
        transfer.DecisionReason = request.Reason.Trim();

        if (approve)
        {
            var saleContract = await dbContext.PropertySaleContracts
                .FirstOrDefaultAsync(contract =>
                    contract.PropertyUnitId == transfer.PropertyUnitId
                    && contract.ResidentProfileId == transfer.CurrentOwnerResidentProfileId
                    && contract.ContractStatus != SaleContractStatus.Cancelled,
                    cancellationToken);

            if (saleContract is null)
            {
                return ServiceResult<OwnershipTransferRequestResponse>.Conflict(
                    "Current owner is no longer the active owner for this property unit.");
            }

            saleContract.ResidentProfileId = transfer.NewOwnerResidentProfileId;
            saleContract.UpdatedAt = DateTime.UtcNow;

            var transferableInstallments = await dbContext.InstallmentScheduleItems
                .Where(installment =>
                    installment.PropertySaleContractId == saleContract.Id
                    && installment.InstallmentStatus != InstallmentStatus.Paid
                    && installment.InstallmentStatus != InstallmentStatus.Cancelled)
                .ToArrayAsync(cancellationToken);

            foreach (var installment in transferableInstallments)
            {
                installment.ResidentProfileId = transfer.NewOwnerResidentProfileId;
                installment.UpdatedAt = DateTime.UtcNow;
            }

            var activeOccupancy = await dbContext.OccupancyRecords
                .FirstOrDefaultAsync(record =>
                    record.PropertyUnitId == transfer.PropertyUnitId
                    && record.OccupancyStatus == OccupancyStatus.Active,
                    cancellationToken);

            if (activeOccupancy is null)
            {
                dbContext.OccupancyRecords.Add(new OccupancyRecord
                {
                    CompoundId = transfer.CompoundId,
                    PropertyUnitId = transfer.PropertyUnitId,
                    ResidentProfileId = transfer.NewOwnerResidentProfileId,
                    OccupancyType = saleContract.SaleType == SaleType.Cash
                        ? OccupancyType.OwnerCash
                        : OccupancyType.OwnerInstallment,
                    OccupancyStatus = OccupancyStatus.Active,
                    StartDate = transfer.RequestedTransferDate,
                    ContractNumber = saleContract.ContractNumber,
                    Notes = "Created by ownership transfer approval."
                });
            }
            else if (activeOccupancy.ResidentProfileId == transfer.CurrentOwnerResidentProfileId
                && activeOccupancy.OccupancyType is OccupancyType.OwnerCash or OccupancyType.OwnerInstallment)
            {
                activeOccupancy.OccupancyStatus = OccupancyStatus.Ended;
                activeOccupancy.EndDate = transfer.RequestedTransferDate;
                activeOccupancy.EndedAt = DateTime.UtcNow;
                activeOccupancy.UpdatedAt = DateTime.UtcNow;

                dbContext.OccupancyRecords.Add(new OccupancyRecord
                {
                    CompoundId = transfer.CompoundId,
                    PropertyUnitId = transfer.PropertyUnitId,
                    ResidentProfileId = transfer.NewOwnerResidentProfileId,
                    OccupancyType = saleContract.SaleType == SaleType.Cash
                        ? OccupancyType.OwnerCash
                        : OccupancyType.OwnerInstallment,
                    OccupancyStatus = OccupancyStatus.Active,
                    StartDate = transfer.RequestedTransferDate,
                    ContractNumber = saleContract.ContractNumber,
                    Notes = "Created by ownership transfer approval."
                });
            }

            dbContext.ContractLifecycleEvents.Add(new ContractLifecycleEvent
            {
                CompoundId = transfer.CompoundId,
                ContractType = CommercialContractType.Sale,
                ContractId = saleContract.Id,
                PropertyUnitId = transfer.PropertyUnitId,
                ResidentProfileId = transfer.NewOwnerResidentProfileId,
                EventType = ContractLifecycleEventType.OwnershipTransferred,
                ActorUserId = currentUserId.Value,
                EffectiveDate = transfer.RequestedTransferDate,
                Reason = request.Reason.Trim()
            });
            transfer.Status = OwnershipTransferStatus.Completed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(transfer.CompoundId, transfer.NewOwnerResidentProfileId, currentUserId, approve ? AuditActionType.OwnershipTransferApproved : AuditActionType.OwnershipTransferRejected, AuditEntityType.OwnershipTransferRequest, transfer.Id, AuditSeverity.High, approve ? "Ownership transfer approved" : "Ownership transfer rejected", transfer.DecisionReason, cancellationToken);

        return ServiceResult<OwnershipTransferRequestResponse>.Success(ToOwnershipTransferResponse(transfer));
    }

    private async Task<ServiceResult<InstallmentRescheduleRequestResponse>> DecideInstallmentRescheduleAsync(Guid? currentUserId, Guid id, DecideInstallmentRescheduleRequest request, bool approve, CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.Forbidden("Current user is required.");
        }

        var reschedule = await dbContext.InstallmentRescheduleRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reschedule is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(reschedule.CompoundId, cancellationToken))
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.NotFound("Installment reschedule was not found.");
        }

        if (reschedule.Status != InstallmentRescheduleStatus.PendingApproval)
        {
            return ServiceResult<InstallmentRescheduleRequestResponse>.Conflict("Installment reschedule was already decided.");
        }

        if (approve && reschedule.RequestedAmount.HasValue)
        {
            var installmentForValidation = await dbContext.InstallmentScheduleItems
                .AsNoTracking()
                .Include(item => item.PropertySaleContract)
                .FirstAsync(item => item.Id == reschedule.InstallmentScheduleItemId, cancellationToken);

            var expectedOutstandingTotal = Math.Round(
                installmentForValidation.PropertySaleContract.PropertyPrice
                    - installmentForValidation.PropertySaleContract.DownPaymentAmount,
                2,
                MidpointRounding.AwayFromZero);

            var scheduleItems = await dbContext.InstallmentScheduleItems
                .AsNoTracking()
                .Where(item => item.PropertySaleContractId == reschedule.PropertySaleContractId
                    && item.InstallmentStatus != InstallmentStatus.Cancelled)
                .ToArrayAsync(cancellationToken);

            var scheduledTotalAfterAmountChange = Math.Round(
                scheduleItems.Sum(item => item.Id == reschedule.InstallmentScheduleItemId
                    ? reschedule.RequestedAmount.Value
                    : item.Amount),
                2,
                MidpointRounding.AwayFromZero);

            if (scheduledTotalAfterAmountChange != expectedOutstandingTotal)
            {
                return ServiceResult<InstallmentRescheduleRequestResponse>.BadRequest(
                    "Installment amount changes must preserve the sale contract outstanding total.");
            }
        }

        reschedule.Status = approve ? InstallmentRescheduleStatus.Approved : InstallmentRescheduleStatus.Rejected;
        reschedule.ReviewedByUserId = currentUserId.Value;
        reschedule.ReviewedAtUtc = DateTime.UtcNow;
        reschedule.DecisionReason = request.Reason.Trim();

        if (approve)
        {
            var installment = await dbContext.InstallmentScheduleItems.FirstAsync(item => item.Id == reschedule.InstallmentScheduleItemId, cancellationToken);
            installment.DueDate = reschedule.RequestedDueDate;
            if (reschedule.RequestedAmount.HasValue)
            {
                installment.Amount = reschedule.RequestedAmount.Value;
            }
            installment.UpdatedAt = DateTime.UtcNow;
            reschedule.Status = InstallmentRescheduleStatus.Applied;
            reschedule.AppliedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendAuditAsync(reschedule.CompoundId, reschedule.ResidentProfileId, currentUserId, approve ? AuditActionType.InstallmentRescheduleApproved : AuditActionType.InstallmentRescheduleRejected, AuditEntityType.InstallmentRescheduleRequest, reschedule.Id, AuditSeverity.High, approve ? "Installment reschedule approved" : "Installment reschedule rejected", reschedule.DecisionReason, cancellationToken);

        return ServiceResult<InstallmentRescheduleRequestResponse>.Success(ToInstallmentRescheduleResponse(reschedule));
    }

    private async Task<(bool IsValid, Guid CompoundId, string? Error)> ValidateOwnershipTransferAsync(Guid propertyUnitId, Guid currentOwnerId, Guid newOwnerId, CancellationToken cancellationToken)
    {
        if (currentOwnerId == newOwnerId)
        {
            return (false, Guid.Empty, "Current owner and new owner must be different residents.");
        }

        var unit = await dbContext.PropertyUnits.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propertyUnitId, cancellationToken);
        var currentOwner = await dbContext.ResidentProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == currentOwnerId, cancellationToken);
        var newOwner = await dbContext.ResidentProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == newOwnerId, cancellationToken);

        if (unit is null || currentOwner is null || newOwner is null)
        {
            return (false, Guid.Empty, "Unit or resident was not found.");
        }

        if (!unit.IsActive || !currentOwner.IsActive || !newOwner.IsActive)
        {
            return (false, Guid.Empty, "Unit and residents must be active.");
        }

        if (unit.CompoundId != currentOwner.CompoundId || unit.CompoundId != newOwner.CompoundId)
        {
            return (false, Guid.Empty, "Residents and unit must belong to the same compound.");
        }

        var saleContract = await dbContext.PropertySaleContracts.AsNoTracking()
            .FirstOrDefaultAsync(contract =>
                contract.PropertyUnitId == propertyUnitId
                && contract.ResidentProfileId == currentOwnerId
                && contract.ContractStatus != SaleContractStatus.Cancelled,
                cancellationToken);

        if (saleContract is null)
        {
            return (false, Guid.Empty, "Current owner is not the active owner for this property unit.");
        }

        return (true, unit.CompoundId, null);
    }

    private async Task<(Guid CompoundId, Guid PropertyUnitId, Guid ResidentProfileId)?> ResolveContractTargetAsync(CommercialContractType contractType, Guid contractId, CancellationToken cancellationToken)
    {
        if (contractType == CommercialContractType.Rent)
        {
            var rent = await dbContext.RentContracts.AsNoTracking()
                .Where(item => item.Id == contractId)
                .Select(item => new { item.CompoundId, item.PropertyUnitId, item.ResidentProfileId })
                .FirstOrDefaultAsync(cancellationToken);
            if (rent is null)
            {
                return null;
            }

            return (rent.CompoundId, rent.PropertyUnitId, rent.ResidentProfileId);
        }

        var sale = await dbContext.PropertySaleContracts.AsNoTracking()
            .Where(item => item.Id == contractId)
            .Select(item => new { item.CompoundId, item.PropertyUnitId, item.ResidentProfileId })
            .FirstOrDefaultAsync(cancellationToken);
        if (sale is null)
        {
            return null;
        }

        return (sale.CompoundId, sale.PropertyUnitId, sale.ResidentProfileId);
    }

    private static IQueryable<T> ApplyOptionalCompoundFilter<T>(IQueryable<T> query, Guid? compoundId)
        where T : class
    {
        if (!compoundId.HasValue)
        {
            return query;
        }

        return query.Where(item => EF.Property<Guid>(item, "CompoundId") == compoundId.Value);
    }

    private static string? ValidateBillingRule(CreateBillingRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Billing rule name is required.";
        }

        if (request.FixedChargeAmount < 0 || request.RatePerUnit < 0 || request.MinimumChargeAmount < 0 || request.LateFeeFlatAmount < 0 || request.LateFeePercentage < 0)
        {
            return "Billing rule amounts cannot be negative.";
        }

        if (request.GracePeriodDays < 0)
        {
            return "Grace period cannot be negative.";
        }

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
        {
            return "Effective end date cannot be before effective start date.";
        }

        if (request.ChargeMode == BillingChargeMode.Tiered && request.RatePerUnit > 0)
        {
            return "Tiered billing rules should use tiers instead of a direct rate per unit.";
        }

        return null;
    }

    private async Task AppendAuditAsync(Guid? compoundId, Guid? residentProfileId, Guid? actorUserId, AuditActionType actionType, AuditEntityType entityType, Guid entityId, AuditSeverity severity, string description, string? reason, CancellationToken cancellationToken)
    {
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            compoundId,
            residentProfileId,
            actorUserId,
            null,
            actionType,
            entityType,
            entityId,
            severity,
            "CommercialEngine",
            description,
            reason),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? TrimOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static BillingRuleResponse ToBillingRuleResponse(BillingRule item)
    {
        return new BillingRuleResponse(
            item.Id,
            item.CompoundId,
            item.CompoundServiceId,
            item.Name,
            item.Description,
            item.Status,
            item.ChargeMode,
            item.FixedChargeAmount,
            item.RatePerUnit,
            item.MinimumChargeAmount,
            item.LateFeeFlatAmount,
            item.LateFeePercentage,
            item.GracePeriodDays,
            item.EffectiveFrom,
            item.EffectiveTo,
            item.Notes,
            item.CreatedAtUtc,
            item.Tiers.OrderBy(tier => tier.SortOrder).Select(ToBillingRuleTierResponse).ToArray());
    }

    private static BillingRuleTierResponse ToBillingRuleTierResponse(BillingRuleTier item)
    {
        return new BillingRuleTierResponse(item.Id, item.FromQuantity, item.ToQuantity, item.RatePerUnit, item.FixedAmount, item.SortOrder);
    }

    private static MeterReadingCorrectionResponse ToMeterCorrectionResponse(MeterReadingCorrection item)
    {
        return new MeterReadingCorrectionResponse(item.Id, item.CompoundId, item.MeterReadingId, item.MeterId, item.PropertyUnitId, item.Status, item.OriginalPreviousReading, item.OriginalCurrentReading, item.OriginalConsumption, item.OriginalAmount, item.CorrectedPreviousReading, item.CorrectedCurrentReading, item.CorrectedConsumption, item.CorrectedAmount, item.Reason, item.DecisionReason, item.RequestedByUserId, item.ReviewedByUserId, item.RequestedAtUtc, item.ReviewedAtUtc, item.AppliedAtUtc);
    }

    private static ContractLifecycleEventResponse ToContractLifecycleEventResponse(ContractLifecycleEvent item)
    {
        return new ContractLifecycleEventResponse(item.Id, item.CompoundId, item.ContractType, item.ContractId, item.PropertyUnitId, item.ResidentProfileId, item.EventType, item.ActorUserId, item.EffectiveDate, item.Reason, item.Notes, item.MetadataJson, item.CreatedAtUtc);
    }

    private static UnitHandoverChecklistResponse ToUnitHandoverChecklistResponse(UnitHandoverChecklist item)
    {
        return new UnitHandoverChecklistResponse(item.Id, item.CompoundId, item.PropertyUnitId, item.ResidentProfileId, item.HandoverType, item.Status, item.ScheduledDate, item.CompletedDate, item.Notes, item.CreatedAtUtc, item.Items.OrderBy(check => check.SortOrder).Select(ToUnitHandoverItemResponse).ToArray());
    }

    private static UnitHandoverChecklistItemResponse ToUnitHandoverItemResponse(UnitHandoverChecklistItem item)
    {
        return new UnitHandoverChecklistItemResponse(item.Id, item.Title, item.Description, item.Status, item.Notes, item.SortOrder);
    }

    private static OwnershipTransferRequestResponse ToOwnershipTransferResponse(OwnershipTransferRequest item)
    {
        return new OwnershipTransferRequestResponse(item.Id, item.CompoundId, item.PropertyUnitId, item.CurrentOwnerResidentProfileId, item.NewOwnerResidentProfileId, item.Status, item.RequestedTransferDate, item.Reason, item.DecisionReason, item.RequestedByUserId, item.ReviewedByUserId, item.RequestedAtUtc, item.ReviewedAtUtc);
    }

    private static InstallmentRescheduleRequestResponse ToInstallmentRescheduleResponse(InstallmentRescheduleRequest item)
    {
        return new InstallmentRescheduleRequestResponse(item.Id, item.CompoundId, item.InstallmentScheduleItemId, item.PropertySaleContractId, item.ResidentProfileId, item.Status, item.OriginalDueDate, item.RequestedDueDate, item.OriginalAmount, item.RequestedAmount, item.Reason, item.DecisionReason, item.RequestedByUserId, item.ReviewedByUserId, item.RequestedAtUtc, item.ReviewedAtUtc, item.AppliedAtUtc);
    }
}


