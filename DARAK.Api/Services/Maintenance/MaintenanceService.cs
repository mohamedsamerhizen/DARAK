using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Maintenance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class MaintenanceService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ICompoundAccessService? compoundAccessService = null)
    : IMaintenanceService
{
    public async Task<PagedResult<MaintenanceRequestResponse>> SearchAdminAsync(
        MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var requests = await ApplyCurrentMaintenanceScopeAsync(
            ApplyFilters(GetMaintenanceDetailsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedMaintenanceResultAsync(requests, query, cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> GetAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var requests = await ApplyCurrentMaintenanceScopeAsync(
            GetMaintenanceDetailsQuery(asNoTracking: true),
            cancellationToken);

        var request = await requests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return request is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : ServiceResult<MaintenanceRequestResponse>.Success(ToMaintenanceRequestResponse(request));
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> AssignAsync(
        Guid id,
        Guid? changedByUserId,
        AssignMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AssignedToUserId == Guid.Empty)
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Assigned user id is required.");
        }

        if (request.CostEstimate < 0)
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Cost estimate cannot be negative.");
        }

        var technician = await userManager.FindByIdAsync(request.AssignedToUserId.ToString());
        if (technician is null || !await userManager.IsInRoleAsync(technician, nameof(UserRole.MaintenanceStaff)))
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Assigned user must be a maintenance staff member.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var requests = await ApplyCurrentMaintenanceScopeAsync(
            GetMaintenanceDetailsQuery(asNoTracking: false),
            cancellationToken);

        var maintenanceRequest = await requests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (maintenanceRequest is null)
        {
            return ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.");
        }

        if (maintenanceRequest.Status is not (MaintenanceStatus.Open or MaintenanceStatus.Assigned))
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Only open or assigned maintenance requests can be assigned.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = maintenanceRequest.Status;
        maintenanceRequest.AssignedToUserId = request.AssignedToUserId;
        maintenanceRequest.AssignedAt = now;
        maintenanceRequest.CostEstimate = request.CostEstimate;
        maintenanceRequest.UpdatedAt = now;

        if (oldStatus != MaintenanceStatus.Assigned)
        {
            ChangeStatus(
                maintenanceRequest,
                MaintenanceStatus.Assigned,
                changedByUserId,
                TrimOrNull(request.Notes),
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAdminAsync(maintenanceRequest.Id, cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> RejectAsync(
        Guid id,
        Guid? changedByUserId,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var requests = await ApplyCurrentMaintenanceScopeAsync(
            GetMaintenanceDetailsQuery(asNoTracking: false),
            cancellationToken);

        var maintenanceRequest = await requests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return maintenanceRequest is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : await ChangeStatusAsync(
                maintenanceRequest,
                MaintenanceStatus.Rejected,
                changedByUserId,
                request.Notes,
                cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> CloseAdminAsync(
        Guid id,
        Guid? changedByUserId,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var requests = await ApplyCurrentMaintenanceScopeAsync(
            GetMaintenanceDetailsQuery(asNoTracking: false),
            cancellationToken);

        var maintenanceRequest = await requests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return maintenanceRequest is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : await ChangeStatusAsync(
                maintenanceRequest,
                MaintenanceStatus.Closed,
                changedByUserId,
                request.Notes,
                cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<MaintenanceRequestResponse>>> SearchResidentAsync(
        Guid userId,
        MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return ServiceResult<PagedResult<MaintenanceRequestResponse>>.Success(
                new PagedResult<MaintenanceRequestResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var requests = ApplyFilters(GetMaintenanceDetailsQuery(asNoTracking: true), query)
            .Where(item => profileIds.Contains(item.ResidentProfileId));

        return ServiceResult<PagedResult<MaintenanceRequestResponse>>.Success(
            await ToPagedMaintenanceResultAsync(requests, query, cancellationToken));
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> GetResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var request = await GetVisibleResidentRequestAsync(userId, id, asNoTracking: true, cancellationToken);

        return request is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : ServiceResult<MaintenanceRequestResponse>.Success(ToMaintenanceRequestResponse(request));
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> CreateResidentAsync(
        Guid userId,
        CreateMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation is not null)
        {
            return ToResult<MaintenanceRequestResponse>(validation);
        }

        var occupancy = await GetActiveResidentOccupancyForUnitAsync(
            userId,
            request.PropertyUnitId,
            cancellationToken);
        if (occupancy is null)
        {
            return ServiceResult<MaintenanceRequestResponse>.NotFound("Active resident occupancy was not found for this unit.");
        }

        var maintenanceRequest = new MaintenanceRequest
        {
            ResidentProfileId = occupancy.ResidentProfileId,
            CompoundId = occupancy.CompoundId,
            PropertyUnitId = occupancy.PropertyUnitId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            StatusHistory =
            [
                new MaintenanceStatusHistory
                {
                    OldStatus = null,
                    NewStatus = MaintenanceStatus.Open,
                    ChangedByUserId = userId,
                    Notes = "Request created."
                }
            ]
        };

        dbContext.MaintenanceRequests.Add(maintenanceRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAdminAsync(maintenanceRequest.Id, cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> UpdateResidentAsync(
        Guid userId,
        Guid id,
        UpdateMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateUpdateRequest(request);
        if (validation is not null)
        {
            return ToResult<MaintenanceRequestResponse>(validation);
        }

        var maintenanceRequest = await GetVisibleResidentRequestAsync(userId, id, asNoTracking: false, cancellationToken);
        if (maintenanceRequest is null)
        {
            return ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.");
        }

        if (maintenanceRequest.Status is MaintenanceStatus.Closed or MaintenanceStatus.Rejected or MaintenanceStatus.Cancelled or MaintenanceStatus.Resolved)
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Maintenance request cannot be edited in its current status.");
        }

        maintenanceRequest.Title = request.Title.Trim();
        maintenanceRequest.Description = request.Description.Trim();
        maintenanceRequest.Priority = request.Priority;
        maintenanceRequest.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<MaintenanceRequestResponse>.Success(ToMaintenanceRequestResponse(maintenanceRequest));
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> CancelResidentAsync(
        Guid userId,
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var maintenanceRequest = await GetVisibleResidentRequestAsync(userId, id, asNoTracking: false, cancellationToken);

        return maintenanceRequest is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : await ChangeStatusAsync(
                maintenanceRequest,
                MaintenanceStatus.Cancelled,
                userId,
                request.Notes,
                cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> CloseResidentAsync(
        Guid userId,
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var maintenanceRequest = await GetVisibleResidentRequestAsync(userId, id, asNoTracking: false, cancellationToken);

        return maintenanceRequest is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : await ChangeStatusAsync(
                maintenanceRequest,
                MaintenanceStatus.Closed,
                userId,
                request.Notes,
                cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<MaintenanceRequestResponse>>> SearchAssignedToStaffAsync(
        Guid staffUserId,
        MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var requests = ApplyFilters(GetMaintenanceDetailsQuery(asNoTracking: true), query)
            .Where(item => item.AssignedToUserId == staffUserId);

        return ServiceResult<PagedResult<MaintenanceRequestResponse>>.Success(
            await ToPagedMaintenanceResultAsync(requests, query, cancellationToken));
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> GetAssignedToStaffAsync(
        Guid staffUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var request = await GetMaintenanceDetailsQuery(asNoTracking: true)
            .Where(item => item.Id == id && item.AssignedToUserId == staffUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return request is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : ServiceResult<MaintenanceRequestResponse>.Success(ToMaintenanceRequestResponse(request));
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> StartAsync(
        Guid staffUserId,
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var maintenanceRequest = await GetAssignedRequestForStaffAsync(staffUserId, id, cancellationToken);

        return maintenanceRequest is null
            ? ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.")
            : await ChangeStatusAsync(
                maintenanceRequest,
                MaintenanceStatus.InProgress,
                staffUserId,
                request.Notes,
                cancellationToken);
    }

    public async Task<ServiceResult<MaintenanceRequestResponse>> ResolveAsync(
        Guid staffUserId,
        Guid id,
        ResolveMaintenanceRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TrimOrNull(request.ResolutionNotes) is null)
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Resolution notes are required.");
        }

        if (request.ActualCost < 0)
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest("Actual cost cannot be negative.");
        }

        var maintenanceRequest = await GetAssignedRequestForStaffAsync(staffUserId, id, cancellationToken);
        if (maintenanceRequest is null)
        {
            return ServiceResult<MaintenanceRequestResponse>.NotFound("Maintenance request was not found.");
        }

        maintenanceRequest.ResolutionNotes = request.ResolutionNotes.Trim();
        maintenanceRequest.ActualCost = request.ActualCost;

        return await ChangeStatusAsync(
            maintenanceRequest,
            MaintenanceStatus.Resolved,
            staffUserId,
            request.ResolutionNotes,
            cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<MaintenanceStatusHistoryResponse>>> GetHistoryAsync(
        Guid maintenanceRequestId,
        MaintenanceStatusHistorySearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var requests = await ApplyCurrentMaintenanceScopeAsync(
            dbContext.MaintenanceRequests.AsNoTracking(),
            cancellationToken);

        var exists = await requests
            .AnyAsync(request => request.Id == maintenanceRequestId, cancellationToken);
        if (!exists)
        {
            return ServiceResult<PagedResult<MaintenanceStatusHistoryResponse>>.NotFound("Maintenance request was not found.");
        }

        var history = dbContext.MaintenanceStatusHistories
            .AsNoTracking()
            .Include(item => item.ChangedByUser)
            .Where(item => item.MaintenanceRequestId == maintenanceRequestId)
            .OrderByDescending(item => item.CreatedAt);

        var totalCount = await history.CountAsync(cancellationToken);
        var items = await history
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new MaintenanceStatusHistoryResponse(
                item.Id,
                item.MaintenanceRequestId,
                item.OldStatus,
                item.NewStatus,
                item.ChangedByUserId,
                item.ChangedByUser == null ? null : item.ChangedByUser.FullName,
                item.Notes,
                item.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<MaintenanceStatusHistoryResponse>>.Success(
            new PagedResult<MaintenanceStatusHistoryResponse>(
                items,
                query.PageNumber,
                query.PageSize,
                totalCount));
    }

    private IQueryable<MaintenanceRequest> GetMaintenanceDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.MaintenanceRequests
            .Include(request => request.ResidentProfile)
            .Include(request => request.Compound)
            .Include(request => request.PropertyUnit)
            .Include(request => request.AssignedToUser)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private static IQueryable<MaintenanceRequest> ApplyFilters(
        IQueryable<MaintenanceRequest> requests,
        MaintenanceRequestSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            requests = requests.Where(request => request.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            requests = requests.Where(request => request.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            requests = requests.Where(request => request.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.AssignedToUserId.HasValue)
        {
            requests = requests.Where(request => request.AssignedToUserId == query.AssignedToUserId.Value);
        }

        if (query.Status.HasValue)
        {
            requests = requests.Where(request => request.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            requests = requests.Where(request => request.Priority == query.Priority.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            requests = requests.Where(request =>
                request.Title.Contains(searchTerm)
                || request.Description.Contains(searchTerm)
                || request.PropertyUnit.UnitNumber.Contains(searchTerm)
                || request.ResidentProfile.FullName.Contains(searchTerm));
        }

        return requests;
    }

    private async Task<PagedResult<MaintenanceRequestResponse>> ToPagedMaintenanceResultAsync(
        IQueryable<MaintenanceRequest> query,
        MaintenanceRequestSearchQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Title)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(ToMaintenanceProjection())
            .ToArrayAsync(cancellationToken);

        return new PagedResult<MaintenanceRequestResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<ServiceResult<MaintenanceRequestResponse>> ChangeStatusAsync(
        MaintenanceRequest maintenanceRequest,
        MaintenanceStatus newStatus,
        Guid? changedByUserId,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (!CanTransition(maintenanceRequest.Status, newStatus))
        {
            return ServiceResult<MaintenanceRequestResponse>.BadRequest(
                $"Cannot change maintenance request from {maintenanceRequest.Status} to {newStatus}.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        ChangeStatus(maintenanceRequest, newStatus, changedByUserId, TrimOrNull(notes), now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAdminAsync(maintenanceRequest.Id, cancellationToken);
    }

    private static void ChangeStatus(
        MaintenanceRequest maintenanceRequest,
        MaintenanceStatus newStatus,
        Guid? changedByUserId,
        string? notes,
        DateTime now)
    {
        var oldStatus = maintenanceRequest.Status;
        maintenanceRequest.Status = newStatus;
        maintenanceRequest.UpdatedAt = now;

        switch (newStatus)
        {
            case MaintenanceStatus.Assigned:
                maintenanceRequest.AssignedAt ??= now;
                break;
            case MaintenanceStatus.InProgress:
                maintenanceRequest.StartedAt ??= now;
                break;
            case MaintenanceStatus.Resolved:
                maintenanceRequest.ResolvedAt ??= now;
                break;
            case MaintenanceStatus.Closed:
                maintenanceRequest.ClosedAt ??= now;
                break;
            case MaintenanceStatus.Cancelled:
                maintenanceRequest.CancelledAt ??= now;
                break;
        }

        maintenanceRequest.StatusHistory.Add(new MaintenanceStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            Notes = notes
        });
    }

    private static bool CanTransition(MaintenanceStatus oldStatus, MaintenanceStatus newStatus)
    {
        return oldStatus switch
        {
            MaintenanceStatus.Open => newStatus is MaintenanceStatus.Assigned
                or MaintenanceStatus.Rejected
                or MaintenanceStatus.Cancelled,
            MaintenanceStatus.Assigned => newStatus is MaintenanceStatus.InProgress
                or MaintenanceStatus.Rejected
                or MaintenanceStatus.Cancelled,
            MaintenanceStatus.InProgress => newStatus is MaintenanceStatus.Resolved
                or MaintenanceStatus.Cancelled,
            MaintenanceStatus.Resolved => newStatus == MaintenanceStatus.Closed,
            _ => false
        };
    }

    private async Task<MaintenanceRequest?> GetVisibleResidentRequestAsync(
        Guid userId,
        Guid requestId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        return await GetMaintenanceDetailsQuery(asNoTracking)
            .Where(request => request.Id == requestId && request.ResidentProfile.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<MaintenanceRequest?> GetAssignedRequestForStaffAsync(
        Guid staffUserId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await GetMaintenanceDetailsQuery(asNoTracking: false)
            .Where(request => request.Id == requestId && request.AssignedToUserId == staffUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<OccupancyRecord?> GetActiveResidentOccupancyForUnitAsync(
        Guid userId,
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Include(record => record.ResidentProfile)
            .Where(record => record.PropertyUnitId == propertyUnitId
                && record.ResidentProfile.UserId == userId
                && record.ResidentProfile.IsActive
                && record.OccupancyStatus == OccupancyStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid[]> GetResidentProfileIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => profile.Id)
            .ToArrayAsync(cancellationToken);
    }

    private static ValidationFailure? ValidateCreateRequest(CreateMaintenanceRequestRequest request)
    {
        if (request.PropertyUnitId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit id is required.");
        }

        if (TrimOrNull(request.Title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Maintenance request title is required.");
        }

        if (TrimOrNull(request.Description) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Maintenance request description is required.");
        }

        return null;
    }

    private static ValidationFailure? ValidateUpdateRequest(UpdateMaintenanceRequestRequest request)
    {
        if (TrimOrNull(request.Title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Maintenance request title is required.");
        }

        if (TrimOrNull(request.Description) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Maintenance request description is required.");
        }

        return null;
    }

    private static MaintenanceRequestResponse ToMaintenanceRequestResponse(MaintenanceRequest request)
    {
        return new MaintenanceRequestResponse(
            request.Id,
            request.ResidentProfileId,
            request.ResidentProfile.FullName,
            request.CompoundId,
            request.Compound.Name,
            request.PropertyUnitId,
            request.PropertyUnit.UnitNumber,
            request.AssignedToUserId,
            request.AssignedToUser?.FullName,
            request.Title,
            request.Description,
            request.Priority,
            request.Status,
            request.CostEstimate,
            request.ActualCost,
            request.ResolutionNotes,
            request.CreatedAt,
            request.UpdatedAt,
            request.AssignedAt,
            request.StartedAt,
            request.ResolvedAt,
            request.ClosedAt,
            request.CancelledAt);
    }

    private static Expression<Func<MaintenanceRequest, MaintenanceRequestResponse>> ToMaintenanceProjection()
    {
        return request => new MaintenanceRequestResponse(
            request.Id,
            request.ResidentProfileId,
            request.ResidentProfile.FullName,
            request.CompoundId,
            request.Compound.Name,
            request.PropertyUnitId,
            request.PropertyUnit.UnitNumber,
            request.AssignedToUserId,
            request.AssignedToUser == null ? null : request.AssignedToUser.FullName,
            request.Title,
            request.Description,
            request.Priority,
            request.Status,
            request.CostEstimate,
            request.ActualCost,
            request.ResolutionNotes,
            request.CreatedAt,
            request.UpdatedAt,
            request.AssignedAt,
            request.StartedAt,
            request.ResolvedAt,
            request.ClosedAt,
            request.CancelledAt);
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

    private async Task<IQueryable<MaintenanceRequest>> ApplyCurrentMaintenanceScopeAsync(
        IQueryable<MaintenanceRequest> requests,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return requests;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return requests.ApplyCompoundAccess(scope, request => request.CompoundId);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
