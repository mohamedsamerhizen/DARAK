using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class MaintenanceReliabilityService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IMaintenanceReliabilityService
{
    private const int MaxNameLength = 150;
    private const int MaxCodeLength = 80;
    private const int MaxTitleLength = 150;
    private const int MaxDescriptionLength = 2000;

    public async Task<ServiceResult<MaintenanceAssetResponse>> CreateAssetAsync(
        CreateMaintenanceAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = TrimOrNull(request.Name);
        var code = TrimOrNull(request.Code);
        if (name is null)
        {
            return ServiceResult<MaintenanceAssetResponse>.BadRequest("Asset name is required.");
        }

        if (code is null)
        {
            return ServiceResult<MaintenanceAssetResponse>.BadRequest("Asset code is required.");
        }

        if (name.Length > MaxNameLength || code.Length > MaxCodeLength)
        {
            return ServiceResult<MaintenanceAssetResponse>.BadRequest("Asset name or code is too long.");
        }

        if (!Enum.IsDefined(request.AssetType) || !Enum.IsDefined(request.Status))
        {
            return ServiceResult<MaintenanceAssetResponse>.BadRequest("Asset type or status is invalid.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<MaintenanceAssetResponse>.Forbidden("Current user cannot access this compound.");
        }

        var locationValidation = await ValidateAssetLocationAsync(
            request.CompoundId,
            request.BuildingId,
            request.FloorId,
            request.PropertyUnitId,
            cancellationToken);
        if (locationValidation is not null)
        {
            return ServiceResult<MaintenanceAssetResponse>.BadRequest(locationValidation);
        }

        var duplicateCode = await dbContext.MaintenanceAssets
            .AsNoTracking()
            .AnyAsync(asset => asset.CompoundId == request.CompoundId && asset.Code == code, cancellationToken);
        if (duplicateCode)
        {
            return ServiceResult<MaintenanceAssetResponse>.Conflict("An asset with the same code already exists in this compound.");
        }

        var asset = new MaintenanceAsset
        {
            CompoundId = request.CompoundId,
            BuildingId = request.BuildingId,
            FloorId = request.FloorId,
            PropertyUnitId = request.PropertyUnitId,
            Name = name,
            Code = code,
            AssetType = request.AssetType,
            Status = request.Status,
            LocationDescription = TrimOrNull(request.LocationDescription),
            Manufacturer = TrimOrNull(request.Manufacturer),
            Model = TrimOrNull(request.Model),
            SerialNumber = TrimOrNull(request.SerialNumber),
            InstalledAtUtc = request.InstalledAtUtc,
            WarrantyExpiresAtUtc = request.WarrantyExpiresAtUtc,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.MaintenanceAssets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<MaintenanceAssetResponse>.Success(ToAssetResponse(asset));
    }

    public async Task<ServiceResult<MaintenanceAssetResponse>> GetAssetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await ApplyCurrentCompoundAccessAsync(dbContext.MaintenanceAssets.AsNoTracking(), cancellationToken);
        var asset = await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return asset is null
            ? ServiceResult<MaintenanceAssetResponse>.NotFound("Asset was not found.")
            : ServiceResult<MaintenanceAssetResponse>.Success(ToAssetResponse(asset));
    }

    public async Task<PagedResult<MaintenanceAssetResponse>> SearchAssetsAsync(
        MaintenanceAssetQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var assets = dbContext.MaintenanceAssets.AsNoTracking().AsQueryable();
        if (query.CompoundId.HasValue)
        {
            assets = assets.Where(asset => asset.CompoundId == query.CompoundId.Value);
        }

        if (query.BuildingId.HasValue)
        {
            assets = assets.Where(asset => asset.BuildingId == query.BuildingId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            assets = assets.Where(asset => asset.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.AssetType.HasValue)
        {
            assets = assets.Where(asset => asset.AssetType == query.AssetType.Value);
        }

        if (query.Status.HasValue)
        {
            assets = assets.Where(asset => asset.Status == query.Status.Value);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            assets = assets.Where(asset => asset.Name.Contains(search) || asset.Code.Contains(search));
        }

        assets = await ApplyCurrentCompoundAccessAsync(assets, cancellationToken);
        return await ToPagedResultAsync(assets.OrderBy(asset => asset.Name), query, ToAssetResponse, cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceSlaPolicyResponse>> CreateSlaPolicyAsync(
        CreateMaintenanceSlaPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = TrimOrNull(request.Name);
        if (name is null)
        {
            return ServiceResult<MaintenanceSlaPolicyResponse>.BadRequest("SLA policy name is required.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<MaintenanceSlaPolicyResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (request.Priority.HasValue && !Enum.IsDefined(request.Priority.Value))
        {
            return ServiceResult<MaintenanceSlaPolicyResponse>.BadRequest("SLA priority is invalid.");
        }

        if (request.SourceType.HasValue && !Enum.IsDefined(request.SourceType.Value))
        {
            return ServiceResult<MaintenanceSlaPolicyResponse>.BadRequest("SLA source type is invalid.");
        }

        if (request.ResponseDueMinutes <= 0 || request.ResolutionDueMinutes <= 0)
        {
            return ServiceResult<MaintenanceSlaPolicyResponse>.BadRequest("SLA response and resolution minutes must be positive.");
        }

        if (request.ResponseDueMinutes > request.ResolutionDueMinutes)
        {
            return ServiceResult<MaintenanceSlaPolicyResponse>.BadRequest("SLA response time cannot be greater than resolution time.");
        }

        var policy = new MaintenanceSlaPolicy
        {
            CompoundId = request.CompoundId,
            Name = name,
            Priority = request.Priority,
            SourceType = request.SourceType,
            ResponseDueMinutes = request.ResponseDueMinutes,
            ResolutionDueMinutes = request.ResolutionDueMinutes,
            EscalationDueMinutes = request.EscalationDueMinutes,
            IsActive = request.IsActive,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.MaintenanceSlaPolicies.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<MaintenanceSlaPolicyResponse>.Success(ToSlaPolicyResponse(policy));
    }

    public async Task<PagedResult<MaintenanceSlaPolicyResponse>> SearchSlaPoliciesAsync(
        MaintenanceSlaPolicyQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var policies = dbContext.MaintenanceSlaPolicies.AsNoTracking().AsQueryable();
        if (query.CompoundId.HasValue)
        {
            policies = policies.Where(policy => policy.CompoundId == query.CompoundId.Value);
        }

        if (query.Priority.HasValue)
        {
            policies = policies.Where(policy => policy.Priority == query.Priority.Value);
        }

        if (query.SourceType.HasValue)
        {
            policies = policies.Where(policy => policy.SourceType == query.SourceType.Value);
        }

        if (query.IsActive.HasValue)
        {
            policies = policies.Where(policy => policy.IsActive == query.IsActive.Value);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            policies = policies.Where(policy => policy.Name.Contains(search));
        }

        policies = await ApplyCurrentCompoundAccessAsync(policies, cancellationToken);
        return await ToPagedResultAsync(policies.OrderBy(policy => policy.Name), query, ToSlaPolicyResponse, cancellationToken);
    }

    public async Task<ServiceResult<WorkOrderSlaSnapshotResponse>> ApplySlaToWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .Include(item => item.MaintenanceAsset)
            .Include(item => item.MaintenanceSlaPolicy)
            .FirstOrDefaultAsync(item => item.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.NotFound("Work order was not found.");
        }

        var policy = await FindBestSlaPolicyAsync(workOrder, cancellationToken);
        if (policy is null)
        {
            workOrder.SlaStatus = MaintenanceSlaStatus.NotApplied;
            workOrder.MaintenanceSlaPolicyId = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<WorkOrderSlaSnapshotResponse>.Success(ToSlaSnapshotResponse(workOrder));
        }

        ApplySla(workOrder, policy, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<WorkOrderSlaSnapshotResponse>.Success(ToSlaSnapshotResponse(workOrder));
    }

    public async Task<ServiceResult<PreventiveMaintenancePlanResponse>> CreatePreventivePlanAsync(
        CreatePreventiveMaintenancePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = TrimOrNull(request.Title);
        var description = TrimOrNull(request.Description);
        if (title is null || description is null)
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.BadRequest("Preventive maintenance title and description are required.");
        }

        if (!Enum.IsDefined(request.Cadence) || !Enum.IsDefined(request.Priority))
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.BadRequest("Preventive maintenance cadence or priority is invalid.");
        }

        if (request.Cadence == PreventiveMaintenanceCadence.CustomDays && (!request.CustomIntervalDays.HasValue || request.CustomIntervalDays <= 0))
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.BadRequest("Custom interval days are required for custom cadence.");
        }

        if (request.AssignedStaffMemberId.HasValue && request.AssignedVendorId.HasValue)
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.BadRequest("Preventive maintenance plan cannot assign both staff and vendor.");
        }

        if (request.NextDueAtUtc == default)
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.BadRequest("Preventive maintenance next due date is required.");
        }

        var asset = await dbContext.MaintenanceAssets
            .FirstOrDefaultAsync(item => item.Id == request.MaintenanceAssetId, cancellationToken);
        if (asset is null)
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.NotFound("Asset was not found.");
        }

        if (!await CanAccessCompoundAsync(asset.CompoundId, cancellationToken))
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.NotFound("Asset was not found.");
        }

        var assignmentValidation = await ValidateStaffVendorScopeAsync(asset.CompoundId, request.AssignedStaffMemberId, request.AssignedVendorId, cancellationToken);
        if (assignmentValidation is not null)
        {
            return ServiceResult<PreventiveMaintenancePlanResponse>.BadRequest(assignmentValidation);
        }

        var plan = new PreventiveMaintenancePlan
        {
            CompoundId = asset.CompoundId,
            MaintenanceAssetId = asset.Id,
            Title = title,
            Description = description,
            Cadence = request.Cadence,
            CustomIntervalDays = request.CustomIntervalDays,
            Priority = request.Priority,
            AssignedStaffMemberId = request.AssignedStaffMemberId,
            AssignedVendorId = request.AssignedVendorId,
            NextDueAtUtc = request.NextDueAtUtc,
            IsActive = request.IsActive,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow
        };

        asset.NextServiceDueAtUtc = request.NextDueAtUtc;
        dbContext.PreventiveMaintenancePlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetPreventivePlanResponseAsync(plan.Id, cancellationToken);
    }

    public async Task<PagedResult<PreventiveMaintenancePlanResponse>> SearchPreventivePlansAsync(
        PreventiveMaintenancePlanQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var plans = dbContext.PreventiveMaintenancePlans
            .AsNoTracking()
            .Include(plan => plan.MaintenanceAsset)
            .Include(plan => plan.AssignedStaffMember)
            .Include(plan => plan.AssignedVendor)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            plans = plans.Where(plan => plan.CompoundId == query.CompoundId.Value);
        }

        if (query.MaintenanceAssetId.HasValue)
        {
            plans = plans.Where(plan => plan.MaintenanceAssetId == query.MaintenanceAssetId.Value);
        }

        if (query.IsActive.HasValue)
        {
            plans = plans.Where(plan => plan.IsActive == query.IsActive.Value);
        }

        if (query.DueFromUtc.HasValue)
        {
            plans = plans.Where(plan => plan.NextDueAtUtc >= query.DueFromUtc.Value);
        }

        if (query.DueToUtc.HasValue)
        {
            plans = plans.Where(plan => plan.NextDueAtUtc <= query.DueToUtc.Value);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            plans = plans.Where(plan => plan.Title.Contains(search) || plan.MaintenanceAsset.Name.Contains(search));
        }

        plans = await ApplyCurrentCompoundAccessAsync(plans, cancellationToken);
        return await ToPagedResultAsync(plans.OrderBy(plan => plan.NextDueAtUtc), query, ToPreventivePlanResponse, cancellationToken);
    }

    public async Task<ServiceResult<WorkOrderSlaSnapshotResponse>> GeneratePreventiveWorkOrderAsync(
        Guid planId,
        Guid? currentUserId,
        GeneratePreventiveWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.PreventiveMaintenancePlans
            .Include(item => item.MaintenanceAsset)
            .FirstOrDefaultAsync(item => item.Id == planId, cancellationToken);
        if (plan is null)
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.NotFound("Preventive maintenance plan was not found.");
        }

        if (!plan.IsActive)
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.BadRequest("Inactive preventive maintenance plans cannot generate work orders.");
        }

        if (!await CanAccessCompoundAsync(plan.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.NotFound("Preventive maintenance plan was not found.");
        }

        if (request.ScheduledAtUtc.HasValue && request.ScheduledAtUtc.Value == default)
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.BadRequest("Preventive work order scheduled date is invalid.");
        }

        if (request.DueAtUtc.HasValue && request.DueAtUtc.Value == default)
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.BadRequest("Preventive work order due date is invalid.");
        }

        var scheduledAtUtc = request.ScheduledAtUtc ?? DateTime.UtcNow;
        if (request.DueAtUtc.HasValue && request.DueAtUtc.Value < scheduledAtUtc)
        {
            return ServiceResult<WorkOrderSlaSnapshotResponse>.BadRequest("Preventive work order due date cannot be before the scheduled date.");
        }
        var workOrder = new WorkOrder
        {
            Title = plan.Title,
            Description = plan.Description,
            SourceType = WorkOrderSourceType.Other,
            SourceEntityId = plan.Id,
            CompoundId = plan.CompoundId,
            Priority = plan.Priority,
            Status = plan.AssignedStaffMemberId.HasValue || plan.AssignedVendorId.HasValue
                ? WorkOrderStatus.Assigned
                : WorkOrderStatus.New,
            AssignedStaffMemberId = plan.AssignedStaffMemberId,
            AssignedVendorId = plan.AssignedVendorId,
            CreatedByUserId = currentUserId,
            MaintenanceAssetId = plan.MaintenanceAssetId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            DueAtUtc = request.DueAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
        workOrder.StatusHistory.Add(new WorkOrderStatusHistory
        {
            OldStatus = null,
            NewStatus = workOrder.Status,
            ChangedByUserId = currentUserId,
            Note = TrimOrNull(request.Note) ?? "Preventive maintenance work order generated.",
            CreatedAtUtc = DateTime.UtcNow
        });

        var policy = await FindBestSlaPolicyAsync(workOrder, cancellationToken);
        if (policy is not null)
        {
            ApplySla(workOrder, policy, scheduledAtUtc);
        }

        plan.LastGeneratedAtUtc = DateTime.UtcNow;
        plan.NextDueAtUtc = CalculateNextDue(plan.NextDueAtUtc, plan.Cadence, plan.CustomIntervalDays);
        plan.MaintenanceAsset.LastServiceAtUtc = DateTime.UtcNow;
        plan.MaintenanceAsset.NextServiceDueAtUtc = plan.NextDueAtUtc;
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<WorkOrderSlaSnapshotResponse>.Success(ToSlaSnapshotResponse(workOrder));
    }

    public async Task<ServiceResult<OperationalChecklistTemplateResponse>> CreateChecklistTemplateAsync(
        CreateOperationalChecklistTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = TrimOrNull(request.Name);
        if (name is null)
        {
            return ServiceResult<OperationalChecklistTemplateResponse>.BadRequest("Checklist template name is required.");
        }

        if (request.Items.Count == 0)
        {
            return ServiceResult<OperationalChecklistTemplateResponse>.BadRequest("At least one checklist item is required.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<OperationalChecklistTemplateResponse>.Forbidden("Current user cannot access this compound.");
        }

        var template = new OperationalChecklistTemplate
        {
            CompoundId = request.CompoundId,
            Name = name,
            Description = TrimOrNull(request.Description),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            Items = request.Items
                .Select((item, index) => new OperationalChecklistTemplateItem
                {
                    Title = TrimOrNull(item.Title) ?? $"Checklist item {index + 1}",
                    Description = TrimOrNull(item.Description),
                    IsRequired = item.IsRequired,
                    SortOrder = item.SortOrder == 0 ? index + 1 : item.SortOrder
                })
                .ToList()
        };

        dbContext.OperationalChecklistTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<OperationalChecklistTemplateResponse>.Success(ToTemplateResponse(template));
    }

    public async Task<PagedResult<OperationalChecklistTemplateResponse>> SearchChecklistTemplatesAsync(
        OperationalChecklistTemplateQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var templates = dbContext.OperationalChecklistTemplates
            .AsNoTracking()
            .Include(template => template.Items)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            templates = templates.Where(template => template.CompoundId == query.CompoundId.Value);
        }

        if (query.IsActive.HasValue)
        {
            templates = templates.Where(template => template.IsActive == query.IsActive.Value);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            templates = templates.Where(template => template.Name.Contains(search));
        }

        templates = await ApplyCurrentCompoundAccessAsync(templates, cancellationToken);
        return await ToPagedResultAsync(templates.OrderBy(template => template.Name), query, ToTemplateResponse, cancellationToken);
    }

    public async Task<ServiceResult<OperationalChecklistRunResponse>> StartChecklistRunAsync(
        Guid? currentUserId,
        StartOperationalChecklistRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.TargetType))
        {
            return ServiceResult<OperationalChecklistRunResponse>.BadRequest("Checklist target type is invalid.");
        }

        var template = await dbContext.OperationalChecklistTemplates
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == request.TemplateId, cancellationToken);
        if (template is null)
        {
            return ServiceResult<OperationalChecklistRunResponse>.NotFound("Checklist template was not found.");
        }

        if (!template.IsActive)
        {
            return ServiceResult<OperationalChecklistRunResponse>.BadRequest("Inactive checklist templates cannot be started.");
        }

        if (!await CanAccessCompoundAsync(template.CompoundId, cancellationToken))
        {
            return ServiceResult<OperationalChecklistRunResponse>.NotFound("Checklist template was not found.");
        }

        var run = new OperationalChecklistRun
        {
            CompoundId = template.CompoundId,
            OperationalChecklistTemplateId = template.Id,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            StartedByUserId = currentUserId,
            StartedAtUtc = DateTime.UtcNow,
            Items = template.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new OperationalChecklistRunItem
                {
                    Title = item.Title,
                    Description = item.Description,
                    IsRequired = item.IsRequired,
                    SortOrder = item.SortOrder,
                    Status = OperationalChecklistItemStatus.Pending
                })
                .ToList()
        };

        dbContext.OperationalChecklistRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<OperationalChecklistRunResponse>.Success(ToRunResponse(run));
    }

    public async Task<ServiceResult<OperationalChecklistRunResponse>> CompleteChecklistRunAsync(
        Guid id,
        Guid? currentUserId,
        CompleteOperationalChecklistRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var run = await dbContext.OperationalChecklistRuns
            .Include(item => item.Template)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null)
        {
            return ServiceResult<OperationalChecklistRunResponse>.NotFound("Checklist run was not found.");
        }

        if (!await CanAccessCompoundAsync(run.CompoundId, cancellationToken))
        {
            return ServiceResult<OperationalChecklistRunResponse>.NotFound("Checklist run was not found.");
        }

        if (run.Status != OperationalChecklistRunStatus.Open)
        {
            return ServiceResult<OperationalChecklistRunResponse>.Conflict("Only open checklist runs can be completed.");
        }

        var submitted = request.Items.ToDictionary(item => item.ItemId, item => item);
        foreach (var runItem in run.Items)
        {
            if (!submitted.TryGetValue(runItem.Id, out var submittedItem))
            {
                continue;
            }

            if (!Enum.IsDefined(submittedItem.Status))
            {
                return ServiceResult<OperationalChecklistRunResponse>.BadRequest("Checklist item status is invalid.");
            }

            runItem.Status = submittedItem.Status;
            runItem.Notes = TrimOrNull(submittedItem.Notes);
        }

        var unresolvedRequired = run.Items.Any(item => item.IsRequired && item.Status == OperationalChecklistItemStatus.Pending);
        if (unresolvedRequired)
        {
            return ServiceResult<OperationalChecklistRunResponse>.BadRequest("All required checklist items must be resolved before completion.");
        }

        run.Status = OperationalChecklistRunStatus.Completed;
        run.CompletedByUserId = currentUserId;
        run.CompletedAtUtc = DateTime.UtcNow;
        run.SummaryNotes = TrimOrNull(request.SummaryNotes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<OperationalChecklistRunResponse>.Success(ToRunResponse(run));
    }

    public async Task<ServiceResult<OperationalChecklistRunResponse>> GetChecklistRunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var runs = await ApplyCurrentCompoundAccessAsync(
            dbContext.OperationalChecklistRuns
                .AsNoTracking()
                .Include(run => run.Template)
                .Include(run => run.Items),
            cancellationToken);
        var run = await runs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return run is null
            ? ServiceResult<OperationalChecklistRunResponse>.NotFound("Checklist run was not found.")
            : ServiceResult<OperationalChecklistRunResponse>.Success(ToRunResponse(run));
    }

    public async Task<ServiceResult<MaintenanceSlaRefreshResponse>> RefreshSlaBreachesAsync(
        MaintenanceReliabilitySummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.CompoundId.HasValue && !await CanAccessCompoundAsync(query.CompoundId.Value, cancellationToken))
        {
            return ServiceResult<MaintenanceSlaRefreshResponse>.Forbidden("Current user cannot access this compound.");
        }

        var workOrders = await ApplyCurrentCompoundAccessAsync(dbContext.WorkOrders, cancellationToken);
        if (query.CompoundId.HasValue)
        {
            workOrders = workOrders.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        var now = DateTime.UtcNow;
        var activeWorkOrders = await workOrders
            .Where(item => item.Status != WorkOrderStatus.Completed && item.Status != WorkOrderStatus.Cancelled)
            .Where(item => item.SlaStatus == MaintenanceSlaStatus.WithinSla || item.SlaStatus == MaintenanceSlaStatus.ResponseBreached)
            .ToListAsync(cancellationToken);

        var updatedCount = 0;
        foreach (var workOrder in activeWorkOrders)
        {
            if (workOrder.ResolutionDueAtUtc.HasValue && workOrder.ResolutionDueAtUtc.Value < now)
            {
                if (workOrder.SlaStatus != MaintenanceSlaStatus.ResolutionBreached)
                {
                    workOrder.SlaStatus = MaintenanceSlaStatus.ResolutionBreached;
                    workOrder.SlaBreachedAtUtc ??= now;
                    workOrder.SlaBreachReason = "Work order resolution SLA is breached.";
                    updatedCount++;
                }

                continue;
            }

            if (!workOrder.FirstRespondedAtUtc.HasValue
                && workOrder.ResponseDueAtUtc.HasValue
                && workOrder.ResponseDueAtUtc.Value < now
                && workOrder.SlaStatus != MaintenanceSlaStatus.ResponseBreached)
            {
                workOrder.SlaStatus = MaintenanceSlaStatus.ResponseBreached;
                workOrder.SlaBreachedAtUtc ??= now;
                workOrder.SlaBreachReason = "Work order response SLA is breached.";
                updatedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var refreshed = await ApplyCurrentCompoundAccessAsync(dbContext.WorkOrders.AsNoTracking(), cancellationToken);
        if (query.CompoundId.HasValue)
        {
            refreshed = refreshed.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        return ServiceResult<MaintenanceSlaRefreshResponse>.Success(new MaintenanceSlaRefreshResponse(
            query.CompoundId,
            await refreshed.CountAsync(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached, cancellationToken),
            await refreshed.CountAsync(item => item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached, cancellationToken),
            updatedCount));
    }

    public async Task<ServiceResult<MaintenanceReliabilitySummaryResponse>> GetSummaryAsync(
        MaintenanceReliabilitySummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.CompoundId.HasValue && !await CanAccessCompoundAsync(query.CompoundId.Value, cancellationToken))
        {
            return ServiceResult<MaintenanceReliabilitySummaryResponse>.Forbidden("Current user cannot access this compound.");
        }

        var assets = await ApplyCurrentCompoundAccessAsync(dbContext.MaintenanceAssets.AsNoTracking(), cancellationToken);
        var policies = await ApplyCurrentCompoundAccessAsync(dbContext.MaintenanceSlaPolicies.AsNoTracking(), cancellationToken);
        var plans = await ApplyCurrentCompoundAccessAsync(dbContext.PreventiveMaintenancePlans.AsNoTracking(), cancellationToken);
        var runs = await ApplyCurrentCompoundAccessAsync(dbContext.OperationalChecklistRuns.AsNoTracking(), cancellationToken);
        var workOrders = await ApplyCurrentCompoundAccessAsync(dbContext.WorkOrders.AsNoTracking(), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            var compoundId = query.CompoundId.Value;
            assets = assets.Where(item => item.CompoundId == compoundId);
            policies = policies.Where(item => item.CompoundId == compoundId);
            plans = plans.Where(item => item.CompoundId == compoundId);
            runs = runs.Where(item => item.CompoundId == compoundId);
            workOrders = workOrders.Where(item => item.CompoundId == compoundId);
        }

        var now = DateTime.UtcNow;
        return ServiceResult<MaintenanceReliabilitySummaryResponse>.Success(new MaintenanceReliabilitySummaryResponse(
            query.CompoundId,
            await assets.CountAsync(item => item.Status == MaintenanceAssetStatus.Active, cancellationToken),
            await assets.CountAsync(item => item.Status == MaintenanceAssetStatus.OutOfService, cancellationToken),
            await policies.CountAsync(item => item.IsActive, cancellationToken),
            await plans.CountAsync(item => item.IsActive && item.NextDueAtUtc <= now, cancellationToken),
            await runs.CountAsync(item => item.Status == OperationalChecklistRunStatus.Open, cancellationToken),
            await workOrders.CountAsync(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached, cancellationToken),
            await workOrders.CountAsync(item => item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached, cancellationToken)));
    }


    public async Task<ServiceResult<MaintenanceReliabilityDashboardResponse>> GetProDashboardAsync(
        MaintenanceReliabilityDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.CompoundId.HasValue && !await CanAccessCompoundAsync(query.CompoundId.Value, cancellationToken))
        {
            return ServiceResult<MaintenanceReliabilityDashboardResponse>.Forbidden("Current user cannot access this compound.");
        }

        var dueWithinDays = query.DueWithinDays <= 0 ? 14 : query.DueWithinDays;
        var now = DateTime.UtcNow;
        var dueToUtc = now.AddDays(dueWithinDays);

        var assets = await ApplyCurrentCompoundAccessAsync(dbContext.MaintenanceAssets.AsNoTracking(), cancellationToken);
        var plans = await ApplyCurrentCompoundAccessAsync(dbContext.PreventiveMaintenancePlans.AsNoTracking(), cancellationToken);
        var workOrders = await ApplyCurrentCompoundAccessAsync(dbContext.WorkOrders.AsNoTracking(), cancellationToken);

        var vendors = dbContext.ServiceVendors.AsNoTracking().AsQueryable();
        var stockItems = await ApplyCurrentCompoundAccessAsync(dbContext.StockItems.AsNoTracking(), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            var compoundId = query.CompoundId.Value;
            assets = assets.Where(item => item.CompoundId == compoundId);
            plans = plans.Where(item => item.CompoundId == compoundId);
            workOrders = workOrders.Where(item => item.CompoundId == compoundId);
            stockItems = stockItems.Where(item => item.CompoundId == compoundId);
        }

        var topPreventiveRisks = await GetPreventiveMaintenanceDueQueueAsync(
            new PreventiveMaintenanceDueQueueQuery
            {
                CompoundId = query.CompoundId,
                DueWithinDays = dueWithinDays,
                IncludeOverdue = true,
                PageNumber = 1,
                PageSize = 5
            },
            cancellationToken);

        var topSlaEscalations = await GetSlaEscalationQueueAsync(
            new MaintenanceSlaEscalationQueueQuery
            {
                CompoundId = query.CompoundId,
                OpenOnly = true,
                PageNumber = 1,
                PageSize = 5
            },
            cancellationToken);

        var topVendorRisks = await GetVendorPerformanceAsync(
            new VendorPerformanceQuery
            {
                CompoundId = query.CompoundId,
                PageNumber = 1,
                PageSize = 5
            },
            cancellationToken);

        var openWorkOrders = workOrders.Where(IsOpenWorkOrderExpression());
        var actionItems = new List<MaintenanceReliabilityActionItemResponse>();

        var overduePlanCount = await plans.CountAsync(item => item.IsActive && item.NextDueAtUtc < now, cancellationToken);
        if (overduePlanCount > 0)
        {
            actionItems.Add(new MaintenanceReliabilityActionItemResponse(
                "PreventiveMaintenance",
                "High",
                null,
                $"{overduePlanCount} preventive maintenance plan(s) are overdue.",
                "Generate or reschedule the overdue preventive work orders."));
        }

        var breachedWorkOrderCount = await workOrders.CountAsync(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached || item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached || item.SlaStatus == MaintenanceSlaStatus.Escalated, cancellationToken);
        if (breachedWorkOrderCount > 0)
        {
            actionItems.Add(new MaintenanceReliabilityActionItemResponse(
                "SLA",
                "Critical",
                null,
                $"{breachedWorkOrderCount} work order(s) have breached or escalated SLA status.",
                "Review the SLA escalation queue and assign accountable owners."));
        }

        var outOfServiceAssetCount = await assets.CountAsync(item => item.Status == MaintenanceAssetStatus.OutOfService, cancellationToken);
        if (outOfServiceAssetCount > 0)
        {
            actionItems.Add(new MaintenanceReliabilityActionItemResponse(
                "Assets",
                "High",
                null,
                $"{outOfServiceAssetCount} asset(s) are out of service.",
                "Create recovery work orders and verify impact on residents."));
        }

        var lowStockCount = await stockItems.CountAsync(item => item.Status == StockItemStatus.Active && item.CurrentQuantity <= item.MinimumQuantity, cancellationToken);
        if (lowStockCount > 0)
        {
            actionItems.Add(new MaintenanceReliabilityActionItemResponse(
                "Inventory",
                "Medium",
                null,
                $"{lowStockCount} spare part item(s) are low stock.",
                "Create procurement requests before critical maintenance is blocked."));
        }

        var vendorAtRiskCount = topVendorRisks.Items.Count(item => item.RiskLevel is "High" or "Critical");

        return ServiceResult<MaintenanceReliabilityDashboardResponse>.Success(new MaintenanceReliabilityDashboardResponse(
            query.CompoundId,
            await assets.CountAsync(item => item.Status == MaintenanceAssetStatus.Active, cancellationToken),
            outOfServiceAssetCount,
            overduePlanCount,
            await plans.CountAsync(item => item.IsActive && item.NextDueAtUtc >= now && item.NextDueAtUtc <= dueToUtc, cancellationToken),
            await openWorkOrders.CountAsync(cancellationToken),
            breachedWorkOrderCount,
            await workOrders.CountAsync(item => item.Priority == WorkOrderPriority.Emergency && item.Status != WorkOrderStatus.Completed && item.Status != WorkOrderStatus.Cancelled, cancellationToken),
            await vendors.CountAsync(item => item.Status == VendorStatus.Active, cancellationToken),
            vendorAtRiskCount,
            lowStockCount,
            await openWorkOrders.SumAsync(item => item.EstimatedCost ?? 0m, cancellationToken),
            await workOrders.Where(item => item.Status == WorkOrderStatus.Completed).SumAsync(item => item.ActualCost ?? 0m, cancellationToken),
            topPreventiveRisks.Items,
            topSlaEscalations.Items,
            topVendorRisks.Items,
            actionItems));
    }

    public async Task<ServiceResult<MaintenanceAssetReliabilityProfileResponse>> GetAssetReliabilityProfileAsync(
        Guid assetId,
        MaintenanceAssetReliabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MaintenanceAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId, cancellationToken);
        if (asset is null || !await CanAccessCompoundAsync(asset.CompoundId, cancellationToken))
        {
            return ServiceResult<MaintenanceAssetReliabilityProfileResponse>.NotFound("Asset was not found.");
        }

        var workOrders = dbContext.WorkOrders
            .AsNoTracking()
            .Include(item => item.CostItems)
            .Where(item => item.MaintenanceAssetId == assetId);
        if (query.FromUtc.HasValue)
        {
            workOrders = workOrders.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            workOrders = workOrders.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        var workOrderList = await workOrders.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var totalCost = workOrderList.Sum(item => item.ActualCost ?? item.EstimatedCost ?? item.CostItems.Sum(cost => cost.Amount));
        var completedWithDuration = workOrderList
            .Where(item => item.Status == WorkOrderStatus.Completed && item.CompletedAtUtc.HasValue)
            .Select(item => (item.CompletedAtUtc!.Value - item.CreatedAtUtc).TotalHours)
            .ToArray();
        var overduePlanCount = await dbContext.PreventiveMaintenancePlans
            .AsNoTracking()
            .CountAsync(item => item.MaintenanceAssetId == assetId && item.IsActive && item.NextDueAtUtc < now, cancellationToken);
        var breachedCount = workOrderList.Count(IsSlaBreached);
        var openCount = workOrderList.Count(IsOpenWorkOrder);
        var reliabilityBand = DetermineAssetReliabilityBand(asset, openCount, breachedCount, overduePlanCount);

        return ServiceResult<MaintenanceAssetReliabilityProfileResponse>.Success(new MaintenanceAssetReliabilityProfileResponse(
            asset.Id,
            asset.CompoundId,
            asset.Name,
            asset.Code,
            asset.AssetType,
            asset.Status,
            asset.LastServiceAtUtc,
            asset.NextServiceDueAtUtc,
            workOrderList.Count,
            openCount,
            workOrderList.Count(item => item.Status == WorkOrderStatus.Completed),
            breachedCount,
            overduePlanCount,
            Math.Round(totalCost, 2),
            completedWithDuration.Length == 0 ? null : Math.Round(completedWithDuration.Average(), 2),
            reliabilityBand,
            GetAssetReliabilityAction(reliabilityBand)));
    }

    public async Task<PagedResult<PreventiveMaintenanceDueQueueItemResponse>> GetPreventiveMaintenanceDueQueueAsync(
        PreventiveMaintenanceDueQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        var dueWithinDays = query.DueWithinDays <= 0 ? 14 : query.DueWithinDays;
        var now = DateTime.UtcNow;
        var dueToUtc = now.AddDays(dueWithinDays);
        var plans = dbContext.PreventiveMaintenancePlans
            .AsNoTracking()
            .Include(item => item.MaintenanceAsset)
            .Include(item => item.AssignedStaffMember)
            .Include(item => item.AssignedVendor)
            .Where(item => item.IsActive)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            plans = plans.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.MaintenanceAssetId.HasValue)
        {
            plans = plans.Where(item => item.MaintenanceAssetId == query.MaintenanceAssetId.Value);
        }

        plans = query.IncludeOverdue
            ? plans.Where(item => item.NextDueAtUtc <= dueToUtc)
            : plans.Where(item => item.NextDueAtUtc >= now && item.NextDueAtUtc <= dueToUtc);

        plans = await ApplyCurrentCompoundAccessAsync(plans, cancellationToken);
        var totalCount = await plans.CountAsync(cancellationToken);
        var items = await plans
            .OrderBy(item => item.NextDueAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PreventiveMaintenanceDueQueueItemResponse>(
            items.Select(item => ToPreventiveDueQueueItemResponse(item, now)).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<PagedResult<MaintenanceSlaEscalationQueueItemResponse>> GetSlaEscalationQueueAsync(
        MaintenanceSlaEscalationQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var workOrders = dbContext.WorkOrders
            .AsNoTracking()
            .Include(item => item.MaintenanceAsset)
            .Include(item => item.AssignedStaffMember)
            .Include(item => item.AssignedVendor)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            workOrders = workOrders.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.OpenOnly)
        {
            workOrders = workOrders.Where(IsOpenWorkOrderExpression());
        }

        if (query.Status.HasValue)
        {
            workOrders = workOrders.Where(item => item.SlaStatus == query.Status.Value);
        }
        else
        {
            workOrders = workOrders.Where(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached
                || item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached
                || item.SlaStatus == MaintenanceSlaStatus.Escalated
                || item.Priority == WorkOrderPriority.Urgent
                || item.Priority == WorkOrderPriority.Emergency
                || (item.ResponseDueAtUtc.HasValue && item.ResponseDueAtUtc.Value < now)
                || (item.ResolutionDueAtUtc.HasValue && item.ResolutionDueAtUtc.Value < now));
        }

        if (query.MinimumPriority.HasValue)
        {
            workOrders = workOrders.Where(item => item.Priority >= query.MinimumPriority.Value);
        }

        workOrders = await ApplyCurrentCompoundAccessAsync(workOrders, cancellationToken);
        var totalCount = await workOrders.CountAsync(cancellationToken);
        var items = await workOrders
            .OrderByDescending(item => item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached || item.SlaStatus == MaintenanceSlaStatus.Escalated)
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => item.ResolutionDueAtUtc ?? item.ResponseDueAtUtc ?? item.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MaintenanceSlaEscalationQueueItemResponse>(
            items.Select(item => ToSlaEscalationQueueItemResponse(item, now)).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<PagedResult<VendorPerformanceItemResponse>> GetVendorPerformanceAsync(
        VendorPerformanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var workOrders = dbContext.WorkOrders
            .AsNoTracking()
            .Include(item => item.AssignedVendor)
            .Include(item => item.CostItems)
            .Where(item => item.AssignedVendorId.HasValue)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            workOrders = workOrders.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.VendorId.HasValue)
        {
            workOrders = workOrders.Where(item => item.AssignedVendorId == query.VendorId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            workOrders = workOrders.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            workOrders = workOrders.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        workOrders = await ApplyCurrentCompoundAccessAsync(workOrders, cancellationToken);
        var workOrderList = await workOrders.ToListAsync(cancellationToken);
        var vendorItems = workOrderList
            .GroupBy(item => item.AssignedVendorId.GetValueOrDefault())
            .Select(group => ToVendorPerformanceItemResponse(group))
            .OrderBy(item => item.ReliabilityScore)
            .ThenByDescending(item => item.SlaBreachedWorkOrderCount)
            .ToArray();

        var totalCount = vendorItems.Length;
        var items = vendorItems
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return new PagedResult<VendorPerformanceItemResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }

    private async Task<ServiceResult<PreventiveMaintenancePlanResponse>> GetPreventivePlanResponseAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.PreventiveMaintenancePlans
            .AsNoTracking()
            .Include(item => item.MaintenanceAsset)
            .Include(item => item.AssignedStaffMember)
            .Include(item => item.AssignedVendor)
            .FirstAsync(item => item.Id == planId, cancellationToken);
        return ServiceResult<PreventiveMaintenancePlanResponse>.Success(ToPreventivePlanResponse(plan));
    }

    private async Task<MaintenanceSlaPolicy?> FindBestSlaPolicyAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        return await dbContext.MaintenanceSlaPolicies
            .Where(policy => policy.CompoundId == workOrder.CompoundId && policy.IsActive)
            .OrderByDescending(policy => policy.Priority == workOrder.Priority && policy.SourceType == workOrder.SourceType)
            .ThenByDescending(policy => policy.Priority == workOrder.Priority)
            .ThenByDescending(policy => policy.SourceType == workOrder.SourceType)
            .ThenBy(policy => policy.ResolutionDueMinutes)
            .FirstOrDefaultAsync(policy =>
                (!policy.Priority.HasValue || policy.Priority == workOrder.Priority)
                && (!policy.SourceType.HasValue || policy.SourceType == workOrder.SourceType),
                cancellationToken);
    }

    private static void ApplySla(WorkOrder workOrder, MaintenanceSlaPolicy policy, DateTime baselineUtc)
    {
        workOrder.MaintenanceSlaPolicyId = policy.Id;
        workOrder.MaintenanceSlaPolicy = policy;
        workOrder.ResponseDueAtUtc = baselineUtc.AddMinutes(policy.ResponseDueMinutes);
        workOrder.ResolutionDueAtUtc = baselineUtc.AddMinutes(policy.ResolutionDueMinutes);
        workOrder.SlaStatus = MaintenanceSlaStatus.WithinSla;
        workOrder.SlaBreachReason = null;
        workOrder.SlaBreachedAtUtc = null;
    }

    private static DateTime CalculateNextDue(DateTime currentDueAtUtc, PreventiveMaintenanceCadence cadence, int? customIntervalDays)
    {
        return cadence switch
        {
            PreventiveMaintenanceCadence.Daily => currentDueAtUtc.AddDays(1),
            PreventiveMaintenanceCadence.Weekly => currentDueAtUtc.AddDays(7),
            PreventiveMaintenanceCadence.Monthly => currentDueAtUtc.AddMonths(1),
            PreventiveMaintenanceCadence.Quarterly => currentDueAtUtc.AddMonths(3),
            PreventiveMaintenanceCadence.SemiAnnual => currentDueAtUtc.AddMonths(6),
            PreventiveMaintenanceCadence.Annual => currentDueAtUtc.AddYears(1),
            PreventiveMaintenanceCadence.CustomDays => currentDueAtUtc.AddDays(customIntervalDays ?? 1),
            _ => currentDueAtUtc.AddMonths(1)
        };
    }

    private async Task<string?> ValidateAssetLocationAsync(Guid compoundId, Guid? buildingId, Guid? floorId, Guid? propertyUnitId, CancellationToken cancellationToken)
    {
        if (buildingId.HasValue)
        {
            var valid = await dbContext.Buildings.AsNoTracking().AnyAsync(item => item.Id == buildingId.Value && item.CompoundId == compoundId, cancellationToken);
            if (!valid)
            {
                return "Asset building does not belong to the selected compound.";
            }
        }

        if (floorId.HasValue)
        {
            var floor = await dbContext.Floors
                .AsNoTracking()
                .Where(item => item.Id == floorId.Value)
                .Select(item => new { item.CompoundId, item.BuildingId })
                .FirstOrDefaultAsync(cancellationToken);
            if (floor is null || floor.CompoundId != compoundId)
            {
                return "Asset floor does not belong to the selected compound.";
            }

            if (buildingId.HasValue && floor.BuildingId != buildingId.Value)
            {
                return "Asset floor does not belong to the selected building.";
            }
        }

        if (propertyUnitId.HasValue)
        {
            var unit = await dbContext.PropertyUnits
                .AsNoTracking()
                .Where(item => item.Id == propertyUnitId.Value)
                .Select(item => new { item.CompoundId, item.BuildingId, item.FloorId })
                .FirstOrDefaultAsync(cancellationToken);
            if (unit is null || unit.CompoundId != compoundId)
            {
                return "Asset unit does not belong to the selected compound.";
            }

            if (buildingId.HasValue && unit.BuildingId != buildingId.Value)
            {
                return "Asset unit does not belong to the selected building.";
            }

            if (floorId.HasValue && unit.FloorId != floorId.Value)
            {
                return "Asset unit does not belong to the selected floor.";
            }
        }

        return null;
    }

    private async Task<string?> ValidateStaffVendorScopeAsync(Guid compoundId, Guid? staffMemberId, Guid? vendorId, CancellationToken cancellationToken)
    {
        if (staffMemberId.HasValue)
        {
            var exists = await dbContext.StaffMembers.AsNoTracking().AnyAsync(item => item.Id == staffMemberId.Value, cancellationToken);
            if (!exists)
            {
                return "Assigned staff member was not found.";
            }
        }

        if (vendorId.HasValue)
        {
            var exists = await dbContext.ServiceVendors.AsNoTracking().AnyAsync(item => item.Id == vendorId.Value, cancellationToken);
            if (!exists)
            {
                return "Assigned vendor was not found.";
            }
        }

        return null;
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<IQueryable<T>> ApplyCurrentCompoundAccessAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        where T : class
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return query.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return query.Where(item => scope.AllowedCompoundIds.Contains(EF.Property<Guid>(item, "CompoundId")));
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TEntity, TResponse>(
        IQueryable<TEntity> query,
        PaginationQuery pagination,
        Func<TEntity, TResponse> mapper,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TResponse>(items.Select(mapper).ToArray(), pagination.PageNumber, pagination.PageSize, totalCount);
    }

    private static MaintenanceAssetResponse ToAssetResponse(MaintenanceAsset asset)
    {
        return new MaintenanceAssetResponse(
            asset.Id,
            asset.CompoundId,
            asset.BuildingId,
            asset.FloorId,
            asset.PropertyUnitId,
            asset.Name,
            asset.Code,
            asset.AssetType,
            asset.Status,
            asset.LocationDescription,
            asset.Manufacturer,
            asset.Model,
            asset.SerialNumber,
            asset.InstalledAtUtc,
            asset.WarrantyExpiresAtUtc,
            asset.LastServiceAtUtc,
            asset.NextServiceDueAtUtc,
            asset.Notes,
            asset.CreatedAtUtc,
            asset.UpdatedAtUtc);
    }

    private static MaintenanceSlaPolicyResponse ToSlaPolicyResponse(MaintenanceSlaPolicy policy)
    {
        return new MaintenanceSlaPolicyResponse(
            policy.Id,
            policy.CompoundId,
            policy.Name,
            policy.Priority,
            policy.SourceType,
            policy.ResponseDueMinutes,
            policy.ResolutionDueMinutes,
            policy.EscalationDueMinutes,
            policy.IsActive,
            policy.Notes,
            policy.CreatedAtUtc,
            policy.UpdatedAtUtc);
    }

    private static PreventiveMaintenancePlanResponse ToPreventivePlanResponse(PreventiveMaintenancePlan plan)
    {
        return new PreventiveMaintenancePlanResponse(
            plan.Id,
            plan.CompoundId,
            plan.MaintenanceAssetId,
            plan.MaintenanceAsset.Name,
            plan.Title,
            plan.Description,
            plan.Cadence,
            plan.CustomIntervalDays,
            plan.Priority,
            plan.AssignedStaffMemberId,
            plan.AssignedStaffMember?.FullName,
            plan.AssignedVendorId,
            plan.AssignedVendor?.Name,
            plan.NextDueAtUtc,
            plan.LastGeneratedAtUtc,
            plan.IsActive,
            plan.Notes,
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc);
    }

    private static WorkOrderSlaSnapshotResponse ToSlaSnapshotResponse(WorkOrder workOrder)
    {
        return new WorkOrderSlaSnapshotResponse(
            workOrder.Id,
            workOrder.MaintenanceAssetId,
            workOrder.MaintenanceAsset?.Name,
            workOrder.MaintenanceSlaPolicyId,
            workOrder.MaintenanceSlaPolicy?.Name,
            workOrder.SlaStatus,
            workOrder.ResponseDueAtUtc,
            workOrder.ResolutionDueAtUtc,
            workOrder.FirstRespondedAtUtc,
            workOrder.SlaBreachedAtUtc,
            workOrder.SlaBreachReason);
    }

    private static OperationalChecklistTemplateResponse ToTemplateResponse(OperationalChecklistTemplate template)
    {
        return new OperationalChecklistTemplateResponse(
            template.Id,
            template.CompoundId,
            template.Name,
            template.Description,
            template.IsActive,
            template.CreatedAtUtc,
            template.UpdatedAtUtc,
            template.Items.OrderBy(item => item.SortOrder).Select(ToTemplateItemResponse).ToArray());
    }

    private static OperationalChecklistTemplateItemResponse ToTemplateItemResponse(OperationalChecklistTemplateItem item)
    {
        return new OperationalChecklistTemplateItemResponse(item.Id, item.Title, item.Description, item.IsRequired, item.SortOrder);
    }

    private static OperationalChecklistRunResponse ToRunResponse(OperationalChecklistRun run)
    {
        var items = run.Items.OrderBy(item => item.SortOrder).Select(ToRunItemResponse).ToArray();
        var requiredItemCount = items.Count(item => item.IsRequired);
        var failedRequiredItemCount = items.Count(item => item.IsRequired && item.Status == OperationalChecklistItemStatus.Failed);
        return new OperationalChecklistRunResponse(
            run.Id,
            run.CompoundId,
            run.OperationalChecklistTemplateId,
            run.Template?.Name ?? string.Empty,
            run.TargetType,
            run.TargetId,
            run.Status,
            run.StartedByUserId,
            run.CompletedByUserId,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.SummaryNotes,
            requiredItemCount,
            failedRequiredItemCount,
            items);
    }

    private static OperationalChecklistRunItemResponse ToRunItemResponse(OperationalChecklistRunItem item)
    {
        return new OperationalChecklistRunItemResponse(
            item.Id,
            item.Title,
            item.Description,
            item.IsRequired,
            item.SortOrder,
            item.Status,
            item.Notes);
    }


    private static PreventiveMaintenanceDueQueueItemResponse ToPreventiveDueQueueItemResponse(PreventiveMaintenancePlan plan, DateTime now)
    {
        var daysUntilDue = (int)Math.Floor((plan.NextDueAtUtc.Date - now.Date).TotalDays);
        var isOverdue = plan.NextDueAtUtc < now;
        var riskLevel = isOverdue
            ? plan.Priority is WorkOrderPriority.Emergency or WorkOrderPriority.Urgent ? "Critical" : "High"
            : daysUntilDue <= 3 ? "Medium" : "Normal";
        var recommendedAction = isOverdue
            ? "Generate the preventive work order immediately or record an approved deferral."
            : "Prepare the assigned team, spare parts and access window before the due date.";

        return new PreventiveMaintenanceDueQueueItemResponse(
            plan.Id,
            plan.CompoundId,
            plan.MaintenanceAssetId,
            plan.MaintenanceAsset.Name,
            plan.MaintenanceAsset.Code,
            plan.MaintenanceAsset.Status,
            plan.Title,
            plan.Cadence,
            plan.Priority,
            plan.NextDueAtUtc,
            daysUntilDue,
            isOverdue,
            riskLevel,
            plan.AssignedStaffMemberId,
            plan.AssignedStaffMember?.FullName,
            plan.AssignedVendorId,
            plan.AssignedVendor?.Name,
            plan.LastGeneratedAtUtc,
            recommendedAction);
    }

    private static MaintenanceSlaEscalationQueueItemResponse ToSlaEscalationQueueItemResponse(WorkOrder workOrder, DateTime now)
    {
        var responseOverdueMinutes = workOrder.ResponseDueAtUtc.HasValue && workOrder.ResponseDueAtUtc.Value < now
            ? (int)Math.Ceiling((now - workOrder.ResponseDueAtUtc.Value).TotalMinutes)
            : (int?)null;
        var resolutionOverdueMinutes = workOrder.ResolutionDueAtUtc.HasValue && workOrder.ResolutionDueAtUtc.Value < now
            ? (int)Math.Ceiling((now - workOrder.ResolutionDueAtUtc.Value).TotalMinutes)
            : (int?)null;
        var escalationLevel = DetermineSlaEscalationLevel(workOrder, responseOverdueMinutes, resolutionOverdueMinutes);

        return new MaintenanceSlaEscalationQueueItemResponse(
            workOrder.Id,
            workOrder.CompoundId,
            workOrder.Title,
            workOrder.Priority,
            workOrder.Status,
            workOrder.SourceType,
            workOrder.MaintenanceAssetId,
            workOrder.MaintenanceAsset?.Name,
            workOrder.AssignedStaffMemberId,
            workOrder.AssignedStaffMember?.FullName,
            workOrder.AssignedVendorId,
            workOrder.AssignedVendor?.Name,
            workOrder.SlaStatus,
            workOrder.ResponseDueAtUtc,
            workOrder.ResolutionDueAtUtc,
            workOrder.FirstRespondedAtUtc,
            workOrder.SlaBreachedAtUtc,
            responseOverdueMinutes,
            resolutionOverdueMinutes,
            escalationLevel,
            GetSlaEscalationAction(escalationLevel));
    }

    private static VendorPerformanceItemResponse ToVendorPerformanceItemResponse(IEnumerable<WorkOrder> vendorWorkOrders)
    {
        var orders = vendorWorkOrders.ToArray();
        var first = orders[0];
        var completed = orders.Where(item => item.Status == WorkOrderStatus.Completed).ToArray();
        var completedWithDuration = completed
            .Where(item => item.CompletedAtUtc.HasValue)
            .Select(item => (item.CompletedAtUtc!.Value - item.CreatedAtUtc).TotalHours)
            .ToArray();
        var breachedCount = orders.Count(IsSlaBreached);
        var cancelledCount = orders.Count(item => item.Status == WorkOrderStatus.Cancelled);
        var openCount = orders.Count(IsOpenWorkOrder);
        var totalCost = orders.Sum(item => item.ActualCost ?? item.EstimatedCost ?? item.CostItems.Sum(cost => cost.Amount));
        var reliabilityScore = CalculateVendorReliabilityScore(orders.Length, completed.Length, breachedCount, cancelledCount, openCount);
        var riskLevel = reliabilityScore < 60m ? "Critical" : reliabilityScore < 75m ? "High" : reliabilityScore < 90m ? "Medium" : "Normal";

        return new VendorPerformanceItemResponse(
            first.AssignedVendorId!.Value,
            first.AssignedVendor?.Name ?? "Unknown vendor",
            first.AssignedVendor?.ServiceType ?? VendorServiceType.Other,
            first.AssignedVendor?.Status ?? VendorStatus.Inactive,
            orders.Length,
            openCount,
            completed.Length,
            cancelledCount,
            breachedCount,
            Math.Round(totalCost, 2),
            completedWithDuration.Length == 0 ? null : Math.Round(completedWithDuration.Average(), 2),
            reliabilityScore,
            riskLevel,
            GetVendorPerformanceAction(riskLevel));
    }

    private static System.Linq.Expressions.Expression<Func<WorkOrder, bool>> IsOpenWorkOrderExpression()
    {
        return item => item.Status != WorkOrderStatus.Completed && item.Status != WorkOrderStatus.Cancelled;
    }

    private static bool IsOpenWorkOrder(WorkOrder workOrder)
    {
        return workOrder.Status != WorkOrderStatus.Completed && workOrder.Status != WorkOrderStatus.Cancelled;
    }

    private static bool IsSlaBreached(WorkOrder workOrder)
    {
        return workOrder.SlaStatus is MaintenanceSlaStatus.ResponseBreached or MaintenanceSlaStatus.ResolutionBreached or MaintenanceSlaStatus.Escalated;
    }

    private static string DetermineSlaEscalationLevel(WorkOrder workOrder, int? responseOverdueMinutes, int? resolutionOverdueMinutes)
    {
        if (workOrder.SlaStatus == MaintenanceSlaStatus.Escalated
            || workOrder.Priority == WorkOrderPriority.Emergency
            || (resolutionOverdueMinutes ?? 0) >= 240)
        {
            return "Critical";
        }

        if (workOrder.SlaStatus == MaintenanceSlaStatus.ResolutionBreached
            || (resolutionOverdueMinutes ?? 0) > 0
            || (responseOverdueMinutes ?? 0) >= 120)
        {
            return "High";
        }

        if (workOrder.SlaStatus == MaintenanceSlaStatus.ResponseBreached
            || (responseOverdueMinutes ?? 0) > 0
            || workOrder.Priority == WorkOrderPriority.Urgent)
        {
            return "Medium";
        }

        return "Normal";
    }

    private static string GetSlaEscalationAction(string escalationLevel)
    {
        return escalationLevel switch
        {
            "Critical" => "Escalate to operations management and assign immediate recovery ownership.",
            "High" => "Assign a responsible owner and update the resident or requester with a recovery ETA.",
            "Medium" => "Review response timing and prevent the work order from becoming a resolution breach.",
            _ => "Monitor within the normal maintenance queue."
        };
    }

    private static decimal CalculateVendorReliabilityScore(int total, int completed, int breached, int cancelled, int open)
    {
        if (total == 0)
        {
            return 100m;
        }

        var completionRate = completed / (decimal)total;
        var breachPenalty = breached * 8m;
        var cancellationPenalty = cancelled * 6m;
        var openLoadPenalty = Math.Max(0, open - completed) * 2m;
        var score = 100m - ((1m - completionRate) * 30m) - breachPenalty - cancellationPenalty - openLoadPenalty;
        if (score < 0m)
        {
            score = 0m;
        }

        if (score > 100m)
        {
            score = 100m;
        }

        return Math.Round(score, 2);
    }

    private static string GetVendorPerformanceAction(string riskLevel)
    {
        return riskLevel switch
        {
            "Critical" => "Freeze new non-essential assignments until vendor recovery is reviewed.",
            "High" => "Require a vendor performance review and corrective action plan.",
            "Medium" => "Monitor next assignments and confirm SLA expectations before dispatch.",
            _ => "Vendor performance is acceptable."
        };
    }

    private static string DetermineAssetReliabilityBand(MaintenanceAsset asset, int openWorkOrderCount, int breachedWorkOrderCount, int overduePreventivePlanCount)
    {
        if (asset.Status == MaintenanceAssetStatus.OutOfService || breachedWorkOrderCount >= 3 || overduePreventivePlanCount >= 2)
        {
            return "Critical";
        }

        if (asset.Status == MaintenanceAssetStatus.UnderMaintenance || openWorkOrderCount >= 3 || breachedWorkOrderCount > 0 || overduePreventivePlanCount > 0)
        {
            return "AtRisk";
        }

        if (asset.NextServiceDueAtUtc.HasValue && asset.NextServiceDueAtUtc.Value < DateTime.UtcNow.AddDays(14))
        {
            return "Watch";
        }

        return "Healthy";
    }

    private static string GetAssetReliabilityAction(string reliabilityBand)
    {
        return reliabilityBand switch
        {
            "Critical" => "Open an urgent recovery plan, verify spare parts, and assign an accountable owner.",
            "AtRisk" => "Review open work orders and overdue preventive maintenance before service quality degrades.",
            "Watch" => "Schedule upcoming service and confirm vendor or technician availability.",
            _ => "Continue normal preventive maintenance."
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

