using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class OperationsService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IOperationsService
{
    private const int MaxTitleLength = 150;
    private const int MaxDescriptionLength = 2000;
    private const int MaxNoteLength = 1000;
    private const int MaxCostDescriptionLength = 300;

    public async Task<ServiceResult<WorkOrderResponse>> CreateWorkOrderAsync(
        Guid? currentUserId,
        CreateWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var compoundResult = await ResolveWorkOrderCompoundIdAsync(
            request.CompoundId,
            request.PropertyUnitId,
            request.SourceType,
            request.SourceEntityId,
            cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<WorkOrderResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Work order compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.Forbidden("Current user cannot access this compound.");
        }

        var validation = await ValidateWorkOrderRequestAsync(
            request.Title,
            request.Description,
            request.SourceType,
            request.Priority,
            request.AssignedStaffMemberId,
            request.AssignedVendorId,
            request.PropertyUnitId,
            request.ScheduledAtUtc,
            request.DueAtUtc,
            request.EstimatedCost,
            actualCost: null,
            createdAtUtc: now,
            cancellationToken);
        if (validation is not null)
        {
            return ToResult<WorkOrderResponse>(validation);
        }

        var status = GetInitialWorkOrderStatus(
            request.AssignedStaffMemberId,
            request.AssignedVendorId,
            request.ScheduledAtUtc);
        var workOrder = new WorkOrder
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            SourceType = request.SourceType,
            SourceEntityId = request.SourceEntityId,
            CompoundId = compoundResult.Value,
            Priority = request.Priority,
            Status = status,
            AssignedStaffMemberId = request.AssignedStaffMemberId,
            AssignedVendorId = request.AssignedVendorId,
            CreatedByUserId = currentUserId,
            PropertyUnitId = request.PropertyUnitId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            DueAtUtc = request.DueAtUtc,
            EstimatedCost = request.EstimatedCost,
            CreatedAtUtc = now
        };
        workOrder.StatusHistory.Add(new WorkOrderStatusHistory
        {
            OldStatus = null,
            NewStatus = status,
            ChangedByUserId = currentUserId,
            Note = "Work order created.",
            CreatedAtUtc = now
        });

        dbContext.WorkOrders.Add(workOrder);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return await GetWorkOrderAsync(workOrder.Id, cancellationToken);
    }

    public async Task<ServiceResult<WorkOrderResponse>> UpdateWorkOrderAsync(
        Guid id,
        UpdateWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (workOrder.Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Completed or cancelled work orders cannot be updated.");
        }

        var compoundResult = await ResolveWorkOrderCompoundIdAsync(
            request.CompoundId,
            request.PropertyUnitId,
            request.SourceType,
            request.SourceEntityId,
            cancellationToken);
        if (!compoundResult.IsSuccess)
        {
            return ToResult<WorkOrderResponse>(new ValidationFailure(compoundResult.Status, compoundResult.Message ?? "Work order compound scope is invalid."));
        }

        if (!await CanAccessCompoundAsync(compoundResult.Value, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.Forbidden("Current user cannot access this compound.");
        }

        var validation = await ValidateWorkOrderRequestAsync(
            request.Title,
            request.Description,
            request.SourceType,
            request.Priority,
            staffMemberId: null,
            vendorId: null,
            request.PropertyUnitId,
            scheduledAtUtc: null,
            request.DueAtUtc,
            request.EstimatedCost,
            actualCost: null,
            workOrder.CreatedAtUtc,
            cancellationToken);
        if (validation is not null)
        {
            return ToResult<WorkOrderResponse>(validation);
        }

        workOrder.Title = request.Title.Trim();
        workOrder.Description = request.Description.Trim();
        workOrder.SourceType = request.SourceType;
        workOrder.SourceEntityId = request.SourceEntityId;
        workOrder.CompoundId = compoundResult.Value;
        workOrder.Priority = request.Priority;
        workOrder.PropertyUnitId = request.PropertyUnitId;
        workOrder.DueAtUtc = request.DueAtUtc;
        workOrder.EstimatedCost = request.EstimatedCost;
        workOrder.UpdatedAtUtc = DateTime.UtcNow;

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return await GetWorkOrderAsync(workOrder.Id, cancellationToken);
    }

    public async Task<ServiceResult<WorkOrderResponse>> GetWorkOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scopedQuery = await ApplyCurrentCompoundAccessAsync(
            GetWorkOrderDetailsQuery(asNoTracking: true),
            cancellationToken);
        var workOrder = await scopedQuery
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return workOrder is null
            ? ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.")
            : ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<PagedResult<WorkOrderResponse>> SearchWorkOrdersAsync(
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var workOrders = ApplyWorkOrderFilters(GetWorkOrderDetailsQuery(asNoTracking: true), query);
        workOrders = await ApplyCurrentCompoundAccessAsync(workOrders, cancellationToken);
        return await ToPagedWorkOrderResultAsync(workOrders, query, cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<WorkOrderResponse>>> SearchWorkOrdersForStaffMemberAsync(
        Guid staffMemberId,
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.StaffMembers
            .AsNoTracking()
            .AnyAsync(staffMember => staffMember.Id == staffMemberId, cancellationToken);
        if (!exists)
        {
            return ServiceResult<PagedResult<WorkOrderResponse>>.NotFound("Staff member was not found.");
        }

        var workOrders = ApplyWorkOrderFilters(GetWorkOrderDetailsQuery(asNoTracking: true), query)
            .Where(workOrder => workOrder.AssignedStaffMemberId == staffMemberId);
        workOrders = await ApplyCurrentCompoundAccessAsync(workOrders, cancellationToken);

        return ServiceResult<PagedResult<WorkOrderResponse>>.Success(
            await ToPagedWorkOrderResultAsync(workOrders, query, cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<WorkOrderResponse>>> SearchWorkOrdersForVendorAsync(
        Guid vendorId,
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.ServiceVendors
            .AsNoTracking()
            .AnyAsync(vendor => vendor.Id == vendorId, cancellationToken);
        if (!exists)
        {
            return ServiceResult<PagedResult<WorkOrderResponse>>.NotFound("Service vendor was not found.");
        }

        var workOrders = ApplyWorkOrderFilters(GetWorkOrderDetailsQuery(asNoTracking: true), query)
            .Where(workOrder => workOrder.AssignedVendorId == vendorId);
        workOrders = await ApplyCurrentCompoundAccessAsync(workOrders, cancellationToken);

        return ServiceResult<PagedResult<WorkOrderResponse>>.Success(
            await ToPagedWorkOrderResultAsync(workOrders, query, cancellationToken));
    }

    public async Task<ServiceResult<WorkOrderResponse>> AssignWorkOrderToStaffAsync(
        Guid id,
        Guid? currentUserId,
        AssignWorkOrderToStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Current user is invalid.");
        }

        if (request.StaffMemberId == Guid.Empty)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Staff member id is required.");
        }

        var staffMember = await dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.StaffMemberId, cancellationToken);
        if (staffMember is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Staff member was not found.");
        }

        if (staffMember.Status != StaffStatus.Active)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Only active staff members can be assigned.");
        }

        var workOrder = await GetWorkOrderDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        var validation = ValidateCanChangeActiveWorkOrder(workOrder);
        if (validation is not null)
        {
            return ToResult<WorkOrderResponse>(validation);
        }


        var oldStatus = workOrder.Status;
        workOrder.AssignedStaffMemberId = request.StaffMemberId;
        workOrder.AssignedVendorId = null;
        if (workOrder.Status != WorkOrderStatus.InProgress)
        {
            workOrder.Status = WorkOrderStatus.Assigned;
        }

        workOrder.UpdatedAtUtc = DateTime.UtcNow;
        AddStatusHistory(workOrder, oldStatus, workOrder.Status, currentUserId.Value, TrimOrNull(request.Note) ?? "Assigned to staff.");
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<ServiceResult<WorkOrderResponse>> AssignWorkOrderToVendorAsync(
        Guid id,
        Guid? currentUserId,
        AssignWorkOrderToVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Current user is invalid.");
        }

        if (request.VendorId == Guid.Empty)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Vendor id is required.");
        }

        var vendor = await dbContext.ServiceVendors
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.VendorId, cancellationToken);
        if (vendor is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Service vendor was not found.");
        }

        if (vendor.Status != VendorStatus.Active)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Only active vendors can be assigned.");
        }

        var workOrder = await GetWorkOrderDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        var validation = ValidateCanChangeActiveWorkOrder(workOrder);
        if (validation is not null)
        {
            return ToResult<WorkOrderResponse>(validation);
        }

        var oldStatus = workOrder.Status;
        workOrder.AssignedVendorId = request.VendorId;
        workOrder.AssignedStaffMemberId = null;
        if (workOrder.Status != WorkOrderStatus.InProgress)
        {
            workOrder.Status = WorkOrderStatus.Assigned;
        }

        workOrder.UpdatedAtUtc = DateTime.UtcNow;
        AddStatusHistory(workOrder, oldStatus, workOrder.Status, currentUserId.Value, TrimOrNull(request.Note) ?? "Assigned to vendor.");
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<ServiceResult<WorkOrderResponse>> ScheduleWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        ScheduleWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Current user is invalid.");
        }

        var workOrder = await GetWorkOrderDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        var stateValidation = ValidateCanChangeActiveWorkOrder(workOrder);
        if (stateValidation is not null)
        {
            return ToResult<WorkOrderResponse>(stateValidation);
        }

        if (request.ScheduledAtUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Scheduled time cannot be in the past.");
        }

        var validation = ValidateSchedule(request.ScheduledAtUtc, request.DueAtUtc, workOrder.CreatedAtUtc);
        if (validation is not null)
        {
            return ToResult<WorkOrderResponse>(validation);
        }

        var oldStatus = workOrder.Status;
        workOrder.ScheduledAtUtc = request.ScheduledAtUtc;
        workOrder.DueAtUtc = request.DueAtUtc ?? workOrder.DueAtUtc;
        workOrder.Status = WorkOrderStatus.Scheduled;
        workOrder.UpdatedAtUtc = DateTime.UtcNow;
        AddStatusHistory(workOrder, oldStatus, workOrder.Status, currentUserId.Value, TrimOrNull(request.Note) ?? "Work order scheduled.");
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<ServiceResult<WorkOrderResponse>> StartWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        StartWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Current user is invalid.");
        }

        var workOrder = await GetWorkOrderDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        var validation = ValidateCanChangeActiveWorkOrder(workOrder);
        if (validation is not null)
        {
            return ToResult<WorkOrderResponse>(validation);
        }

        if (!workOrder.AssignedStaffMemberId.HasValue && !workOrder.AssignedVendorId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Work order must be assigned before it can be started.");
        }

        if (workOrder.Status is not (WorkOrderStatus.Assigned or WorkOrderStatus.Scheduled))
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Only assigned or scheduled work orders can be started.");
        }

        var oldStatus = workOrder.Status;
        var startedAtUtc = DateTime.UtcNow;
        workOrder.Status = WorkOrderStatus.InProgress;
        workOrder.StartedAtUtc ??= startedAtUtc;
        workOrder.FirstRespondedAtUtc ??= startedAtUtc;
        if (workOrder.SlaStatus == MaintenanceSlaStatus.WithinSla
            && workOrder.ResponseDueAtUtc.HasValue
            && startedAtUtc > workOrder.ResponseDueAtUtc.Value)
        {
            workOrder.SlaStatus = MaintenanceSlaStatus.ResponseBreached;
            workOrder.SlaBreachedAtUtc ??= startedAtUtc;
            workOrder.SlaBreachReason ??= "Work order response SLA was breached before start.";
        }

        workOrder.UpdatedAtUtc = startedAtUtc;
        AddStatusHistory(workOrder, oldStatus, workOrder.Status, currentUserId.Value, TrimOrNull(request.Note) ?? "Work order started.");
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<ServiceResult<WorkOrderResponse>> CompleteWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        CompleteWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Current user is invalid.");
        }

        if (request.ActualCost < 0)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Actual cost cannot be negative.");
        }

        var workOrder = await GetWorkOrderDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (workOrder.Status == WorkOrderStatus.Cancelled)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Cancelled work orders cannot be completed.");
        }

        if (workOrder.Status != WorkOrderStatus.InProgress)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Work order must be in progress before completion.");
        }

        var oldStatus = workOrder.Status;
        var completedAtUtc = DateTime.UtcNow;
        workOrder.Status = WorkOrderStatus.Completed;
        workOrder.CompletedAtUtc = completedAtUtc;
        workOrder.SlaStatus = MaintenanceSlaStatus.Completed;
        workOrder.ActualCost = request.ActualCost ?? workOrder.ActualCost;
        workOrder.CompletionNotes = TrimOrNull(request.CompletionNotes);
        workOrder.UpdatedAtUtc = completedAtUtc;
        AddStatusHistory(workOrder, oldStatus, workOrder.Status, currentUserId.Value, "Work order completed.");
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<ServiceResult<WorkOrderResponse>> CancelWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        CancelWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Current user is invalid.");
        }

        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Cancellation reason is required.");
        }

        var workOrder = await GetWorkOrderDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderResponse>.NotFound("Work order was not found.");
        }

        if (workOrder.Status == WorkOrderStatus.Completed)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Completed work orders cannot be cancelled.");
        }

        if (workOrder.Status == WorkOrderStatus.Cancelled)
        {
            return ServiceResult<WorkOrderResponse>.BadRequest("Work order is already cancelled.");
        }

        var oldStatus = workOrder.Status;
        workOrder.Status = WorkOrderStatus.Cancelled;
        workOrder.CancelledAtUtc = DateTime.UtcNow;
        workOrder.SlaStatus = MaintenanceSlaStatus.Cancelled;
        workOrder.CancellationReason = reason;
        workOrder.UpdatedAtUtc = DateTime.UtcNow;
        AddStatusHistory(workOrder, oldStatus, workOrder.Status, currentUserId.Value, reason);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderResponse>.Success(ToWorkOrderResponse(workOrder));
    }

    public async Task<ServiceResult<WorkOrderCostItemResponse>> AddCostItemAsync(
        Guid id,
        AddWorkOrderCostItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderCostItemResponse>.NotFound("Work order was not found.");
        }

        if (!await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderCostItemResponse>.NotFound("Work order was not found.");
        }

        var stateValidation = ValidateCanChangeActiveWorkOrder(workOrder);
        if (stateValidation is not null)
        {
            return ToResult<WorkOrderCostItemResponse>(stateValidation);
        }

        var description = TrimOrNull(request.Description);
        if (description is null)
        {
            return ServiceResult<WorkOrderCostItemResponse>.BadRequest("Cost item description is required.");
        }

        if (description.Length > MaxCostDescriptionLength)
        {
            return ServiceResult<WorkOrderCostItemResponse>.BadRequest("Cost item description is too long.");
        }

        if (!Enum.IsDefined(request.CostType))
        {
            return ServiceResult<WorkOrderCostItemResponse>.BadRequest("Cost type is invalid.");
        }

        if (request.Amount < 0)
        {
            return ServiceResult<WorkOrderCostItemResponse>.BadRequest("Cost amount cannot be negative.");
        }

        var costItem = new WorkOrderCostItem
        {
            WorkOrderId = id,
            Description = description,
            CostType = request.CostType,
            Amount = request.Amount
        };
        workOrder.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.WorkOrderCostItems.Add(costItem);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderCostItemResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<WorkOrderCostItemResponse>.Success(ToWorkOrderCostItemResponse(costItem));
    }

    public async Task<ServiceResult<PagedResult<WorkOrderStatusHistoryResponse>>> GetStatusHistoryAsync(
        Guid id,
        WorkOrderStatusHistoryQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scopedWorkOrders = await ApplyCurrentCompoundAccessAsync(
            dbContext.WorkOrders.AsNoTracking(),
            cancellationToken);
        var exists = await scopedWorkOrders
            .AnyAsync(workOrder => workOrder.Id == id, cancellationToken);
        if (!exists)
        {
            return ServiceResult<PagedResult<WorkOrderStatusHistoryResponse>>.NotFound("Work order was not found.");
        }

        var history = dbContext.WorkOrderStatusHistories
            .AsNoTracking()
            .Include(item => item.ChangedByUser)
            .Where(item => item.WorkOrderId == id)
            .OrderByDescending(item => item.CreatedAtUtc);

        var totalCount = await history.CountAsync(cancellationToken);
        var items = await history
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new WorkOrderStatusHistoryResponse(
                item.Id,
                item.WorkOrderId,
                item.OldStatus,
                item.NewStatus,
                item.ChangedByUserId,
                item.ChangedByUser == null ? null : item.ChangedByUser.FullName,
                item.Note,
                item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<WorkOrderStatusHistoryResponse>>.Success(
            new PagedResult<WorkOrderStatusHistoryResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<WorkOrderRatingResponse>> RateWorkOrderAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        CreateWorkOrderRatingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<WorkOrderRatingResponse>.BadRequest("Current user is invalid.");
        }

        if (request.Rating is < 1 or > 5)
        {
            return ServiceResult<WorkOrderRatingResponse>.BadRequest("Rating must be between 1 and 5.");
        }

        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workOrder is null)
        {
            return ServiceResult<WorkOrderRatingResponse>.NotFound("Work order was not found.");
        }

        if (workOrder.Status != WorkOrderStatus.Completed)
        {
            return ServiceResult<WorkOrderRatingResponse>.BadRequest("Only completed work orders can be rated.");
        }

        if (isManager && !await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderRatingResponse>.NotFound("Work order was not found.");
        }

        if (!isManager)
        {
            if (!await CanResidentRateWorkOrderAsync(workOrder, currentUserId.Value, cancellationToken))
            {
                return ServiceResult<WorkOrderRatingResponse>.NotFound("Work order was not found.");
            }
        }

        var existingRating = await dbContext.WorkOrderRatings
            .AsNoTracking()
            .AnyAsync(rating =>
                rating.WorkOrderId == id && rating.UserId == currentUserId.Value,
                cancellationToken);
        if (existingRating)
        {
            return ServiceResult<WorkOrderRatingResponse>.Conflict("Work order has already been rated by this user.");
        }

        var rating = new WorkOrderRating
        {
            WorkOrderId = id,
            UserId = currentUserId.Value,
            Rating = request.Rating,
            Comment = TrimOrNull(request.Comment)
        };

        dbContext.WorkOrderRatings.Add(rating);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<WorkOrderRatingResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        var userName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == currentUserId.Value)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<WorkOrderRatingResponse>.Success(ToWorkOrderRatingResponse(rating, userName));
    }

    public async Task<PagedResult<WorkOrderResponse>> SearchOverdueWorkOrdersAsync(
        WorkOrderQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var workOrders = ApplyWorkOrderFilters(GetWorkOrderDetailsQuery(asNoTracking: true), query)
            .Where(workOrder =>
                workOrder.DueAtUtc.HasValue
                && workOrder.DueAtUtc.Value < now
                && workOrder.Status != WorkOrderStatus.Completed
                && workOrder.Status != WorkOrderStatus.Cancelled);
        workOrders = await ApplyCurrentCompoundAccessAsync(workOrders, cancellationToken);

        return await ToPagedWorkOrderResultAsync(workOrders, query, cancellationToken);
    }


    private async Task<ServiceResult<T>?> SaveChangesWithConcurrencyGuardAsync<T>(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<T>.Conflict("The work order was updated by another operation. Reload and try again.");
        }
    }

    private static IQueryable<WorkOrder> ApplyWorkOrderFilters(
        IQueryable<WorkOrder> workOrders,
        WorkOrderQueryRequest query)
    {
        if (query.Status.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.Priority == query.Priority.Value);
        }

        if (query.SourceType.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.SourceType == query.SourceType.Value);
        }

        if (query.SourceEntityId.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.SourceEntityId == query.SourceEntityId.Value);
        }

        if (query.CompoundId.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.CompoundId == query.CompoundId.Value);
        }

        if (query.AssignedStaffMemberId.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.AssignedStaffMemberId == query.AssignedStaffMemberId.Value);
        }

        if (query.AssignedVendorId.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.AssignedVendorId == query.AssignedVendorId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.DueFromUtc.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.DueAtUtc >= query.DueFromUtc.Value);
        }

        if (query.DueToUtc.HasValue)
        {
            workOrders = workOrders.Where(workOrder => workOrder.DueAtUtc <= query.DueToUtc.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            workOrders = workOrders.Where(workOrder =>
                workOrder.Title.Contains(searchTerm)
                || workOrder.Description.Contains(searchTerm));
        }

        return workOrders;
    }

    private IQueryable<WorkOrder> GetWorkOrderDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.WorkOrders
            .Include(workOrder => workOrder.AssignedStaffMember)
            .Include(workOrder => workOrder.AssignedVendor)
            .Include(workOrder => workOrder.CreatedByUser)
            .Include(workOrder => workOrder.PropertyUnit)
            .Include(workOrder => workOrder.Compound)
            .Include(workOrder => workOrder.CostItems)
            .Include(workOrder => workOrder.Ratings)
            .AsSplitQuery()
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<PagedResult<WorkOrderResponse>> ToPagedWorkOrderResultAsync(
        IQueryable<WorkOrder> query,
        WorkOrderQueryRequest pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var workOrders = await query
            .OrderByDescending(workOrder => workOrder.CreatedAtUtc)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = workOrders
            .Select(ToWorkOrderResponse)
            .ToArray();

        return new PagedResult<WorkOrderResponse>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    private async Task<bool> CanResidentRateWorkOrderAsync(
        WorkOrder workOrder,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (workOrder.PropertyUnitId.HasValue)
        {
            var hasActiveUnitOccupancy = await dbContext.OccupancyRecords
                .AsNoTracking()
                .AnyAsync(record =>
                    record.PropertyUnitId == workOrder.PropertyUnitId.Value
                    && record.OccupancyStatus == OccupancyStatus.Active
                    && record.ResidentProfile.UserId == userId
                    && record.ResidentProfile.IsActive,
                    cancellationToken);
            if (hasActiveUnitOccupancy)
            {
                return true;
            }
        }

        if (!workOrder.SourceEntityId.HasValue)
        {
            return false;
        }

        var sourceEntityId = workOrder.SourceEntityId.Value;
        return workOrder.SourceType switch
        {
            WorkOrderSourceType.MaintenanceRequest => await dbContext.MaintenanceRequests
                .AsNoTracking()
                .AnyAsync(request =>
                    request.Id == sourceEntityId
                    && request.ResidentProfile.UserId == userId
                    && request.ResidentProfile.IsActive,
                    cancellationToken),
            WorkOrderSourceType.Complaint => await dbContext.Complaints
                .AsNoTracking()
                .AnyAsync(complaint =>
                    complaint.Id == sourceEntityId
                    && complaint.ResidentProfile.UserId == userId
                    && complaint.ResidentProfile.IsActive,
                    cancellationToken),
            WorkOrderSourceType.Violation => await dbContext.Violations
                .AsNoTracking()
                .AnyAsync(violation =>
                    violation.Id == sourceEntityId
                    && violation.ResidentProfile != null
                    && violation.ResidentProfile.UserId == userId
                    && violation.ResidentProfile.IsActive,
                    cancellationToken),
            _ => false
        };
    }

    private async Task<IQueryable<WorkOrder>> ApplyCurrentCompoundAccessAsync(
        IQueryable<WorkOrder> workOrders,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return workOrders;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return workOrders.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return workOrders;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return workOrders.Where(_ => false);
        }

        return workOrders.Where(workOrder => scope.AllowedCompoundIds.Contains(workOrder.CompoundId));
    }

    private async Task<bool> CanAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<ServiceResult<Guid>> ResolveWorkOrderCompoundIdAsync(
        Guid? requestedCompoundId,
        Guid? propertyUnitId,
        WorkOrderSourceType sourceType,
        Guid? sourceEntityId,
        CancellationToken cancellationToken)
    {
        if (propertyUnitId.HasValue)
        {
            var unitCompoundId = await dbContext.PropertyUnits
                .AsNoTracking()
                .Where(unit => unit.Id == propertyUnitId.Value)
                .Select(unit => (Guid?)unit.CompoundId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!unitCompoundId.HasValue)
            {
                return ServiceResult<Guid>.NotFound("Property unit was not found.");
            }

            if (requestedCompoundId.HasValue && requestedCompoundId.Value != unitCompoundId.Value)
            {
                return ServiceResult<Guid>.BadRequest("Work order compound must match the selected property unit compound.");
            }

            return ServiceResult<Guid>.Success(unitCompoundId.Value);
        }

        if (sourceEntityId.HasValue)
        {
            var sourceCompoundId = await ResolveSourceCompoundIdAsync(sourceType, sourceEntityId.Value, cancellationToken);
            if (sourceCompoundId.HasValue)
            {
                if (requestedCompoundId.HasValue && requestedCompoundId.Value != sourceCompoundId.Value)
                {
                    return ServiceResult<Guid>.BadRequest("Work order compound must match the source entity compound.");
                }

                return ServiceResult<Guid>.Success(sourceCompoundId.Value);
            }
        }

        if (!requestedCompoundId.HasValue || requestedCompoundId.Value == Guid.Empty)
        {
            return ServiceResult<Guid>.BadRequest("Compound id is required for common-area or manual work orders.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == requestedCompoundId.Value && compound.IsActive, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<Guid>.NotFound("Compound was not found.");
        }

        return ServiceResult<Guid>.Success(requestedCompoundId.Value);
    }

    private async Task<Guid?> ResolveSourceCompoundIdAsync(
        WorkOrderSourceType sourceType,
        Guid sourceEntityId,
        CancellationToken cancellationToken)
    {
        return sourceType switch
        {
            WorkOrderSourceType.MaintenanceRequest => await dbContext.MaintenanceRequests
                .AsNoTracking()
                .Where(request => request.Id == sourceEntityId)
                .Select(request => (Guid?)request.CompoundId)
                .FirstOrDefaultAsync(cancellationToken),
            WorkOrderSourceType.Complaint => await dbContext.Complaints
                .AsNoTracking()
                .Where(complaint => complaint.Id == sourceEntityId)
                .Select(complaint => (Guid?)complaint.CompoundId)
                .FirstOrDefaultAsync(cancellationToken),
            WorkOrderSourceType.Violation => await dbContext.Violations
                .AsNoTracking()
                .Where(violation => violation.Id == sourceEntityId)
                .Select(violation => (Guid?)violation.CompoundId)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    private async Task<ValidationFailure?> ValidateWorkOrderRequestAsync(
        string title,
        string description,
        WorkOrderSourceType sourceType,
        WorkOrderPriority priority,
        Guid? staffMemberId,
        Guid? vendorId,
        Guid? propertyUnitId,
        DateTime? scheduledAtUtc,
        DateTime? dueAtUtc,
        decimal? estimatedCost,
        decimal? actualCost,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (TrimOrNull(title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order title is required.");
        }

        if (title.Trim().Length > MaxTitleLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order title is too long.");
        }

        if (TrimOrNull(description) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order description is required.");
        }

        if (description.Trim().Length > MaxDescriptionLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order description is too long.");
        }

        if (!Enum.IsDefined(sourceType))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order source type is invalid.");
        }

        if (!Enum.IsDefined(priority))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order priority is invalid.");
        }

        if (staffMemberId.HasValue && vendorId.HasValue)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work order can only be assigned to staff or vendor, not both.");
        }

        if (estimatedCost < 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Estimated cost cannot be negative.");
        }

        if (actualCost < 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Actual cost cannot be negative.");
        }

        var scheduleValidation = ValidateSchedule(scheduledAtUtc, dueAtUtc, createdAtUtc);
        if (scheduleValidation is not null)
        {
            return scheduleValidation;
        }

        if (staffMemberId.HasValue)
        {
            var staffStatus = await dbContext.StaffMembers
                .AsNoTracking()
                .Where(staffMember => staffMember.Id == staffMemberId.Value)
                .Select(staffMember => (StaffStatus?)staffMember.Status)
                .FirstOrDefaultAsync(cancellationToken);
            if (!staffStatus.HasValue)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Staff member was not found.");
            }

            if (staffStatus.Value != StaffStatus.Active)
            {
                return new ValidationFailure(ServiceResultStatus.BadRequest, "Only active staff members can be assigned.");
            }
        }

        if (vendorId.HasValue)
        {
            var vendorStatus = await dbContext.ServiceVendors
                .AsNoTracking()
                .Where(vendor => vendor.Id == vendorId.Value)
                .Select(vendor => (VendorStatus?)vendor.Status)
                .FirstOrDefaultAsync(cancellationToken);
            if (!vendorStatus.HasValue)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Service vendor was not found.");
            }

            if (vendorStatus.Value != VendorStatus.Active)
            {
                return new ValidationFailure(ServiceResultStatus.BadRequest, "Only active vendors can be assigned.");
            }
        }

        if (propertyUnitId.HasValue)
        {
            var propertyUnitExists = await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(unit => unit.Id == propertyUnitId.Value, cancellationToken);
            if (!propertyUnitExists)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Property unit was not found.");
            }
        }

        return null;
    }

    private static ValidationFailure? ValidateSchedule(
        DateTime? scheduledAtUtc,
        DateTime? dueAtUtc,
        DateTime createdAtUtc)
    {
        if (scheduledAtUtc.HasValue
            && (scheduledAtUtc.Value < createdAtUtc.AddDays(-1)
                || scheduledAtUtc.Value > createdAtUtc.AddYears(2)))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Scheduled time is not reasonable.");
        }

        if (dueAtUtc.HasValue && dueAtUtc.Value <= createdAtUtc)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Due time must be after work order creation time.");
        }

        if (scheduledAtUtc.HasValue && dueAtUtc.HasValue && dueAtUtc.Value < scheduledAtUtc.Value)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Due time must be after scheduled time.");
        }

        return null;
    }

    private static ValidationFailure? ValidateCanChangeActiveWorkOrder(WorkOrder workOrder)
    {
        return workOrder.Status switch
        {
            WorkOrderStatus.Completed => new ValidationFailure(ServiceResultStatus.BadRequest, "Completed work orders cannot be changed."),
            WorkOrderStatus.Cancelled => new ValidationFailure(ServiceResultStatus.BadRequest, "Cancelled work orders cannot be changed."),
            _ => null
        };
    }

    private static WorkOrderStatus GetInitialWorkOrderStatus(
        Guid? staffMemberId,
        Guid? vendorId,
        DateTime? scheduledAtUtc)
    {
        if (scheduledAtUtc.HasValue)
        {
            return WorkOrderStatus.Scheduled;
        }

        return staffMemberId.HasValue || vendorId.HasValue
            ? WorkOrderStatus.Assigned
            : WorkOrderStatus.New;
    }

    private static void AddStatusHistory(
        WorkOrder workOrder,
        WorkOrderStatus oldStatus,
        WorkOrderStatus newStatus,
        Guid changedByUserId,
        string note)
    {
        workOrder.StatusHistory.Add(new WorkOrderStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            Note = Truncate(note, MaxNoteLength),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static WorkOrderResponse ToWorkOrderResponse(WorkOrder workOrder)
    {
        var ratingCount = workOrder.Ratings.Count;
        var averageRating = ratingCount == 0
            ? (decimal?)null
            : Math.Round((decimal)workOrder.Ratings.Average(rating => rating.Rating), 2, MidpointRounding.AwayFromZero);

        return new WorkOrderResponse(
            workOrder.Id,
            workOrder.Title,
            workOrder.Description,
            workOrder.SourceType,
            workOrder.SourceEntityId,
            workOrder.CompoundId,
            workOrder.Priority,
            workOrder.Status,
            workOrder.AssignedStaffMemberId,
            workOrder.AssignedStaffMember?.FullName,
            workOrder.AssignedVendorId,
            workOrder.AssignedVendor?.Name,
            workOrder.CreatedByUserId,
            workOrder.CreatedByUser?.FullName,
            workOrder.PropertyUnitId,
            workOrder.PropertyUnit?.UnitNumber,
            workOrder.ScheduledAtUtc,
            workOrder.StartedAtUtc,
            workOrder.CompletedAtUtc,
            workOrder.CancelledAtUtc,
            workOrder.DueAtUtc,
            workOrder.EstimatedCost,
            workOrder.ActualCost,
            workOrder.CompletionNotes,
            workOrder.CancellationReason,
            workOrder.CreatedAtUtc,
            workOrder.UpdatedAtUtc,
            IsOverdue(workOrder),
            averageRating,
            ratingCount,
            workOrder.CostItems
                .OrderBy(costItem => costItem.CreatedAtUtc)
                .Select(ToWorkOrderCostItemResponse)
                .ToArray());
    }

    private static WorkOrderCostItemResponse ToWorkOrderCostItemResponse(WorkOrderCostItem costItem)
    {
        return new WorkOrderCostItemResponse(
            costItem.Id,
            costItem.WorkOrderId,
            costItem.Description,
            costItem.CostType,
            costItem.Amount,
            costItem.CreatedAtUtc);
    }

    private static WorkOrderRatingResponse ToWorkOrderRatingResponse(
        WorkOrderRating rating,
        string? userName)
    {
        return new WorkOrderRatingResponse(
            rating.Id,
            rating.WorkOrderId,
            rating.UserId,
            userName,
            rating.Rating,
            rating.Comment,
            rating.CreatedAtUtc);
    }

    private static bool IsOverdue(WorkOrder workOrder)
    {
        return workOrder.DueAtUtc.HasValue
            && workOrder.DueAtUtc.Value < DateTime.UtcNow
            && workOrder.Status != WorkOrderStatus.Completed
            && workOrder.Status != WorkOrderStatus.Cancelled;
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
