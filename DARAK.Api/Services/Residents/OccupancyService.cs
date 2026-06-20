using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Occupancy;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class OccupancyService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IOccupancyService
{
    private static readonly HashSet<UnitStatus> AllowedUnitStatusesAfterEnd =
    [
        UnitStatus.Available,
        UnitStatus.UnderMaintenance,
        UnitStatus.Blocked
    ];

    public async Task<PagedResult<OccupancyRecordResponse>> SearchOccupanciesAsync(
        OccupancySearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var occupancyRecords = await ApplyCurrentOccupancyScopeAsync(
            ApplyOccupancyFilters(
                dbContext.OccupancyRecords.AsNoTracking(),
                query),
            cancellationToken);

        return await ToPagedResultAsync(
            occupancyRecords
                .OrderByDescending(record => record.StartDate)
                .ThenBy(record => record.PropertyUnit.UnitNumber),
            query,
            record => new OccupancyRecordResponse(
                record.Id,
                record.ResidentProfileId,
                record.ResidentProfile.FullName,
                record.CompoundId,
                record.Compound.Name,
                record.PropertyUnitId,
                record.PropertyUnit.UnitNumber,
                record.OccupancyType,
                record.OccupancyStatus,
                record.StartDate,
                record.EndDate,
                record.ContractNumber,
                record.Notes,
                record.CreatedAt,
                record.UpdatedAt,
                record.EndedAt),
            cancellationToken);
    }

    public async Task<PagedResult<ResidentOccupancyRecordResponse>> SearchOccupanciesForUserAsync(
        Guid userId,
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var occupancyRecords = dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.ResidentProfile.UserId == userId);

        return await ToPagedResultAsync(
            occupancyRecords
                .OrderByDescending(record => record.StartDate)
                .ThenBy(record => record.PropertyUnit.UnitNumber),
            query,
            record => new ResidentOccupancyRecordResponse(
                record.Id,
                record.ResidentProfileId,
                record.ResidentProfile.FullName,
                record.CompoundId,
                record.Compound.Name,
                record.PropertyUnitId,
                record.PropertyUnit.UnitNumber,
                record.OccupancyType,
                record.OccupancyStatus,
                record.StartDate,
                record.EndDate,
                record.ContractNumber,
                record.CreatedAt,
                record.UpdatedAt,
                record.EndedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<OccupancyRecordResponse>> GetOccupancyAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var occupancyRecords = await ApplyCurrentOccupancyScopeAsync(
            dbContext.OccupancyRecords
                .AsNoTracking()
                .Include(record => record.ResidentProfile)
                .Include(record => record.Compound)
                .Include(record => record.PropertyUnit),
            cancellationToken);

        var occupancyRecord = await occupancyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.Id == id, cancellationToken);

        return occupancyRecord is null
            ? ServiceResult<OccupancyRecordResponse>.NotFound("Occupancy record was not found.")
            : ServiceResult<OccupancyRecordResponse>.Success(ToOccupancyRecordResponse(occupancyRecord));
    }

    public async Task<ServiceResult<OccupancyRecordResponse>> CreateOccupancyAsync(
        CreateOccupancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndDate.HasValue && request.EndDate <= request.StartDate)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest(
                "Occupancy end date must be after the start date.");
        }

        var residentProfile = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Include(profile => profile.Compound)
            .FirstOrDefaultAsync(profile => profile.Id == request.ResidentProfileId, cancellationToken);

        if (residentProfile is null)
        {
            return ServiceResult<OccupancyRecordResponse>.NotFound("Resident profile was not found.");
        }

        if (!residentProfile.IsActive)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest("Resident profile is inactive.");
        }

        if (!residentProfile.Compound.IsActive)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest("Compound is inactive.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(residentProfile.CompoundId, cancellationToken))
        {
            return ServiceResult<OccupancyRecordResponse>.Forbidden("Current user cannot access this compound.");
        }

        var propertyUnit = await dbContext.PropertyUnits
            .FirstOrDefaultAsync(unit => unit.Id == request.PropertyUnitId, cancellationToken);

        if (propertyUnit is null)
        {
            return ServiceResult<OccupancyRecordResponse>.NotFound("Property unit was not found.");
        }

        if (!propertyUnit.IsActive)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest("Property unit is inactive.");
        }

        if (propertyUnit.CompoundId != residentProfile.CompoundId)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest(
                "Property unit must belong to the resident profile compound.");
        }

        if (propertyUnit.UnitStatus != UnitStatus.Available)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest(
                "Property unit must be available before occupancy can be created.");
        }

        var activeOccupancyExists = await dbContext.OccupancyRecords.AnyAsync(
            record => record.PropertyUnitId == request.PropertyUnitId
                && record.OccupancyStatus == OccupancyStatus.Active,
            cancellationToken);

        if (activeOccupancyExists)
        {
            return ServiceResult<OccupancyRecordResponse>.Conflict(
                "An active occupancy already exists for this property unit.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var occupancyRecord = new OccupancyRecord
        {
            ResidentProfileId = residentProfile.Id,
            CompoundId = residentProfile.CompoundId,
            PropertyUnitId = propertyUnit.Id,
            OccupancyType = request.OccupancyType,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ContractNumber = TrimOrNull(request.ContractNumber),
            Notes = TrimOrNull(request.Notes)
        };

        propertyUnit.UnitStatus = MapOccupancyTypeToUnitStatus(request.OccupancyType);
        propertyUnit.UpdatedAt = DateTime.UtcNow;

        dbContext.OccupancyRecords.Add(occupancyRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        occupancyRecord.ResidentProfile = residentProfile;
        occupancyRecord.Compound = residentProfile.Compound;
        occupancyRecord.PropertyUnit = propertyUnit;

        return ServiceResult<OccupancyRecordResponse>.Success(ToOccupancyRecordResponse(occupancyRecord));
    }

    public async Task<ServiceResult<OccupancyRecordResponse>> EndOccupancyAsync(
        Guid id,
        EndOccupancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedUnitStatusesAfterEnd.Contains(request.UnitStatusAfterEnd))
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest(
                "Unit status after ending occupancy must be Available, UnderMaintenance, or Blocked.");
        }

        var occupancyRecord = await dbContext.OccupancyRecords
            .Include(record => record.ResidentProfile)
            .Include(record => record.Compound)
            .Include(record => record.PropertyUnit)
            .FirstOrDefaultAsync(record => record.Id == id, cancellationToken);

        if (occupancyRecord is null)
        {
            return ServiceResult<OccupancyRecordResponse>.NotFound("Occupancy record was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(occupancyRecord.CompoundId, cancellationToken))
        {
            return ServiceResult<OccupancyRecordResponse>.NotFound("Occupancy record was not found.");
        }

        if (occupancyRecord.OccupancyStatus != OccupancyStatus.Active)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest("Occupancy record has already ended.");
        }

        if (request.EndDate < occupancyRecord.StartDate)
        {
            return ServiceResult<OccupancyRecordResponse>.BadRequest(
                "Occupancy end date cannot be before the start date.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        occupancyRecord.OccupancyStatus = OccupancyStatus.Ended;
        occupancyRecord.EndDate = request.EndDate;
        occupancyRecord.EndedAt = DateTime.UtcNow;
        occupancyRecord.UpdatedAt = DateTime.UtcNow;
        occupancyRecord.PropertyUnit.UnitStatus = request.UnitStatusAfterEnd;
        occupancyRecord.PropertyUnit.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<OccupancyRecordResponse>.Success(ToOccupancyRecordResponse(occupancyRecord));
    }

    private static IQueryable<OccupancyRecord> ApplyOccupancyFilters(
        IQueryable<OccupancyRecord> occupancyRecords,
        OccupancySearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(record => record.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(
                record => record.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(record => record.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.OccupancyType.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(record => record.OccupancyType == query.OccupancyType.Value);
        }

        if (query.OccupancyStatus.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(record => record.OccupancyStatus == query.OccupancyStatus.Value);
        }

        if (query.StartDateFrom.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(record => record.StartDate >= query.StartDateFrom.Value);
        }

        if (query.StartDateTo.HasValue)
        {
            occupancyRecords = occupancyRecords.Where(record => record.StartDate <= query.StartDateTo.Value);
        }

        return occupancyRecords;
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TSource, TResponse>(
        IQueryable<TSource> query,
        PaginationQuery pagination,
        Expression<Func<TSource, TResponse>> selector,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(selector)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<TResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private static UnitStatus MapOccupancyTypeToUnitStatus(OccupancyType occupancyType)
    {
        return occupancyType switch
        {
            OccupancyType.Tenant => UnitStatus.Rented,
            OccupancyType.OwnerCash => UnitStatus.SoldCash,
            OccupancyType.OwnerInstallment => UnitStatus.SoldInstallment,
            _ => UnitStatus.Available
        };
    }

    private static OccupancyRecordResponse ToOccupancyRecordResponse(OccupancyRecord occupancyRecord)
    {
        return new OccupancyRecordResponse(
            occupancyRecord.Id,
            occupancyRecord.ResidentProfileId,
            occupancyRecord.ResidentProfile.FullName,
            occupancyRecord.CompoundId,
            occupancyRecord.Compound.Name,
            occupancyRecord.PropertyUnitId,
            occupancyRecord.PropertyUnit.UnitNumber,
            occupancyRecord.OccupancyType,
            occupancyRecord.OccupancyStatus,
            occupancyRecord.StartDate,
            occupancyRecord.EndDate,
            occupancyRecord.ContractNumber,
            occupancyRecord.Notes,
            occupancyRecord.CreatedAt,
            occupancyRecord.UpdatedAt,
            occupancyRecord.EndedAt);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<IQueryable<OccupancyRecord>> ApplyCurrentOccupancyScopeAsync(
        IQueryable<OccupancyRecord> occupancyRecords,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return occupancyRecords;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return occupancyRecords.ApplyCompoundAccess(scope, record => record.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }
}
