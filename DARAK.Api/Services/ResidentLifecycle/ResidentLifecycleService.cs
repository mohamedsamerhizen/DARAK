using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.ResidentLifecycle;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentLifecycleService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IResidentLifecycleService
{
    private const string MoveOutFinalReadingMarkerPrefix = "MOVE_OUT_FINAL_READING";

    public async Task<ServiceResult<ResidentLifecycleProcessResponse>> CreateProcessAsync(
        Guid? currentUserId,
        CreateResidentLifecycleProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Forbidden("Current user is required.");
        }

        if (!Enum.IsDefined(request.ProcessType))
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("Lifecycle process type is invalid.");
        }

        var validation = await ValidateUnitAndResidentAsync(request.PropertyUnitId, request.ResidentProfileId, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.NotFound(validation.Error!);
        }

        if (!await CanAccessCompoundAsync(validation.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Forbidden("Current user cannot access this compound.");
        }

        var activeProcessExists = await dbContext.ResidentLifecycleProcesses.AnyAsync(item =>
            item.PropertyUnitId == request.PropertyUnitId
            && item.ResidentProfileId == request.ResidentProfileId
            && item.ProcessType == request.ProcessType
            && item.Status != ResidentLifecycleStatus.Completed
            && item.Status != ResidentLifecycleStatus.Cancelled,
            cancellationToken);
        if (activeProcessExists)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Conflict("An active lifecycle process already exists for this resident and unit.");
        }

        if (request.ProcessType == ResidentLifecycleProcessType.MoveIn)
        {
            var activeOccupancyExists = await dbContext.OccupancyRecords.AnyAsync(item =>
                item.PropertyUnitId == request.PropertyUnitId
                && item.OccupancyStatus == OccupancyStatus.Active,
                cancellationToken);
            if (activeOccupancyExists)
            {
                return ServiceResult<ResidentLifecycleProcessResponse>.Conflict("The unit already has an active occupancy record.");
            }
        }
        else if (request.ProcessType == ResidentLifecycleProcessType.MoveOut)
        {
            var activeOccupancyExists = await dbContext.OccupancyRecords.AnyAsync(item =>
                item.PropertyUnitId == request.PropertyUnitId
                && item.ResidentProfileId == request.ResidentProfileId
                && item.OccupancyStatus == OccupancyStatus.Active,
                cancellationToken);
            if (!activeOccupancyExists)
            {
                return ServiceResult<ResidentLifecycleProcessResponse>.Conflict("Move-out requires an active occupancy record for this resident and unit.");
            }
        }

        var process = new ResidentLifecycleProcess
        {
            CompoundId = validation.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            ResidentProfileId = request.ResidentProfileId,
            ProcessType = request.ProcessType,
            TargetDate = request.TargetDate,
            FinancialClearanceRequired = request.ProcessType == ResidentLifecycleProcessType.MoveOut && request.FinancialClearanceRequired,
            Status = request.ProcessType == ResidentLifecycleProcessType.MoveOut && request.FinancialClearanceRequired
                ? ResidentLifecycleStatus.PendingFinancialClearance
                : ResidentLifecycleStatus.InProgress,
            Notes = TrimOptional(request.Notes),
            CreatedByUserId = currentUserId.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ResidentLifecycleProcesses.Add(process);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentLifecycleProcessResponse>.Success(ToProcessResponse(process));
    }

    public async Task<ServiceResult<ResidentLifecycleProcessResponse>> GetProcessAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await ApplyCurrentCompoundAccessAsync(dbContext.ResidentLifecycleProcesses.AsNoTracking(), cancellationToken);
        var process = await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return process is null
            ? ServiceResult<ResidentLifecycleProcessResponse>.NotFound("Lifecycle process was not found.")
            : ServiceResult<ResidentLifecycleProcessResponse>.Success(ToProcessResponse(process));
    }

    public async Task<PagedResult<ResidentLifecycleProcessResponse>> SearchProcessesAsync(
        ResidentLifecycleQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var processes = await ApplyCurrentCompoundAccessAsync(dbContext.ResidentLifecycleProcesses.AsNoTracking(), cancellationToken);
        if (query.CompoundId.HasValue)
        {
            processes = processes.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            processes = processes.Where(item => item.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            processes = processes.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.ProcessType.HasValue)
        {
            processes = processes.Where(item => item.ProcessType == query.ProcessType.Value);
        }

        if (query.Status.HasValue)
        {
            processes = processes.Where(item => item.Status == query.Status.Value);
        }

        processes = processes.OrderByDescending(item => item.CreatedAtUtc);
        return await ToPagedResultAsync(processes, query, ToProcessResponse, cancellationToken);
    }

    public async Task<ServiceResult<ResidentLifecycleProcessResponse>> ConfirmFinancialClearanceAsync(
        Guid id,
        Guid? currentUserId,
        ConfirmLifecycleFinancialClearanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Forbidden("Current user is required.");
        }

        var process = await dbContext.ResidentLifecycleProcesses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.NotFound("Lifecycle process was not found.");
        }

        if (process.Status is ResidentLifecycleStatus.Completed or ResidentLifecycleStatus.Cancelled)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Conflict("Completed or cancelled lifecycle processes cannot be updated.");
        }

        process.FinancialClearanceConfirmed = true;
        process.FinancialClearanceConfirmedAtUtc = DateTime.UtcNow;
        process.FinancialClearanceConfirmedByUserId = currentUserId.Value;
        process.FinancialClearanceNotes = TrimOptional(request.Notes);
        process.UpdatedAtUtc = DateTime.UtcNow;
        if (process.Status == ResidentLifecycleStatus.PendingFinancialClearance)
        {
            process.Status = ResidentLifecycleStatus.InProgress;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentLifecycleProcessResponse>.Success(ToProcessResponse(process));
    }

    public async Task<ServiceResult<ResidentLifecycleProcessResponse>> CompleteProcessAsync(
        Guid id,
        Guid? currentUserId,
        CompleteResidentLifecycleProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Forbidden("Current user is required.");
        }

        var process = await dbContext.ResidentLifecycleProcesses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.NotFound("Lifecycle process was not found.");
        }

        if (process.Status == ResidentLifecycleStatus.Completed)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Conflict("Lifecycle process is already completed.");
        }

        if (process.Status == ResidentLifecycleStatus.Cancelled)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.Conflict("Cancelled lifecycle processes cannot be completed.");
        }

        var unit = await dbContext.PropertyUnits.FirstOrDefaultAsync(item => item.Id == process.PropertyUnitId, cancellationToken);
        var resident = await dbContext.ResidentProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == process.ResidentProfileId, cancellationToken);
        if (unit is null || resident is null || unit.CompoundId != resident.CompoundId)
        {
            return ServiceResult<ResidentLifecycleProcessResponse>.NotFound("Unit or resident was not found.");
        }

        if (process.ProcessType == ResidentLifecycleProcessType.MoveOut)
        {
            if (process.FinancialClearanceRequired && !process.FinancialClearanceConfirmed)
            {
                process.Status = ResidentLifecycleStatus.PendingFinancialClearance;
                await dbContext.SaveChangesAsync(cancellationToken);
                return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("Financial clearance must be confirmed before move-out completion.");
            }

            var activeRentContractCount = await dbContext.RentContracts.AsNoTracking().CountAsync(item =>
                item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.ResidentProfileId == process.ResidentProfileId
                && item.ContractStatus == RentContractStatus.Active,
                cancellationToken);
            if (activeRentContractCount > 0)
            {
                return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("Active rent contracts must be terminated or expired before move-out completion.");
            }

            var activeSaleContractCount = await dbContext.PropertySaleContracts.AsNoTracking().CountAsync(item =>
                item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.ResidentProfileId == process.ResidentProfileId
                && item.ContractStatus == SaleContractStatus.Active,
                cancellationToken);
            if (activeSaleContractCount > 0)
            {
                return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("Active sale contracts must be completed or cancelled before move-out completion.");
            }

            var activeMeterIds = await dbContext.Meters.AsNoTracking()
                .Where(item => item.CompoundId == process.CompoundId
                    && item.PropertyUnitId == process.PropertyUnitId
                    && item.IsActive)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            if (activeMeterIds.Count > 0)
            {
                var finalMarker = BuildMoveOutFinalReadingMarker(process.Id);
                var finalMeterReadingIds = await dbContext.MeterReadings.AsNoTracking()
                    .Where(item => activeMeterIds.Contains(item.MeterId)
                        && item.Notes != null
                        && item.Notes.Contains(finalMarker))
                    .Select(item => item.MeterId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                if (finalMeterReadingIds.Count < activeMeterIds.Count)
                {
                    return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("All active meters must have final move-out readings before move-out completion.");
                }

                var unbilledFinalReadingExists = await dbContext.MeterReadings.AsNoTracking().AnyAsync(item =>
                    activeMeterIds.Contains(item.MeterId)
                    && item.Notes != null
                    && item.Notes.Contains(finalMarker)
                    && !item.IsBilled,
                    cancellationToken);
                if (unbilledFinalReadingExists)
                {
                    process.Status = ResidentLifecycleStatus.PendingFinancialClearance;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("Final move-out meter readings must be billed before move-out completion.");
                }
            }

            var openDamageLiabilityExists = await dbContext.UnitDamageLiabilities.AnyAsync(item =>
                item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.ResidentProfileId == process.ResidentProfileId
                && (item.Status == DamageLiabilityStatus.Draft
                    || item.Status == DamageLiabilityStatus.Charged
                    || item.Status == DamageLiabilityStatus.Disputed),
                cancellationToken);
            if (openDamageLiabilityExists)
            {
                process.Status = ResidentLifecycleStatus.PendingCustodyClearance;
                await dbContext.SaveChangesAsync(cancellationToken);
                return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("All open damage liabilities must be resolved or cancelled before move-out completion.");
            }

            var unsettledCustodyExists = await dbContext.ResidentCustodyItems.AnyAsync(item =>
                item.PropertyUnitId == process.PropertyUnitId
                && item.ResidentProfileId == process.ResidentProfileId
                && item.Status != CustodyItemStatus.Returned,
                cancellationToken);
            if (unsettledCustodyExists)
            {
                process.Status = ResidentLifecycleStatus.PendingCustodyClearance;
                await dbContext.SaveChangesAsync(cancellationToken);
                return ServiceResult<ResidentLifecycleProcessResponse>.BadRequest("All custody items must be returned or cleared before move-out completion.");
            }

            var activeOccupancies = await dbContext.OccupancyRecords
                .Where(item => item.PropertyUnitId == process.PropertyUnitId
                    && item.ResidentProfileId == process.ResidentProfileId
                    && item.OccupancyStatus == OccupancyStatus.Active)
                .ToListAsync(cancellationToken);
            foreach (var occupancy in activeOccupancies)
            {
                occupancy.OccupancyStatus = OccupancyStatus.Ended;
                occupancy.EndDate ??= process.TargetDate;
                occupancy.EndedAt = DateTime.UtcNow;
                occupancy.UpdatedAt = DateTime.UtcNow;
            }

            unit.UnitStatus = UnitStatus.Available;
        }
        else
        {
            var activeOccupancyExists = await dbContext.OccupancyRecords.AnyAsync(item =>
                item.PropertyUnitId == process.PropertyUnitId
                && item.OccupancyStatus == OccupancyStatus.Active,
                cancellationToken);
            if (!activeOccupancyExists)
            {
                dbContext.OccupancyRecords.Add(new OccupancyRecord
                {
                    CompoundId = process.CompoundId,
                    PropertyUnitId = process.PropertyUnitId,
                    ResidentProfileId = process.ResidentProfileId,
                    OccupancyType = OccupancyType.Tenant,
                    OccupancyStatus = OccupancyStatus.Active,
                    StartDate = process.TargetDate,
                    Notes = "Created by resident lifecycle move-in completion."
                });
            }

            unit.UnitStatus = UnitStatus.Occupied;
        }

        process.Status = ResidentLifecycleStatus.Completed;
        process.CompletedAtUtc = DateTime.UtcNow;
        process.CompletedByUserId = currentUserId.Value;
        process.Notes = TrimOptional(request.Notes) ?? process.Notes;
        process.UpdatedAtUtc = DateTime.UtcNow;
        unit.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentLifecycleProcessResponse>.Success(ToProcessResponse(process));
    }

    public async Task<ServiceResult<ResidentCustodyItemResponse>> IssueCustodyItemAsync(
        Guid? currentUserId,
        IssueCustodyItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentCustodyItemResponse>.Forbidden("Current user is required.");
        }

        var identifier = TrimOrNull(request.Identifier);
        if (identifier is null)
        {
            return ServiceResult<ResidentCustodyItemResponse>.BadRequest("Custody item identifier is required.");
        }

        if (!Enum.IsDefined(request.ItemType))
        {
            return ServiceResult<ResidentCustodyItemResponse>.BadRequest("Custody item type is invalid.");
        }

        var validation = await ValidateUnitAndResidentAsync(request.PropertyUnitId, request.ResidentProfileId, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<ResidentCustodyItemResponse>.NotFound(validation.Error!);
        }

        if (!await CanAccessCompoundAsync(validation.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentCustodyItemResponse>.Forbidden("Current user cannot access this compound.");
        }

        var duplicateIssued = await dbContext.ResidentCustodyItems.AnyAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.Identifier == identifier
            && item.Status == CustodyItemStatus.Issued,
            cancellationToken);
        if (duplicateIssued)
        {
            return ServiceResult<ResidentCustodyItemResponse>.Conflict("An issued custody item with the same identifier already exists.");
        }

        var custodyItem = new ResidentCustodyItem
        {
            CompoundId = validation.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            ResidentProfileId = request.ResidentProfileId,
            ItemType = request.ItemType,
            Identifier = identifier,
            Description = TrimOptional(request.Description),
            ReplacementFeeAmount = request.ReplacementFeeAmount,
            IssuedAtUtc = request.IssuedAtUtc ?? DateTime.UtcNow,
            IssuedByUserId = currentUserId.Value,
            Notes = TrimOptional(request.Notes)
        };

        dbContext.ResidentCustodyItems.Add(custodyItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentCustodyItemResponse>.Success(ToCustodyResponse(custodyItem));
    }

    public async Task<ServiceResult<ResidentCustodyItemResponse>> ReturnCustodyItemAsync(
        Guid id,
        Guid? currentUserId,
        ReturnCustodyItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentCustodyItemResponse>.Forbidden("Current user is required.");
        }

        var custodyItem = await dbContext.ResidentCustodyItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (custodyItem is null || !await CanAccessCompoundAsync(custodyItem.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentCustodyItemResponse>.NotFound("Custody item was not found.");
        }

        if (custodyItem.Status == CustodyItemStatus.Returned)
        {
            return ServiceResult<ResidentCustodyItemResponse>.Conflict("Custody item is already returned.");
        }

        custodyItem.Status = CustodyItemStatus.Returned;
        custodyItem.ReturnedAtUtc = request.ReturnedAtUtc ?? DateTime.UtcNow;
        custodyItem.ReturnedByUserId = currentUserId.Value;
        custodyItem.Notes = TrimOptional(request.Notes) ?? custodyItem.Notes;
        custodyItem.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentCustodyItemResponse>.Success(ToCustodyResponse(custodyItem));
    }

    public async Task<PagedResult<ResidentCustodyItemResponse>> SearchCustodyItemsAsync(
        CustodyItemQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var items = await ApplyCurrentCompoundAccessAsync(dbContext.ResidentCustodyItems.AsNoTracking(), cancellationToken);
        if (query.CompoundId.HasValue)
        {
            items = items.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            items = items.Where(item => item.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            items = items.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.Status.HasValue)
        {
            items = items.Where(item => item.Status == query.Status.Value);
        }

        if (query.ItemType.HasValue)
        {
            items = items.Where(item => item.ItemType == query.ItemType.Value);
        }

        items = items.OrderByDescending(item => item.CreatedAtUtc);
        return await ToPagedResultAsync(items, query, ToCustodyResponse, cancellationToken);
    }

    public async Task<ServiceResult<MoveLogisticsPermitResponse>> CreateMovePermitAsync(
        Guid? currentUserId,
        CreateMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.Forbidden("Current user is required.");
        }

        if (!Enum.IsDefined(request.MoveType))
        {
            return ServiceResult<MoveLogisticsPermitResponse>.BadRequest("Move type is invalid.");
        }

        if (request.ScheduledEndAtUtc <= request.ScheduledStartAtUtc)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.BadRequest("Scheduled end must be after scheduled start.");
        }

        var validation = await ValidateUnitAndResidentAsync(request.PropertyUnitId, request.ResidentProfileId, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.NotFound(validation.Error!);
        }

        if (!await CanAccessCompoundAsync(validation.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveLogisticsPermitResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (request.ResidentLifecycleProcessId.HasValue)
        {
            var linkedProcessValid = await dbContext.ResidentLifecycleProcesses.AnyAsync(item =>
                item.Id == request.ResidentLifecycleProcessId.Value
                && item.CompoundId == validation.CompoundId
                && item.PropertyUnitId == request.PropertyUnitId
                && item.ResidentProfileId == request.ResidentProfileId,
                cancellationToken);
            if (!linkedProcessValid)
            {
                return ServiceResult<MoveLogisticsPermitResponse>.BadRequest("Linked lifecycle process is invalid.");
            }
        }

        var permit = new MoveLogisticsPermit
        {
            CompoundId = validation.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            ResidentProfileId = request.ResidentProfileId,
            ResidentLifecycleProcessId = request.ResidentLifecycleProcessId,
            MoveType = request.MoveType,
            ScheduledStartAtUtc = request.ScheduledStartAtUtc,
            ScheduledEndAtUtc = request.ScheduledEndAtUtc,
            TruckInfo = TrimOptional(request.TruckInfo),
            WorkersCount = request.WorkersCount,
            Notes = TrimOptional(request.Notes),
            CreatedByUserId = currentUserId.Value
        };

        dbContext.MoveLogisticsPermits.Add(permit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<MoveLogisticsPermitResponse>.Success(ToMovePermitResponse(permit));
    }

    public async Task<ServiceResult<MoveLogisticsPermitResponse>> DecideMovePermitAsync(
        Guid id,
        Guid? currentUserId,
        DecideMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.Forbidden("Current user is required.");
        }

        var permit = await dbContext.MoveLogisticsPermits.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (permit is null || !await CanAccessCompoundAsync(permit.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveLogisticsPermitResponse>.NotFound("Move logistics permit was not found.");
        }

        if (permit.Status is MoveLogisticsPermitStatus.Completed or MoveLogisticsPermitStatus.Cancelled)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.Conflict("Completed or cancelled move permits cannot be decided.");
        }

        permit.Status = request.Approved ? MoveLogisticsPermitStatus.Approved : MoveLogisticsPermitStatus.Rejected;
        permit.DecisionReason = TrimOptional(request.Reason);
        permit.ApprovedByUserId = currentUserId.Value;
        permit.ApprovedAtUtc = DateTime.UtcNow;
        permit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<MoveLogisticsPermitResponse>.Success(ToMovePermitResponse(permit));
    }

    public async Task<ServiceResult<MoveLogisticsPermitResponse>> CompleteMovePermitAsync(
        Guid id,
        Guid? currentUserId,
        CompleteMoveLogisticsPermitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.Forbidden("Current user is required.");
        }

        var permit = await dbContext.MoveLogisticsPermits.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (permit is null || !await CanAccessCompoundAsync(permit.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveLogisticsPermitResponse>.NotFound("Move logistics permit was not found.");
        }

        if (permit.Status != MoveLogisticsPermitStatus.Approved)
        {
            return ServiceResult<MoveLogisticsPermitResponse>.Conflict("Only approved move permits can be completed.");
        }

        permit.Status = MoveLogisticsPermitStatus.Completed;
        permit.CompletedByUserId = currentUserId.Value;
        permit.CompletedAtUtc = DateTime.UtcNow;
        permit.CompletionNotes = TrimOptional(request.CompletionNotes);
        permit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<MoveLogisticsPermitResponse>.Success(ToMovePermitResponse(permit));
    }

    public async Task<PagedResult<MoveLogisticsPermitResponse>> SearchMovePermitsAsync(
        MoveLogisticsPermitQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var permits = await ApplyCurrentCompoundAccessAsync(dbContext.MoveLogisticsPermits.AsNoTracking(), cancellationToken);
        if (query.CompoundId.HasValue)
        {
            permits = permits.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            permits = permits.Where(item => item.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            permits = permits.Where(item => item.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.MoveType.HasValue)
        {
            permits = permits.Where(item => item.MoveType == query.MoveType.Value);
        }

        if (query.Status.HasValue)
        {
            permits = permits.Where(item => item.Status == query.Status.Value);
        }

        permits = permits.OrderByDescending(item => item.CreatedAtUtc);
        return await ToPagedResultAsync(permits, query, ToMovePermitResponse, cancellationToken);
    }

    public async Task<ServiceResult<UnitReadinessRecordResponse>> CreateUnitReadinessRecordAsync(
        Guid? currentUserId,
        CreateUnitReadinessRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitReadinessRecordResponse>.Forbidden("Current user is required.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return ServiceResult<UnitReadinessRecordResponse>.BadRequest("Unit readiness status is invalid.");
        }

        var unit = await dbContext.PropertyUnits.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.PropertyUnitId, cancellationToken);
        if (unit is null)
        {
            return ServiceResult<UnitReadinessRecordResponse>.NotFound("Property unit was not found.");
        }

        if (!await CanAccessCompoundAsync(unit.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitReadinessRecordResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (request.ResidentLifecycleProcessId.HasValue)
        {
            var processValid = await dbContext.ResidentLifecycleProcesses.AnyAsync(item =>
                item.Id == request.ResidentLifecycleProcessId.Value
                && item.PropertyUnitId == unit.Id
                && item.CompoundId == unit.CompoundId,
                cancellationToken);
            if (!processValid)
            {
                return ServiceResult<UnitReadinessRecordResponse>.BadRequest("Linked lifecycle process is invalid.");
            }
        }

        if (request.OperationalChecklistRunId.HasValue)
        {
            var checklistValid = await dbContext.OperationalChecklistRuns.AnyAsync(item =>
                item.Id == request.OperationalChecklistRunId.Value
                && item.CompoundId == unit.CompoundId,
                cancellationToken);
            if (!checklistValid)
            {
                return ServiceResult<UnitReadinessRecordResponse>.BadRequest("Linked checklist run is invalid.");
            }
        }

        var readiness = new UnitReadinessRecord
        {
            CompoundId = unit.CompoundId,
            PropertyUnitId = unit.Id,
            ResidentLifecycleProcessId = request.ResidentLifecycleProcessId,
            Status = request.Status,
            OperationalChecklistRunId = request.OperationalChecklistRunId,
            Notes = TrimOptional(request.Notes),
            CreatedByUserId = currentUserId.Value
        };

        dbContext.UnitReadinessRecords.Add(readiness);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UnitReadinessRecordResponse>.Success(ToReadinessResponse(readiness));
    }

    public async Task<ServiceResult<UnitReadinessRecordResponse>> UpdateUnitReadinessStatusAsync(
        Guid id,
        Guid? currentUserId,
        UpdateUnitReadinessStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitReadinessRecordResponse>.Forbidden("Current user is required.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return ServiceResult<UnitReadinessRecordResponse>.BadRequest("Unit readiness status is invalid.");
        }

        var readiness = await dbContext.UnitReadinessRecords.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (readiness is null || !await CanAccessCompoundAsync(readiness.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitReadinessRecordResponse>.NotFound("Unit readiness record was not found.");
        }

        readiness.Status = request.Status;
        readiness.Notes = TrimOptional(request.Notes) ?? readiness.Notes;
        readiness.UpdatedAtUtc = DateTime.UtcNow;

        var unit = await dbContext.PropertyUnits.FirstOrDefaultAsync(item => item.Id == readiness.PropertyUnitId, cancellationToken);
        if (unit is not null)
        {
            unit.UnitStatus = request.Status == UnitReadinessStatus.ReadyForMoveIn
                ? UnitStatus.Available
                : UnitStatus.UnderMaintenance;
            unit.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UnitReadinessRecordResponse>.Success(ToReadinessResponse(readiness));
    }

    public async Task<PagedResult<UnitReadinessRecordResponse>> SearchUnitReadinessRecordsAsync(
        UnitReadinessQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var readiness = await ApplyCurrentCompoundAccessAsync(dbContext.UnitReadinessRecords.AsNoTracking(), cancellationToken);
        if (query.CompoundId.HasValue)
        {
            readiness = readiness.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            readiness = readiness.Where(item => item.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.Status.HasValue)
        {
            readiness = readiness.Where(item => item.Status == query.Status.Value);
        }

        readiness = readiness.OrderByDescending(item => item.CreatedAtUtc);
        return await ToPagedResultAsync(readiness, query, ToReadinessResponse, cancellationToken);
    }

    public async Task<ServiceResult<UnitDamageLiabilityResponse>> CreateDamageLiabilityAsync(
        Guid? currentUserId,
        CreateUnitDamageLiabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitDamageLiabilityResponse>.Forbidden("Current user is required.");
        }

        var description = TrimOrNull(request.Description);
        if (description is null)
        {
            return ServiceResult<UnitDamageLiabilityResponse>.BadRequest("Damage liability description is required.");
        }

        var validation = await ValidateUnitAndResidentAsync(request.PropertyUnitId, request.ResidentProfileId, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<UnitDamageLiabilityResponse>.NotFound(validation.Error!);
        }

        if (!await CanAccessCompoundAsync(validation.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitDamageLiabilityResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (request.ResidentLifecycleProcessId.HasValue)
        {
            var processValid = await dbContext.ResidentLifecycleProcesses.AnyAsync(item =>
                item.Id == request.ResidentLifecycleProcessId.Value
                && item.CompoundId == validation.CompoundId
                && item.PropertyUnitId == request.PropertyUnitId
                && item.ResidentProfileId == request.ResidentProfileId,
                cancellationToken);
            if (!processValid)
            {
                return ServiceResult<UnitDamageLiabilityResponse>.BadRequest("Linked lifecycle process is invalid.");
            }
        }

        if (request.FinancialAdjustmentId.HasValue)
        {
            var adjustmentValid = await dbContext.FinancialAdjustments.AnyAsync(item =>
                item.Id == request.FinancialAdjustmentId.Value
                && item.CompoundId == validation.CompoundId
                && item.ResidentProfileId == request.ResidentProfileId,
                cancellationToken);
            if (!adjustmentValid)
            {
                return ServiceResult<UnitDamageLiabilityResponse>.BadRequest("Linked financial adjustment is invalid.");
            }
        }

        if (request.WorkOrderId.HasValue)
        {
            var workOrderValid = await dbContext.WorkOrders.AnyAsync(item =>
                item.Id == request.WorkOrderId.Value
                && item.CompoundId == validation.CompoundId,
                cancellationToken);
            if (!workOrderValid)
            {
                return ServiceResult<UnitDamageLiabilityResponse>.BadRequest("Linked work order is invalid.");
            }
        }

        var liability = new UnitDamageLiability
        {
            CompoundId = validation.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            ResidentProfileId = request.ResidentProfileId,
            ResidentLifecycleProcessId = request.ResidentLifecycleProcessId,
            EstimatedAmount = request.EstimatedAmount,
            Description = description,
            FinancialAdjustmentId = request.FinancialAdjustmentId,
            WorkOrderId = request.WorkOrderId,
            Notes = TrimOptional(request.Notes),
            CreatedByUserId = currentUserId.Value,
            Status = request.FinancialAdjustmentId.HasValue ? DamageLiabilityStatus.Charged : DamageLiabilityStatus.Draft
        };

        dbContext.UnitDamageLiabilities.Add(liability);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UnitDamageLiabilityResponse>.Success(ToDamageResponse(liability));
    }


    public async Task<ServiceResult<UnitDamageLiabilityResponse>> UpdateDamageLiabilityStatusAsync(
        Guid id,
        Guid? currentUserId,
        UpdateDamageLiabilityStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitDamageLiabilityResponse>.Forbidden("Current user is required.");
        }

        if (request.Status is not (DamageLiabilityStatus.Disputed or DamageLiabilityStatus.Resolved or DamageLiabilityStatus.Cancelled))
        {
            return ServiceResult<UnitDamageLiabilityResponse>.BadRequest("Damage liability can only be marked disputed, resolved, or cancelled from settlement workflow.");
        }

        var liability = await dbContext.UnitDamageLiabilities.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (liability is null || !await CanAccessCompoundAsync(liability.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitDamageLiabilityResponse>.NotFound("Damage liability was not found.");
        }

        if (liability.Status is DamageLiabilityStatus.Resolved or DamageLiabilityStatus.Cancelled)
        {
            return ServiceResult<UnitDamageLiabilityResponse>.Conflict("Resolved or cancelled damage liabilities cannot be changed.");
        }

        liability.Status = request.Status;
        liability.Notes = TrimOptional(request.Notes) ?? liability.Notes;
        liability.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UnitDamageLiabilityResponse>.Success(ToDamageResponse(liability));
    }

    public async Task<ServiceResult<ResidentCustodyItemResponse>> UpdateCustodyItemSettlementStatusAsync(
        Guid id,
        Guid? currentUserId,
        UpdateCustodySettlementStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentCustodyItemResponse>.Forbidden("Current user is required.");
        }

        if (request.Status is not (CustodyItemStatus.Returned or CustodyItemStatus.Lost or CustodyItemStatus.Damaged))
        {
            return ServiceResult<ResidentCustodyItemResponse>.BadRequest("Custody settlement status must be returned, lost, or damaged.");
        }

        var custodyItem = await dbContext.ResidentCustodyItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (custodyItem is null || !await CanAccessCompoundAsync(custodyItem.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentCustodyItemResponse>.NotFound("Custody item was not found.");
        }

        if (custodyItem.Status == CustodyItemStatus.Returned)
        {
            return ServiceResult<ResidentCustodyItemResponse>.Conflict("Returned custody items cannot be changed.");
        }

        if (request.Status is CustodyItemStatus.Lost or CustodyItemStatus.Damaged)
        {
            var notes = TrimOrNull(request.Notes);
            if (notes is null)
            {
                return ServiceResult<ResidentCustodyItemResponse>.BadRequest("Lost or damaged custody settlement requires notes.");
            }

            custodyItem.Notes = notes;
        }
        else
        {
            custodyItem.Notes = TrimOptional(request.Notes) ?? custodyItem.Notes;
        }

        custodyItem.Status = request.Status;
        custodyItem.ReturnedAtUtc = request.Status == CustodyItemStatus.Returned
            ? request.ReturnedAtUtc ?? DateTime.UtcNow
            : null;
        custodyItem.ReturnedByUserId = currentUserId.Value;
        custodyItem.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ResidentCustodyItemResponse>.Success(ToCustodyResponse(custodyItem));
    }

    public async Task<ServiceResult<MoveOutOperationalSettlementResponse>> GetMoveOutOperationalSettlementAsync(
        Guid residentLifecycleProcessId,
        CancellationToken cancellationToken = default)
    {
        var process = await dbContext.ResidentLifecycleProcesses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentLifecycleProcessId, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveOutOperationalSettlementResponse>.NotFound("Lifecycle process was not found.");
        }

        if (process.ProcessType != ResidentLifecycleProcessType.MoveOut)
        {
            return ServiceResult<MoveOutOperationalSettlementResponse>.BadRequest("Operational settlement is only available for move-out processes.");
        }

        var marker = BuildMoveOutFinalReadingMarker(process.Id);
        var activeMeters = await dbContext.Meters.AsNoTracking()
            .Where(item => item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.IsActive)
            .OrderBy(item => item.MeterType)
            .ThenBy(item => item.MeterNumber)
            .ToListAsync(cancellationToken);

        var activeMeterIds = activeMeters.Select(item => item.Id).ToArray();
        List<MeterReading> finalReadings = activeMeterIds.Length == 0
            ? []
            : await dbContext.MeterReadings.AsNoTracking()
                .Where(item => activeMeterIds.Contains(item.MeterId)
                    && item.Notes != null
                    && item.Notes.Contains(marker))
                .ToListAsync(cancellationToken);

        var meterItems = activeMeters.Select(meter =>
        {
            var reading = finalReadings
                .Where(item => item.MeterId == meter.Id)
                .OrderByDescending(item => item.ReadingDate)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefault();

            return new MoveOutMeterSettlementItemResponse(
                meter.Id,
                meter.MeterType,
                meter.MeterNumber,
                reading is not null,
                reading?.Id,
                reading?.PreviousReading,
                reading?.CurrentReading,
                reading?.Consumption,
                reading?.Amount,
                reading?.IsBilled,
                reading?.ReadingDate,
                reading is null
                    ? "Record final move-out reading."
                    : reading.IsBilled
                        ? "No action required."
                        : "Generate the final utility bill line for this reading.");
        }).ToArray();

        var custodyItems = await dbContext.ResidentCustodyItems.AsNoTracking()
            .Where(item => item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.ResidentProfileId == process.ResidentProfileId
                && item.Status != CustodyItemStatus.Returned)
            .OrderBy(item => item.Status)
            .ThenBy(item => item.Identifier)
            .ToListAsync(cancellationToken);
        var custodyResponses = custodyItems.Select(item => new MoveOutCustodySettlementItemResponse(
            item.Id,
            item.ItemType,
            item.Status,
            item.Identifier,
            item.ReplacementFeeAmount,
            item.Status == CustodyItemStatus.Issued
                ? "Return, mark lost, or mark damaged."
                : "Confirm replacement fee or related damage/financial action.")).ToArray();

        var damageLiabilities = await dbContext.UnitDamageLiabilities.AsNoTracking()
            .Where(item => item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.ResidentProfileId == process.ResidentProfileId
                && (item.Status == DamageLiabilityStatus.Draft
                    || item.Status == DamageLiabilityStatus.Charged
                    || item.Status == DamageLiabilityStatus.Disputed))
            .OrderByDescending(item => item.EstimatedAmount)
            .ToListAsync(cancellationToken);
        var damageResponses = damageLiabilities.Select(item => new MoveOutDamageSettlementItemResponse(
            item.Id,
            item.Status,
            item.EstimatedAmount,
            item.Description,
            item.FinancialAdjustmentId,
            item.Status == DamageLiabilityStatus.Charged
                ? "Collect or settle the linked financial adjustment, then mark resolved."
                : "Resolve, cancel, or formally dispute this liability.")).ToArray();

        var missingFinalReadingCount = meterItems.Count(item => !item.HasFinalReading);
        var unbilledFinalReadingCount = finalReadings.Count(item => !item.IsBilled);
        var issuedCustodyItemCount = custodyItems.Count(item => item.Status == CustodyItemStatus.Issued);
        var lostOrDamagedCustodyItemCount = custodyItems.Count(item => item.Status is CustodyItemStatus.Lost or CustodyItemStatus.Damaged);
        var custodyReplacementAmount = custodyItems
            .Where(item => item.Status is CustodyItemStatus.Issued or CustodyItemStatus.Lost or CustodyItemStatus.Damaged)
            .Sum(item => item.ReplacementFeeAmount ?? 0);
        var openDamageLiabilityAmount = damageLiabilities.Sum(item => item.EstimatedAmount);

        var blockers = BuildMoveOutSettlementBlockers(
            missingFinalReadingCount,
            unbilledFinalReadingCount,
            issuedCustodyItemCount,
            lostOrDamagedCustodyItemCount,
            damageLiabilities.Count,
            openDamageLiabilityAmount);

        var canComplete = missingFinalReadingCount == 0
            && unbilledFinalReadingCount == 0
            && issuedCustodyItemCount == 0
            && lostOrDamagedCustodyItemCount == 0
            && damageLiabilities.Count == 0;

        return ServiceResult<MoveOutOperationalSettlementResponse>.Success(new MoveOutOperationalSettlementResponse(
            process.Id,
            process.CompoundId,
            process.PropertyUnitId,
            process.ResidentProfileId,
            process.Status,
            activeMeters.Count,
            finalReadings.Select(item => item.MeterId).Distinct().Count(),
            missingFinalReadingCount,
            unbilledFinalReadingCount,
            finalReadings.Sum(item => item.Amount),
            issuedCustodyItemCount,
            lostOrDamagedCustodyItemCount,
            custodyReplacementAmount,
            damageLiabilities.Count,
            openDamageLiabilityAmount,
            canComplete,
            blockers,
            meterItems,
            custodyResponses,
            damageResponses));
    }

    public async Task<ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>> RecordMoveOutFinalMeterReadingsAsync(
        Guid residentLifecycleProcessId,
        Guid? currentUserId,
        RecordMoveOutFinalMeterReadingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.Forbidden("Current user is required.");
        }

        if (request.Readings.Count == 0)
        {
            return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.BadRequest("At least one final meter reading is required.");
        }

        var duplicateMeterId = request.Readings
            .GroupBy(item => item.MeterId)
            .FirstOrDefault(group => group.Key != Guid.Empty && group.Count() > 1)
            ?.Key;
        if (duplicateMeterId.HasValue)
        {
            return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.BadRequest("Each meter can be submitted only once per request.");
        }

        var process = await dbContext.ResidentLifecycleProcesses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentLifecycleProcessId, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.NotFound("Lifecycle process was not found.");
        }

        if (process.ProcessType != ResidentLifecycleProcessType.MoveOut)
        {
            return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.BadRequest("Final meter readings are only available for move-out processes.");
        }

        if (process.Status is ResidentLifecycleStatus.Completed or ResidentLifecycleStatus.Cancelled)
        {
            return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.Conflict("Completed or cancelled lifecycle processes cannot receive final meter readings.");
        }

        var marker = BuildMoveOutFinalReadingMarker(process.Id);
        var responses = new List<MoveOutFinalMeterReadingResponse>();
        foreach (var requestedReading in request.Readings)
        {
            if (requestedReading.MeterId == Guid.Empty)
            {
                return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.BadRequest("Meter id is required.");
            }

            var meter = await dbContext.Meters.AsNoTracking().FirstOrDefaultAsync(item =>
                item.Id == requestedReading.MeterId
                && item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.IsActive,
                cancellationToken);
            if (meter is null)
            {
                return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.BadRequest("Meter must be active and linked to the move-out unit.");
            }

            var duplicateFinalReadingExists = await dbContext.MeterReadings.AsNoTracking().AnyAsync(item =>
                item.MeterId == meter.Id
                && item.Notes != null
                && item.Notes.Contains(marker),
                cancellationToken);
            if (duplicateFinalReadingExists)
            {
                return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.Conflict($"Final move-out reading already exists for meter {meter.MeterNumber}.");
            }

            var previousReading = await dbContext.MeterReadings.AsNoTracking()
                .Where(item => item.MeterId == meter.Id)
                .OrderByDescending(item => item.ReadingDate)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var previousValue = previousReading?.CurrentReading ?? 0;
            if (requestedReading.CurrentReading < previousValue)
            {
                return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.BadRequest($"Final reading for meter {meter.MeterNumber} cannot be lower than the previous reading.");
            }

            var readingDate = requestedReading.ReadingDateUtc ?? DateTime.UtcNow;
            var consumption = requestedReading.CurrentReading - previousValue;
            var meterReading = new MeterReading
            {
                CompoundId = process.CompoundId,
                MeterId = meter.Id,
                PropertyUnitId = process.PropertyUnitId,
                Year = readingDate.Year,
                Month = readingDate.Month,
                PreviousReading = previousValue,
                CurrentReading = requestedReading.CurrentReading,
                Consumption = consumption,
                RatePerUnit = meter.RatePerUnit,
                Amount = consumption * meter.RatePerUnit,
                IsBilled = false,
                ReadingDate = readingDate,
                Notes = BuildMoveOutFinalReadingNotes(process.Id, requestedReading.Notes)
            };

            dbContext.MeterReadings.Add(meterReading);
            responses.Add(new MoveOutFinalMeterReadingResponse(
                meterReading.Id,
                meter.Id,
                meter.MeterType,
                meter.MeterNumber,
                meterReading.PreviousReading,
                meterReading.CurrentReading,
                meterReading.Consumption,
                meterReading.RatePerUnit,
                meterReading.Amount,
                meterReading.IsBilled,
                meterReading.ReadingDate));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<IReadOnlyCollection<MoveOutFinalMeterReadingResponse>>.Success(responses);
    }


    public async Task<ServiceResult<MoveOutExitCertificateResponse>> GetMoveOutExitCertificateAsync(
        Guid residentLifecycleProcessId,
        CancellationToken cancellationToken = default)
    {
        var process = await dbContext.ResidentLifecycleProcesses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentLifecycleProcessId, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveOutExitCertificateResponse>.NotFound("Lifecycle process was not found.");
        }

        if (process.ProcessType != ResidentLifecycleProcessType.MoveOut)
        {
            return ServiceResult<MoveOutExitCertificateResponse>.BadRequest("Exit certificates are only available for move-out processes.");
        }

        var unit = await dbContext.PropertyUnits.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == process.PropertyUnitId, cancellationToken);
        var resident = await dbContext.ResidentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == process.ResidentProfileId, cancellationToken);
        if (unit is null || resident is null || unit.CompoundId != resident.CompoundId)
        {
            return ServiceResult<MoveOutExitCertificateResponse>.NotFound("Unit or resident was not found.");
        }

        var operationalResult = await GetMoveOutOperationalSettlementAsync(process.Id, cancellationToken);
        if (!operationalResult.IsSuccess)
        {
            return operationalResult.Status switch
            {
                ServiceResultStatus.BadRequest => ServiceResult<MoveOutExitCertificateResponse>.BadRequest(operationalResult.Message ?? "Operational settlement is invalid."),
                ServiceResultStatus.Forbidden => ServiceResult<MoveOutExitCertificateResponse>.Forbidden(operationalResult.Message ?? "Current user cannot access this compound."),
                ServiceResultStatus.Conflict => ServiceResult<MoveOutExitCertificateResponse>.Conflict(operationalResult.Message ?? "Operational settlement is blocked."),
                _ => ServiceResult<MoveOutExitCertificateResponse>.NotFound(operationalResult.Message ?? "Lifecycle process was not found.")
            };
        }

        var blockers = operationalResult.Value!.Blockers.ToList();
        if (process.FinancialClearanceRequired && !process.FinancialClearanceConfirmed)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "FINANCIAL_CLEARANCE_NOT_CONFIRMED",
                "High",
                "Financial clearance has not been confirmed for this move-out process.",
                "Confirm financial clearance after all balances, disputes, and collection cases are settled."));
        }

        if (process.Status != ResidentLifecycleStatus.Completed)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "MOVE_OUT_NOT_COMPLETED",
                "High",
                "The move-out lifecycle process is not completed yet.",
                "Complete the move-out process before issuing the exit certificate."));
        }

        var certificateEligible = blockers.Count == 0
            && process.Status == ResidentLifecycleStatus.Completed
            && operationalResult.Value.CanCompleteOperationalSettlement
            && (!process.FinancialClearanceRequired || process.FinancialClearanceConfirmed);

        return ServiceResult<MoveOutExitCertificateResponse>.Success(new MoveOutExitCertificateResponse(
            BuildMoveOutExitCertificateNumber(process),
            process.Id,
            process.CompoundId,
            process.PropertyUnitId,
            unit.UnitNumber,
            process.ResidentProfileId,
            resident.FullName,
            process.Status,
            process.TargetDate,
            process.FinancialClearanceRequired,
            process.FinancialClearanceConfirmed,
            operationalResult.Value.CanCompleteOperationalSettlement,
            process.Status == ResidentLifecycleStatus.Completed,
            certificateEligible,
            process.CompletedAtUtc,
            DateTime.UtcNow,
            blockers));
    }

    public async Task<ServiceResult<UnitReadinessRecordResponse>> PrepareMoveOutUnitTurnoverAsync(
        Guid residentLifecycleProcessId,
        Guid? currentUserId,
        PrepareMoveOutUnitTurnoverRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<UnitReadinessRecordResponse>.Forbidden("Current user is required.");
        }

        if (!Enum.IsDefined(request.InitialStatus))
        {
            return ServiceResult<UnitReadinessRecordResponse>.BadRequest("Unit readiness status is invalid.");
        }

        var process = await dbContext.ResidentLifecycleProcesses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentLifecycleProcessId, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<UnitReadinessRecordResponse>.NotFound("Lifecycle process was not found.");
        }

        if (process.ProcessType != ResidentLifecycleProcessType.MoveOut)
        {
            return ServiceResult<UnitReadinessRecordResponse>.BadRequest("Unit turnover can only be prepared from move-out processes.");
        }

        if (process.Status != ResidentLifecycleStatus.Completed)
        {
            return ServiceResult<UnitReadinessRecordResponse>.Conflict("Move-out must be completed before preparing unit turnover.");
        }

        var existing = await dbContext.UnitReadinessRecords.AsNoTracking()
            .Where(item => item.ResidentLifecycleProcessId == process.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return ServiceResult<UnitReadinessRecordResponse>.Conflict("Unit turnover readiness has already been prepared for this move-out process.");
        }

        var unit = await dbContext.PropertyUnits.FirstOrDefaultAsync(item => item.Id == process.PropertyUnitId, cancellationToken);
        if (unit is null)
        {
            return ServiceResult<UnitReadinessRecordResponse>.NotFound("Property unit was not found.");
        }

        var readiness = new UnitReadinessRecord
        {
            CompoundId = process.CompoundId,
            PropertyUnitId = process.PropertyUnitId,
            ResidentLifecycleProcessId = process.Id,
            Status = request.InitialStatus,
            Notes = TrimOptional(request.Notes) ?? "Created by move-out unit turnover preparation.",
            CreatedByUserId = currentUserId.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.UnitReadinessRecords.Add(readiness);
        unit.UnitStatus = request.InitialStatus == UnitReadinessStatus.ReadyForMoveIn
            ? UnitStatus.Available
            : UnitStatus.UnderMaintenance;
        unit.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UnitReadinessRecordResponse>.Success(ToReadinessResponse(readiness));
    }

    public async Task<ServiceResult<MoveOutUnitTurnoverTimelineResponse>> GetMoveOutUnitTurnoverTimelineAsync(
        Guid residentLifecycleProcessId,
        CancellationToken cancellationToken = default)
    {
        var process = await dbContext.ResidentLifecycleProcesses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentLifecycleProcessId, cancellationToken);
        if (process is null || !await CanAccessCompoundAsync(process.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveOutUnitTurnoverTimelineResponse>.NotFound("Lifecycle process was not found.");
        }

        if (process.ProcessType != ResidentLifecycleProcessType.MoveOut)
        {
            return ServiceResult<MoveOutUnitTurnoverTimelineResponse>.BadRequest("Unit turnover timeline is only available for move-out processes.");
        }

        var unit = await dbContext.PropertyUnits.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == process.PropertyUnitId, cancellationToken);
        var resident = await dbContext.ResidentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == process.ResidentProfileId, cancellationToken);
        if (unit is null || resident is null || unit.CompoundId != resident.CompoundId)
        {
            return ServiceResult<MoveOutUnitTurnoverTimelineResponse>.NotFound("Unit or resident was not found.");
        }

        var marker = BuildMoveOutFinalReadingMarker(process.Id);
        var activeMeterIds = await dbContext.Meters.AsNoTracking()
            .Where(item => item.CompoundId == process.CompoundId
                && item.PropertyUnitId == process.PropertyUnitId
                && item.IsActive)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var finalReadings = activeMeterIds.Count == 0
            ? []
            : await dbContext.MeterReadings.AsNoTracking()
                .Where(item => activeMeterIds.Contains(item.MeterId)
                    && item.Notes != null
                    && item.Notes.Contains(marker))
                .ToListAsync(cancellationToken);

        var unsettledCustodyCount = await dbContext.ResidentCustodyItems.AsNoTracking().CountAsync(item =>
            item.PropertyUnitId == process.PropertyUnitId
            && item.ResidentProfileId == process.ResidentProfileId
            && item.Status != CustodyItemStatus.Returned,
            cancellationToken);
        var openDamageCount = await dbContext.UnitDamageLiabilities.AsNoTracking().CountAsync(item =>
            item.CompoundId == process.CompoundId
            && item.PropertyUnitId == process.PropertyUnitId
            && item.ResidentProfileId == process.ResidentProfileId
            && (item.Status == DamageLiabilityStatus.Draft
                || item.Status == DamageLiabilityStatus.Charged
                || item.Status == DamageLiabilityStatus.Disputed),
            cancellationToken);
        var movePermits = await dbContext.MoveLogisticsPermits.AsNoTracking()
            .Where(item => item.ResidentLifecycleProcessId == process.Id)
            .ToListAsync(cancellationToken);
        var readinessRecords = await dbContext.UnitReadinessRecords.AsNoTracking()
            .Where(item => item.ResidentLifecycleProcessId == process.Id)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var latestReadiness = readinessRecords.LastOrDefault();

        var timeline = new List<MoveOutTurnoverTimelineItemResponse>
        {
            new(
                "MOVE_OUT_PROCESS_CREATED",
                "Completed",
                process.CreatedAtUtc,
                "Move-out lifecycle process was created.",
                "Continue clearance and settlement checks."),
            new(
                "FINANCIAL_CLEARANCE",
                process.FinancialClearanceRequired
                    ? process.FinancialClearanceConfirmed ? "Completed" : "Blocked"
                    : "NotRequired",
                process.FinancialClearanceConfirmedAtUtc,
                process.FinancialClearanceRequired
                    ? "Financial clearance is required for this move-out process."
                    : "Financial clearance is not required for this move-out process.",
                process.FinancialClearanceRequired && !process.FinancialClearanceConfirmed
                    ? "Confirm financial clearance before completing move-out."
                    : "No action required."),
            new(
                "FINAL_METER_READINGS",
                activeMeterIds.Count == finalReadings.Select(item => item.MeterId).Distinct().Count()
                    ? finalReadings.Any(item => !item.IsBilled) ? "Blocked" : "Completed"
                    : "Blocked",
                finalReadings.Count == 0 ? null : finalReadings.Max(item => item.ReadingDate),
                $"Final readings recorded for {finalReadings.Select(item => item.MeterId).Distinct().Count()} of {activeMeterIds.Count} active meters.",
                activeMeterIds.Count == finalReadings.Select(item => item.MeterId).Distinct().Count()
                    ? finalReadings.Any(item => !item.IsBilled) ? "Bill all final meter readings." : "No action required."
                    : "Record final readings for all active meters."),
            new(
                "CUSTODY_CLEARANCE",
                unsettledCustodyCount == 0 ? "Completed" : "Blocked",
                null,
                $"There are {unsettledCustodyCount} custody items that are not returned.",
                unsettledCustodyCount == 0 ? "No action required." : "Return or settle all custody items."),
            new(
                "DAMAGE_SETTLEMENT",
                openDamageCount == 0 ? "Completed" : "Blocked",
                null,
                $"There are {openDamageCount} open damage liabilities.",
                openDamageCount == 0 ? "No action required." : "Resolve or cancel all open damage liabilities."),
            new(
                "MOVE_LOGISTICS",
                movePermits.Any(item => item.Status == MoveLogisticsPermitStatus.Completed)
                    ? "Completed"
                    : movePermits.Any(item => item.Status == MoveLogisticsPermitStatus.Approved)
                        ? "InProgress"
                        : movePermits.Count == 0 ? "NotStarted" : "Pending",
                movePermits.Where(item => item.CompletedAtUtc.HasValue).Max(item => item.CompletedAtUtc),
                $"There are {movePermits.Count} move logistics permits linked to this process.",
                movePermits.Any(item => item.Status == MoveLogisticsPermitStatus.Completed)
                    ? "No action required."
                    : "Approve and complete move logistics when applicable."),
            new(
                "MOVE_OUT_COMPLETION",
                process.Status == ResidentLifecycleStatus.Completed ? "Completed" : "Pending",
                process.CompletedAtUtc,
                "Move-out process completion closes the resident occupancy and releases the unit for turnover.",
                process.Status == ResidentLifecycleStatus.Completed
                    ? "Prepare unit turnover readiness."
                    : "Complete the move-out process after all blockers are cleared."),
            new(
                "UNIT_TURNOVER_READINESS",
                latestReadiness is null
                    ? "NotStarted"
                    : latestReadiness.Status == UnitReadinessStatus.ReadyForMoveIn ? "Completed" : "InProgress",
                latestReadiness?.UpdatedAtUtc ?? latestReadiness?.CreatedAtUtc,
                latestReadiness is null
                    ? "No unit readiness record has been created for this move-out process."
                    : $"Latest unit readiness status is {latestReadiness.Status}.",
                latestReadiness is null
                    ? "Prepare unit turnover and create the inspection/readiness record."
                    : latestReadiness.Status == UnitReadinessStatus.ReadyForMoveIn ? "No action required." : "Finish inspection, cleaning, or maintenance until the unit is ready." )
        };

        var readyForNextResident = process.Status == ResidentLifecycleStatus.Completed
            && latestReadiness?.Status == UnitReadinessStatus.ReadyForMoveIn
            && unit.UnitStatus == UnitStatus.Available;

        return ServiceResult<MoveOutUnitTurnoverTimelineResponse>.Success(new MoveOutUnitTurnoverTimelineResponse(
            process.Id,
            process.CompoundId,
            process.PropertyUnitId,
            unit.UnitNumber,
            process.ResidentProfileId,
            resident.FullName,
            process.Status,
            unit.UnitStatus,
            latestReadiness?.Status,
            readyForNextResident,
            timeline));
    }

    public async Task<ServiceResult<MoveOutReadinessResponse>> GetMoveOutReadinessAsync(
        MoveOutReadinessQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.PropertyUnitId == Guid.Empty)
        {
            return ServiceResult<MoveOutReadinessResponse>.BadRequest("Property unit is required.");
        }

        if (query.ResidentProfileId == Guid.Empty)
        {
            return ServiceResult<MoveOutReadinessResponse>.BadRequest("Resident profile is required.");
        }

        var validation = await ValidateUnitAndResidentAsync(query.PropertyUnitId, query.ResidentProfileId, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<MoveOutReadinessResponse>.NotFound(validation.Error!);
        }

        if (!await CanAccessCompoundAsync(validation.CompoundId, cancellationToken))
        {
            return ServiceResult<MoveOutReadinessResponse>.Forbidden("Current user cannot access this compound.");
        }

        var asOfDate = query.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var unit = await dbContext.PropertyUnits.AsNoTracking().FirstAsync(item => item.Id == query.PropertyUnitId, cancellationToken);
        var resident = await dbContext.ResidentProfiles.AsNoTracking().FirstAsync(item => item.Id == query.ResidentProfileId, cancellationToken);

        var hasActiveOccupancy = await dbContext.OccupancyRecords.AsNoTracking().AnyAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.PropertyUnitId == query.PropertyUnitId
            && item.ResidentProfileId == query.ResidentProfileId
            && item.OccupancyStatus == OccupancyStatus.Active,
            cancellationToken);

        var activeMoveOutProcess = await dbContext.ResidentLifecycleProcesses.AsNoTracking()
            .Where(item =>
                item.CompoundId == validation.CompoundId
                && item.PropertyUnitId == query.PropertyUnitId
                && item.ResidentProfileId == query.ResidentProfileId
                && item.ProcessType == ResidentLifecycleProcessType.MoveOut
                && item.Status != ResidentLifecycleStatus.Completed
                && item.Status != ResidentLifecycleStatus.Cancelled)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var activeDisputes = await dbContext.FinancialDisputes.AsNoTracking()
            .Where(item =>
                item.CompoundId == validation.CompoundId
                && item.ResidentProfileId == query.ResidentProfileId
                && (item.Status == FinancialDisputeStatus.Open
                    || item.Status == FinancialDisputeStatus.UnderReview
                    || item.Status == FinancialDisputeStatus.NeedResidentResponse
                    || item.Status == FinancialDisputeStatus.Accepted))
            .ToListAsync(cancellationToken);

        var financialItems = new List<MoveOutReadinessFinancialItemResponse>();

        var utilityBills = await dbContext.UtilityBills.AsNoTracking()
            .Where(item =>
                item.CompoundId == validation.CompoundId
                && item.PropertyUnitId == query.PropertyUnitId
                && item.ResidentProfileId == query.ResidentProfileId
                && item.BillStatus != BillStatus.Paid
                && item.BillStatus != BillStatus.Cancelled
                && item.TotalAmount > item.PaidAmount)
            .ToListAsync(cancellationToken);
        foreach (var bill in utilityBills)
        {
            var dispute = FindActiveDispute(activeDisputes, FinancialLedgerSourceType.UtilityBill, bill.Id);
            financialItems.Add(new MoveOutReadinessFinancialItemResponse(
                FinancialLedgerSourceType.UtilityBill,
                bill.Id,
                bill.BillNumber,
                bill.DueDate,
                bill.TotalAmount,
                bill.PaidAmount,
                Math.Max(0, bill.TotalAmount - bill.PaidAmount),
                bill.DueDate < asOfDate,
                dispute is not null,
                dispute?.Id,
                dispute?.Status));
        }

        var rentInvoices = await dbContext.RentInvoices.AsNoTracking()
            .Where(item =>
                item.CompoundId == validation.CompoundId
                && item.PropertyUnitId == query.PropertyUnitId
                && item.ResidentProfileId == query.ResidentProfileId
                && item.RentInvoiceStatus != RentInvoiceStatus.Paid
                && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled
                && item.TotalAmount > item.PaidAmount)
            .ToListAsync(cancellationToken);
        foreach (var invoice in rentInvoices)
        {
            var dispute = FindActiveDispute(activeDisputes, FinancialLedgerSourceType.RentInvoice, invoice.Id);
            financialItems.Add(new MoveOutReadinessFinancialItemResponse(
                FinancialLedgerSourceType.RentInvoice,
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.DueDate,
                invoice.TotalAmount,
                invoice.PaidAmount,
                Math.Max(0, invoice.TotalAmount - invoice.PaidAmount),
                invoice.DueDate < asOfDate,
                dispute is not null,
                dispute?.Id,
                dispute?.Status));
        }

        var installments = await dbContext.InstallmentScheduleItems.AsNoTracking()
            .Where(item =>
                item.CompoundId == validation.CompoundId
                && item.PropertyUnitId == query.PropertyUnitId
                && item.ResidentProfileId == query.ResidentProfileId
                && item.InstallmentStatus != InstallmentStatus.Paid
                && item.InstallmentStatus != InstallmentStatus.Cancelled
                && item.Amount > item.PaidAmount)
            .ToListAsync(cancellationToken);
        foreach (var installment in installments)
        {
            var dispute = FindActiveDispute(activeDisputes, FinancialLedgerSourceType.PropertyInstallment, installment.Id);
            financialItems.Add(new MoveOutReadinessFinancialItemResponse(
                FinancialLedgerSourceType.PropertyInstallment,
                installment.Id,
                $"Installment #{installment.InstallmentNumber}",
                installment.DueDate,
                installment.Amount,
                installment.PaidAmount,
                Math.Max(0, installment.Amount - installment.PaidAmount),
                installment.DueDate < asOfDate,
                dispute is not null,
                dispute?.Id,
                dispute?.Status));
        }

        var violationFines = await dbContext.ViolationFines.AsNoTracking()
            .Where(item =>
                item.CompoundId == validation.CompoundId
                && item.ResidentProfileId == query.ResidentProfileId
                && item.Status != ViolationFineStatus.Paid
                && item.Status != ViolationFineStatus.Cancelled
                && item.Amount > item.PaidAmount)
            .ToListAsync(cancellationToken);
        foreach (var fine in violationFines)
        {
            var dispute = FindActiveDispute(activeDisputes, FinancialLedgerSourceType.ViolationFine, fine.Id);
            financialItems.Add(new MoveOutReadinessFinancialItemResponse(
                FinancialLedgerSourceType.ViolationFine,
                fine.Id,
                $"Violation fine {fine.Id:N}",
                fine.DueDate,
                fine.Amount,
                fine.PaidAmount,
                Math.Max(0, fine.Amount - fine.PaidAmount),
                fine.DueDate < asOfDate,
                dispute is not null,
                dispute?.Id,
                dispute?.Status));
        }

        financialItems = financialItems
            .OrderByDescending(item => item.IsOverdue)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.SourceType)
            .ToList();

        var openCollectionCaseCount = await dbContext.CollectionCases.AsNoTracking().CountAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.ResidentProfileId == query.ResidentProfileId
            && (item.Status == CollectionCaseStatus.Open
                || item.Status == CollectionCaseStatus.Paused
                || item.Status == CollectionCaseStatus.LegalEscalated
                || item.Status == CollectionCaseStatus.PaymentPlanActive),
            cancellationToken);

        var activeRentContractCount = await dbContext.RentContracts.AsNoTracking().CountAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.PropertyUnitId == query.PropertyUnitId
            && item.ResidentProfileId == query.ResidentProfileId
            && item.ContractStatus == RentContractStatus.Active,
            cancellationToken);

        var activeSaleContractCount = await dbContext.PropertySaleContracts.AsNoTracking().CountAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.PropertyUnitId == query.PropertyUnitId
            && item.ResidentProfileId == query.ResidentProfileId
            && item.ContractStatus == SaleContractStatus.Active,
            cancellationToken);

        var issuedCustodyItemCount = await dbContext.ResidentCustodyItems.AsNoTracking().CountAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.PropertyUnitId == query.PropertyUnitId
            && item.ResidentProfileId == query.ResidentProfileId
            && item.Status == CustodyItemStatus.Issued,
            cancellationToken);

        var openDamageLiabilityCount = await dbContext.UnitDamageLiabilities.AsNoTracking().CountAsync(item =>
            item.CompoundId == validation.CompoundId
            && item.PropertyUnitId == query.PropertyUnitId
            && item.ResidentProfileId == query.ResidentProfileId
            && (item.Status == DamageLiabilityStatus.Draft
                || item.Status == DamageLiabilityStatus.Charged
                || item.Status == DamageLiabilityStatus.Disputed),
            cancellationToken);

        var outstandingAmount = financialItems.Sum(item => item.OutstandingAmount);
        var hasFinancialBlockers = outstandingAmount > 0 || activeDisputes.Count > 0 || openCollectionCaseCount > 0;
        var hasOperationalBlockers = !hasActiveOccupancy
            || activeMoveOutProcess is not null
            || activeRentContractCount > 0
            || activeSaleContractCount > 0
            || issuedCustodyItemCount > 0
            || openDamageLiabilityCount > 0;
        var canStartMoveOutProcess = hasActiveOccupancy && activeMoveOutProcess is null;
        var canConfirmFinancialClearance = financialItems.Count == 0 && activeDisputes.Count == 0 && openCollectionCaseCount == 0;
        var canCompleteMoveOutNow = hasActiveOccupancy
            && activeMoveOutProcess is not null
            && canConfirmFinancialClearance
            && activeRentContractCount == 0
            && activeSaleContractCount == 0
            && issuedCustodyItemCount == 0
            && openDamageLiabilityCount == 0;

        var blockers = BuildMoveOutReadinessBlockers(
            hasActiveOccupancy,
            activeMoveOutProcess,
            outstandingAmount,
            financialItems.Count,
            activeDisputes.Count,
            openCollectionCaseCount,
            activeRentContractCount,
            activeSaleContractCount,
            issuedCustodyItemCount,
            openDamageLiabilityCount);

        return ServiceResult<MoveOutReadinessResponse>.Success(new MoveOutReadinessResponse(
            validation.CompoundId,
            query.PropertyUnitId,
            unit.UnitNumber,
            query.ResidentProfileId,
            resident.FullName,
            asOfDate,
            hasActiveOccupancy,
            activeMoveOutProcess?.Id,
            activeMoveOutProcess?.Status,
            hasFinancialBlockers,
            hasOperationalBlockers,
            canStartMoveOutProcess,
            canConfirmFinancialClearance,
            canCompleteMoveOutNow,
            outstandingAmount,
            financialItems.Count,
            activeDisputes.Count,
            openCollectionCaseCount,
            activeRentContractCount,
            activeSaleContractCount,
            issuedCustodyItemCount,
            openDamageLiabilityCount,
            blockers,
            financialItems));
    }

    public async Task<ServiceResult<ResidentLifecycleSummaryResponse>> GetSummaryAsync(
        ResidentLifecycleSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.CompoundId.HasValue && !await CanAccessCompoundAsync(query.CompoundId.Value, cancellationToken))
        {
            return ServiceResult<ResidentLifecycleSummaryResponse>.Forbidden("Current user cannot access this compound.");
        }

        var processes = await ApplyCurrentCompoundAccessAsync(dbContext.ResidentLifecycleProcesses.AsNoTracking(), cancellationToken);
        var custody = await ApplyCurrentCompoundAccessAsync(dbContext.ResidentCustodyItems.AsNoTracking(), cancellationToken);
        var permits = await ApplyCurrentCompoundAccessAsync(dbContext.MoveLogisticsPermits.AsNoTracking(), cancellationToken);
        var readiness = await ApplyCurrentCompoundAccessAsync(dbContext.UnitReadinessRecords.AsNoTracking(), cancellationToken);
        var liabilities = await ApplyCurrentCompoundAccessAsync(dbContext.UnitDamageLiabilities.AsNoTracking(), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            var compoundId = query.CompoundId.Value;
            processes = processes.Where(item => item.CompoundId == compoundId);
            custody = custody.Where(item => item.CompoundId == compoundId);
            permits = permits.Where(item => item.CompoundId == compoundId);
            readiness = readiness.Where(item => item.CompoundId == compoundId);
            liabilities = liabilities.Where(item => item.CompoundId == compoundId);
        }

        var activeStatuses = new[]
        {
            ResidentLifecycleStatus.InProgress,
            ResidentLifecycleStatus.PendingFinancialClearance,
            ResidentLifecycleStatus.PendingCustodyClearance,
            ResidentLifecycleStatus.PendingUnitReadiness
        };

        return ServiceResult<ResidentLifecycleSummaryResponse>.Success(new ResidentLifecycleSummaryResponse(
            query.CompoundId,
            await processes.CountAsync(item => activeStatuses.Contains(item.Status), cancellationToken),
            await processes.CountAsync(item => item.Status == ResidentLifecycleStatus.PendingFinancialClearance, cancellationToken),
            await custody.CountAsync(item => item.Status == CustodyItemStatus.Issued, cancellationToken),
            await permits.CountAsync(item => item.Status == MoveLogisticsPermitStatus.PendingApproval, cancellationToken),
            await readiness.CountAsync(item => item.Status != UnitReadinessStatus.ReadyForMoveIn, cancellationToken),
            await liabilities.CountAsync(item => item.Status == DamageLiabilityStatus.Draft || item.Status == DamageLiabilityStatus.Charged || item.Status == DamageLiabilityStatus.Disputed, cancellationToken),
            DateTime.UtcNow));
    }

    private async Task<(bool IsValid, Guid CompoundId, string? Error)> ValidateUnitAndResidentAsync(Guid propertyUnitId, Guid residentProfileId, CancellationToken cancellationToken)
    {
        var unit = await dbContext.PropertyUnits.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propertyUnitId, cancellationToken);
        var resident = await dbContext.ResidentProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == residentProfileId, cancellationToken);
        if (unit is null || resident is null)
        {
            return (false, Guid.Empty, "Unit or resident was not found.");
        }

        if (unit.CompoundId != resident.CompoundId)
        {
            return (false, Guid.Empty, "Unit and resident must belong to the same compound.");
        }

        return (true, unit.CompoundId, null);
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

    private static ResidentLifecycleProcessResponse ToProcessResponse(ResidentLifecycleProcess item)
    {
        return new ResidentLifecycleProcessResponse(
            item.Id,
            item.CompoundId,
            item.PropertyUnitId,
            item.ResidentProfileId,
            item.ProcessType,
            item.Status,
            item.TargetDate,
            item.FinancialClearanceRequired,
            item.FinancialClearanceConfirmed,
            item.FinancialClearanceConfirmedAtUtc,
            item.CompletedAtUtc,
            item.Notes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static ResidentCustodyItemResponse ToCustodyResponse(ResidentCustodyItem item)
    {
        return new ResidentCustodyItemResponse(
            item.Id,
            item.CompoundId,
            item.PropertyUnitId,
            item.ResidentProfileId,
            item.ItemType,
            item.Status,
            item.Identifier,
            item.Description,
            item.ReplacementFeeAmount,
            item.IssuedAtUtc,
            item.ReturnedAtUtc,
            item.Notes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static MoveLogisticsPermitResponse ToMovePermitResponse(MoveLogisticsPermit item)
    {
        return new MoveLogisticsPermitResponse(
            item.Id,
            item.CompoundId,
            item.PropertyUnitId,
            item.ResidentProfileId,
            item.ResidentLifecycleProcessId,
            item.MoveType,
            item.Status,
            item.ScheduledStartAtUtc,
            item.ScheduledEndAtUtc,
            item.TruckInfo,
            item.WorkersCount,
            item.Notes,
            item.DecisionReason,
            item.ApprovedAtUtc,
            item.CompletedAtUtc,
            item.CompletionNotes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static UnitReadinessRecordResponse ToReadinessResponse(UnitReadinessRecord item)
    {
        return new UnitReadinessRecordResponse(
            item.Id,
            item.CompoundId,
            item.PropertyUnitId,
            item.ResidentLifecycleProcessId,
            item.Status,
            item.OperationalChecklistRunId,
            item.Notes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static UnitDamageLiabilityResponse ToDamageResponse(UnitDamageLiability item)
    {
        return new UnitDamageLiabilityResponse(
            item.Id,
            item.CompoundId,
            item.PropertyUnitId,
            item.ResidentProfileId,
            item.ResidentLifecycleProcessId,
            item.Status,
            item.EstimatedAmount,
            item.Description,
            item.FinancialAdjustmentId,
            item.WorkOrderId,
            item.Notes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static IReadOnlyCollection<MoveOutReadinessBlockerResponse> BuildMoveOutReadinessBlockers(
        bool hasActiveOccupancy,
        ResidentLifecycleProcess? activeMoveOutProcess,
        decimal outstandingAmount,
        int outstandingItemCount,
        int activeFinancialDisputeCount,
        int openCollectionCaseCount,
        int activeRentContractCount,
        int activeSaleContractCount,
        int issuedCustodyItemCount,
        int openDamageLiabilityCount)
    {
        var blockers = new List<MoveOutReadinessBlockerResponse>();

        if (!hasActiveOccupancy)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "NO_ACTIVE_OCCUPANCY",
                "Critical",
                "The resident does not have an active occupancy record for this unit.",
                "Fix the occupancy record before starting move-out."));
        }

        if (activeMoveOutProcess is not null)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "ACTIVE_MOVE_OUT_PROCESS",
                "High",
                "An active move-out lifecycle process already exists for this resident and unit.",
                "Continue the existing process instead of creating another one."));
        }

        if (outstandingAmount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "OUTSTANDING_FINANCIAL_BALANCE",
                "High",
                $"There are {outstandingItemCount} outstanding financial items with total balance {outstandingAmount:0.##}.",
                "Settle the balance or resolve it through an approved financial adjustment before confirming financial clearance."));
        }

        if (activeFinancialDisputeCount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "ACTIVE_FINANCIAL_DISPUTES",
                "High",
                $"There are {activeFinancialDisputeCount} active financial disputes.",
                "Resolve or formally decide all financial disputes before confirming financial clearance."));
        }

        if (openCollectionCaseCount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "OPEN_COLLECTION_CASES",
                "High",
                $"There are {openCollectionCaseCount} open collection cases.",
                "Close, settle, or formally pause collection cases before final move-out approval."));
        }

        if (activeRentContractCount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "ACTIVE_RENT_CONTRACT",
                "Medium",
                $"There are {activeRentContractCount} active rent contracts for this unit and resident.",
                "Terminate or expire the active rent contract before completing move-out."));
        }

        if (activeSaleContractCount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "ACTIVE_SALE_CONTRACT",
                "Medium",
                $"There are {activeSaleContractCount} active sale contracts linked to this resident and unit.",
                "Review ownership/sale obligations before completing move-out."));
        }

        if (issuedCustodyItemCount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "ISSUED_CUSTODY_ITEMS",
                "Medium",
                $"There are {issuedCustodyItemCount} issued custody items.",
                "Return keys, cards, and custody items before completing move-out."));
        }

        if (openDamageLiabilityCount > 0)
        {
            blockers.Add(new MoveOutReadinessBlockerResponse(
                "OPEN_DAMAGE_LIABILITIES",
                "Medium",
                $"There are {openDamageLiabilityCount} open damage liabilities.",
                "Close or charge all damage liabilities before completing move-out."));
        }

        return blockers;
    }

    private static IReadOnlyCollection<MoveOutSettlementBlockerResponse> BuildMoveOutSettlementBlockers(
        int missingFinalReadingCount,
        int unbilledFinalReadingCount,
        int issuedCustodyItemCount,
        int lostOrDamagedCustodyItemCount,
        int openDamageLiabilityCount,
        decimal openDamageLiabilityAmount)
    {
        var blockers = new List<MoveOutSettlementBlockerResponse>();

        if (missingFinalReadingCount > 0)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "FINAL_METER_READING_MISSING",
                "High",
                $"There are {missingFinalReadingCount} active meters without final move-out readings.",
                "Record final readings for all active meters before completing move-out."));
        }

        if (unbilledFinalReadingCount > 0)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "FINAL_METER_READING_UNBILLED",
                "High",
                $"There are {unbilledFinalReadingCount} final meter readings that are not billed yet.",
                "Generate final utility bill lines and complete financial clearance."));
        }

        if (issuedCustodyItemCount > 0)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "ISSUED_CUSTODY_ITEMS",
                "Medium",
                $"There are {issuedCustodyItemCount} issued custody items.",
                "Return custody items or mark them lost/damaged with documented action."));
        }

        if (lostOrDamagedCustodyItemCount > 0)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "LOST_OR_DAMAGED_CUSTODY_ITEMS",
                "Medium",
                $"There are {lostOrDamagedCustodyItemCount} lost or damaged custody items.",
                "Confirm replacement fees or related damage liabilities before final approval."));
        }

        if (openDamageLiabilityCount > 0)
        {
            blockers.Add(new MoveOutSettlementBlockerResponse(
                "OPEN_DAMAGE_LIABILITIES",
                "High",
                $"There are {openDamageLiabilityCount} open damage liabilities with total estimated amount {openDamageLiabilityAmount:0.##}.",
                "Resolve, cancel, or formally dispute all damage liabilities before completing move-out."));
        }

        return blockers;
    }

    private static string BuildMoveOutFinalReadingMarker(Guid processId)
    {
        return $"{MoveOutFinalReadingMarkerPrefix}:{processId:N}";
    }

    private static string BuildMoveOutFinalReadingNotes(Guid processId, string? notes)
    {
        var markerValue = BuildMoveOutFinalReadingMarker(processId);
        var trimmedNotes = TrimOptional(notes);
        return trimmedNotes is null
            ? $"{markerValue}; Final move-out meter reading."
            : $"{markerValue}; Final move-out meter reading. {trimmedNotes}";
    }

    private static string BuildMoveOutExitCertificateNumber(ResidentLifecycleProcess process)
    {
        var referenceDate = process.CompletedAtUtc
            ?? process.UpdatedAtUtc
            ?? process.CreatedAtUtc;
        var datePart = referenceDate.ToString("yyyyMMdd");
        var shortProcessId = process.Id.ToString("N")[..8].ToUpperInvariant();

        return $"DARAK-EXIT-{datePart}-{shortProcessId}";
    }

    private static FinancialDispute? FindActiveDispute(
        IEnumerable<FinancialDispute> disputes,
        FinancialLedgerSourceType sourceType,
        Guid sourceId)
    {
        var targetType = ToFinancialDisputeTargetType(sourceType);
        return targetType is null
            ? null
            : disputes.FirstOrDefault(item => item.TargetType == targetType.Value && item.TargetId == sourceId);
    }

    private static FinancialDisputeTargetType? ToFinancialDisputeTargetType(FinancialLedgerSourceType sourceType)
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

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? TrimOptional(string? value)
    {
        return TrimOrNull(value);
    }
}
