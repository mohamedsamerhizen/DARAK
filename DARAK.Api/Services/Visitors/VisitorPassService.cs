using System.Linq.Expressions;
using System.Security.Cryptography;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class VisitorPassService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null,
    IAccessCodeHasher? accessCodeHasher = null)
    : IVisitorPassService
{
    private const string MaskedAccessCode = "********";
    private readonly IAccessCodeHasher accessCodeHasher = accessCodeHasher ?? new AccessCodeHasher();

    public async Task<PagedResult<VisitorPassResponse>> SearchAdminAsync(
        VisitorPassSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(
            ApplyFilters(GetVisitorPassDetailsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedVisitorPassResultAsync(visitorPasses, query, cancellationToken);
    }

    public async Task<ServiceResult<VisitorPassResponse>> GetAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(
            GetVisitorPassDetailsQuery(asNoTracking: true),
            cancellationToken);

        var visitorPass = await visitorPasses
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);

        return visitorPass is null
            ? ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.")
            : ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(
            GetVisitorPassDetailsQuery(asNoTracking: false),
            cancellationToken);

        var visitorPass = await visitorPasses
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);
        if (visitorPass is null)
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        if (visitorPass.Status != VisitorPassStatus.Pending)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Only pending visitor passes can be approved.");
        }

        if (visitorPass.ValidUntil <= DateTime.UtcNow)
        {
            visitorPass.Status = VisitorPassStatus.Expired;
            visitorPass.UpdatedAt = DateTime.UtcNow;
            var expiredConcurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
            if (expiredConcurrencyFailure is not null)
            {
                return expiredConcurrencyFailure;
            }
            return ServiceResult<VisitorPassResponse>.BadRequest("Expired visitor pass cannot be approved.");
        }

        visitorPass.Status = VisitorPassStatus.Approved;
        visitorPass.UpdatedAt = DateTime.UtcNow;
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> DenyAsync(
        Guid id,
        DenyVisitorPassRequest request,
        Guid? guardUserId = null,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Denial reason is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var visitorPasses = guardUserId.HasValue
            ? GetVisitorPassDetailsQuery(asNoTracking: false)
            : await ApplyCurrentCompoundScopeAsync(GetVisitorPassDetailsQuery(asNoTracking: false), cancellationToken);

        var visitorPass = await visitorPasses
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);
        if (visitorPass is null)
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        if (guardUserId.HasValue
            && !await CanGuardAccessPassAsync(guardUserId, visitorPass.CompoundId, cancellationToken))
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        if (visitorPass.Status is VisitorPassStatus.CheckedIn or VisitorPassStatus.CheckedOut or VisitorPassStatus.Cancelled)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor pass cannot be denied in its current status.");
        }

        visitorPass.Status = VisitorPassStatus.Denied;
        visitorPass.DenialReason = reason;
        visitorPass.UpdatedAt = DateTime.UtcNow;
        dbContext.VisitorAccessLogs.Add(new VisitorAccessLog
        {
            VisitorPassId = visitorPass.Id,
            GuardUserId = guardUserId,
            Action = VisitorAccessAction.Denied,
            Notes = reason
        });

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> CancelAdminAsync(
        Guid id,
        CancelVisitorPassRequest request,
        CancellationToken cancellationToken = default)
    {
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(
            GetVisitorPassDetailsQuery(asNoTracking: false),
            cancellationToken);

        var visitorPass = await visitorPasses
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);

        return visitorPass is null
            ? ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.")
            : await CancelVisitorPassAsync(visitorPass, cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<VisitorPassResponse>>> SearchResidentAsync(
        Guid userId,
        VisitorPassSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return ServiceResult<PagedResult<VisitorPassResponse>>.Success(
                new PagedResult<VisitorPassResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var visitorPasses = ApplyFilters(GetVisitorPassDetailsQuery(asNoTracking: true), query)
            .Where(pass => profileIds.Contains(pass.ResidentProfileId));

        return ServiceResult<PagedResult<VisitorPassResponse>>.Success(
            await ToPagedVisitorPassResultAsync(visitorPasses, query, cancellationToken));
    }

    public async Task<ServiceResult<VisitorPassResponse>> GetResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var visitorPass = await GetVisibleResidentPassAsync(userId, id, asNoTracking: true, cancellationToken);

        return visitorPass is null
            ? ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.")
            : ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> CreateResidentAsync(
        Guid userId,
        CreateVisitorPassRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation is not null)
        {
            return ToResult<VisitorPassResponse>(validation);
        }

        var occupancy = await GetActiveResidentOccupancyForUnitAsync(
            userId,
            request.PropertyUnitId,
            cancellationToken);
        if (occupancy is null)
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Active resident occupancy was not found for this unit.");
        }

        var accessCode = await GenerateUniqueAccessCodeAsync(cancellationToken);
        var visitorPass = new VisitorPass
        {
            ResidentProfileId = occupancy.ResidentProfileId,
            CompoundId = occupancy.CompoundId,
            PropertyUnitId = occupancy.PropertyUnitId,
            VisitorName = request.VisitorName.Trim(),
            VisitorPhoneNumber = request.VisitorPhoneNumber.Trim(),
            VisitReason = request.VisitReason.Trim(),
            AccessCode = accessCodeHasher.Hash(accessCode),
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil
        };

        dbContext.VisitorPasses.Add(visitorPass);
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        var created = await GetVisibleResidentPassAsync(userId, visitorPass.Id, asNoTracking: true, cancellationToken);
        return created is null
            ? ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.")
            : ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(created, accessCode));
    }

    public async Task<ServiceResult<VisitorPassResponse>> CancelResidentAsync(
        Guid userId,
        Guid id,
        CancelVisitorPassRequest request,
        CancellationToken cancellationToken = default)
    {
        var visitorPass = await GetVisibleResidentPassAsync(userId, id, asNoTracking: false, cancellationToken);

        return visitorPass is null
            ? ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.")
            : await CancelVisitorPassAsync(visitorPass, cancellationToken);
    }

    public async Task<PagedResult<VisitorPassResponse>> SearchTodayForGuardAsync(
        Guid? guardUserId,
        VisitorPassSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var visitorPasses = ApplyFilters(GetVisitorPassDetailsQuery(asNoTracking: true), query)
            .Where(pass => pass.ValidFrom < tomorrowStart
                && pass.ValidUntil >= todayStart
                && (pass.Status == VisitorPassStatus.Approved || pass.Status == VisitorPassStatus.Pending));

        visitorPasses = await ApplyGuardCompoundScopeAsync(
            visitorPasses,
            guardUserId,
            cancellationToken);

        return await ToPagedVisitorPassResultAsync(visitorPasses, query, cancellationToken);
    }

    public async Task<ServiceResult<VisitorPassResponse>> VerifyAccessCodeAsync(
        Guid? guardUserId,
        VerifyVisitorPassAccessCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessCode = TrimOrNull(request.AccessCode);
        if (accessCode is null)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor access code is required.");
        }

        var visitorPasses = await ApplyGuardCompoundScopeAsync(
            GetVisitorPassDetailsQuery(asNoTracking: false),
            guardUserId,
            cancellationToken);

        var candidatePasses = await visitorPasses.ToArrayAsync(cancellationToken);
        var visitorPass = candidatePasses.FirstOrDefault(pass => accessCodeHasher.Verify(accessCode, pass.AccessCode));

        if (visitorPass is null || !IsVisibleToGuard(visitorPass))
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        if (visitorPass.Status == VisitorPassStatus.Pending)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor pass is pending admin approval.");
        }

        dbContext.VisitorAccessLogs.Add(new VisitorAccessLog
        {
            VisitorPassId = visitorPass.Id,
            GuardUserId = guardUserId,
            Action = VisitorAccessAction.Verified,
            Notes = "Visitor access code verified."
        });

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> GetGuardAsync(
        Guid? guardUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var visitorPass = await GetVisitorPassDetailsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);
        if (visitorPass is null
            || !IsVisibleToGuard(visitorPass)
            || !await CanGuardAccessPassAsync(guardUserId, visitorPass.CompoundId, cancellationToken))
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> CheckInAsync(
        Guid id,
        Guid? guardUserId,
        VisitorPassAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var providedAccessCode = TrimOrNull(request.AccessCode);
        if (providedAccessCode is null)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor access code is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var visitorPass = await GetVisitorPassDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);
        if (visitorPass is null
            || !await CanGuardAccessPassAsync(guardUserId, visitorPass.CompoundId, cancellationToken))
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        if (!accessCodeHasher.Verify(providedAccessCode, visitorPass.AccessCode))
        {
            dbContext.VisitorAccessLogs.Add(new VisitorAccessLog
            {
                VisitorPassId = visitorPass.Id,
                GuardUserId = guardUserId,
                Action = VisitorAccessAction.CredentialFailed,
                Notes = "Invalid visitor access code."
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor access code is invalid.");
        }

        var now = DateTime.UtcNow;
        if (visitorPass.ValidUntil <= now)
        {
            visitorPass.Status = VisitorPassStatus.Expired;
            visitorPass.UpdatedAt = now;
            var expiredConcurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
            if (expiredConcurrencyFailure is not null)
            {
                return expiredConcurrencyFailure;
            }
            return ServiceResult<VisitorPassResponse>.BadRequest("Expired visitor pass cannot be checked in.");
        }

        if (visitorPass.ValidFrom > now)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor pass is not valid yet.");
        }

        if (visitorPass.Status != VisitorPassStatus.Approved)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Only approved visitor passes can be checked in.");
        }

        if (visitorPass.CheckedInAt.HasValue)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor pass is already checked in.");
        }

        visitorPass.Status = VisitorPassStatus.CheckedIn;
        visitorPass.CheckedInAt = now;
        visitorPass.UpdatedAt = now;
        dbContext.VisitorAccessLogs.Add(new VisitorAccessLog
        {
            VisitorPassId = visitorPass.Id,
            GuardUserId = guardUserId,
            Action = VisitorAccessAction.CheckIn,
            Notes = TrimOrNull(request.Notes)
        });

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<VisitorPassResponse>> CheckOutAsync(
        Guid id,
        Guid? guardUserId,
        VisitorPassAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var visitorPass = await GetVisitorPassDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(pass => pass.Id == id, cancellationToken);
        if (visitorPass is null
            || !await CanGuardAccessPassAsync(guardUserId, visitorPass.CompoundId, cancellationToken))
        {
            return ServiceResult<VisitorPassResponse>.NotFound("Visitor pass was not found.");
        }

        if (!visitorPass.CheckedInAt.HasValue || visitorPass.Status != VisitorPassStatus.CheckedIn)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor pass must be checked in before checkout.");
        }

        if (visitorPass.CheckedOutAt.HasValue)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Visitor pass is already checked out.");
        }

        var now = DateTime.UtcNow;
        visitorPass.Status = VisitorPassStatus.CheckedOut;
        visitorPass.CheckedOutAt = now;
        visitorPass.UpdatedAt = now;
        dbContext.VisitorAccessLogs.Add(new VisitorAccessLog
        {
            VisitorPassId = visitorPass.Id,
            GuardUserId = guardUserId,
            Action = VisitorAccessAction.CheckOut,
            Notes = TrimOrNull(request.Notes)
        });

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
    }

    public async Task<ServiceResult<PagedResult<VisitorAccessLogResponse>>> GetAccessLogsAsync(
        Guid visitorPassId,
        VisitorAccessLogSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(
            dbContext.VisitorPasses.AsNoTracking(),
            cancellationToken);

        var exists = await visitorPasses
            .AnyAsync(pass => pass.Id == visitorPassId, cancellationToken);
        if (!exists)
        {
            return ServiceResult<PagedResult<VisitorAccessLogResponse>>.NotFound("Visitor pass was not found.");
        }

        var logs = dbContext.VisitorAccessLogs
            .AsNoTracking()
            .Include(log => log.GuardUser)
            .Where(log => log.VisitorPassId == visitorPassId)
            .OrderByDescending(log => log.CreatedAt);

        var totalCount = await logs.CountAsync(cancellationToken);
        var items = await logs
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(log => new VisitorAccessLogResponse(
                log.Id,
                log.VisitorPassId,
                log.GuardUserId,
                log.GuardUser == null ? null : log.GuardUser.FullName,
                log.Action,
                log.Notes,
                log.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<VisitorAccessLogResponse>>.Success(
            new PagedResult<VisitorAccessLogResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    private IQueryable<VisitorPass> GetVisitorPassDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.VisitorPasses
            .Include(pass => pass.ResidentProfile)
            .Include(pass => pass.Compound)
            .Include(pass => pass.PropertyUnit)
            .Include(pass => pass.AccessLogs)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private static IQueryable<VisitorPass> ApplyFilters(
        IQueryable<VisitorPass> visitorPasses,
        VisitorPassSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            visitorPasses = visitorPasses.Where(pass => pass.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            visitorPasses = visitorPasses.Where(pass => pass.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            visitorPasses = visitorPasses.Where(pass => pass.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.Status.HasValue)
        {
            visitorPasses = visitorPasses.Where(pass => pass.Status == query.Status.Value);
        }

        if (query.ValidFrom.HasValue)
        {
            visitorPasses = visitorPasses.Where(pass => pass.ValidUntil >= query.ValidFrom.Value);
        }

        if (query.ValidUntil.HasValue)
        {
            visitorPasses = visitorPasses.Where(pass => pass.ValidFrom <= query.ValidUntil.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            visitorPasses = visitorPasses.Where(pass =>
                pass.VisitorName.Contains(searchTerm)
                || pass.PropertyUnit.UnitNumber.Contains(searchTerm));
        }

        return visitorPasses;
    }

    private async Task<PagedResult<VisitorPassResponse>> ToPagedVisitorPassResultAsync(
        IQueryable<VisitorPass> query,
        VisitorPassSearchQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(pass => pass.CreatedAt)
            .ThenBy(pass => pass.VisitorName)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(ToVisitorPassProjection(includeAccessCode: false))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<VisitorPassResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<ServiceResult<VisitorPassResponse>> CancelVisitorPassAsync(
        VisitorPass visitorPass,
        CancellationToken cancellationToken)
    {
        if (visitorPass.Status is VisitorPassStatus.CheckedIn or VisitorPassStatus.CheckedOut)
        {
            return ServiceResult<VisitorPassResponse>.BadRequest("Checked visitor pass cannot be cancelled.");
        }

        if (visitorPass.Status == VisitorPassStatus.Cancelled)
        {
            return ServiceResult<VisitorPassResponse>.Conflict("Visitor pass is already cancelled.");
        }

        visitorPass.Status = VisitorPassStatus.Cancelled;
        visitorPass.CancelledAt = DateTime.UtcNow;
        visitorPass.UpdatedAt = DateTime.UtcNow;
        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<VisitorPassResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<VisitorPassResponse>.Success(ToVisitorPassResponse(visitorPass));
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
            return ServiceResult<T>.Conflict("The visitor pass was updated by another operation. Reload and try again.");
        }
    }
    private async Task<VisitorPass?> GetVisibleResidentPassAsync(
        Guid userId,
        Guid visitorPassId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        return await GetVisitorPassDetailsQuery(asNoTracking)
            .Where(pass => pass.Id == visitorPassId && pass.ResidentProfile.UserId == userId)
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

    private static Task<string> GenerateUniqueAccessCodeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"VP-{DateTime.UtcNow:yyyyMMdd}-{GenerateSecureCodeSegment(12)}");
    }

    private static string GenerateSecureCodeSegment(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }

    private static bool IsVisibleToGuard(VisitorPass visitorPass)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        return visitorPass.ValidFrom < tomorrowStart
            && visitorPass.ValidUntil >= todayStart
            && visitorPass.Status is VisitorPassStatus.Pending
                or VisitorPassStatus.Approved
                or VisitorPassStatus.CheckedIn;
    }

    private static ValidationFailure? ValidateCreateRequest(CreateVisitorPassRequest request)
    {
        if (request.PropertyUnitId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit id is required.");
        }

        if (TrimOrNull(request.VisitorName) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Visitor name is required.");
        }

        if (TrimOrNull(request.VisitorPhoneNumber) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Visitor phone number is required.");
        }

        if (TrimOrNull(request.VisitReason) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Visit reason is required.");
        }

        if (request.ValidUntil <= request.ValidFrom)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Valid until must be after valid from.");
        }

        if (request.ValidUntil <= DateTime.UtcNow)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Visitor pass validity must end in the future.");
        }

        return null;
    }

    private static VisitorPassResponse ToVisitorPassResponse(VisitorPass visitorPass, string? displayOnceAccessCode = null)
    {
        return new VisitorPassResponse(
            visitorPass.Id,
            visitorPass.ResidentProfileId,
            visitorPass.ResidentProfile.FullName,
            visitorPass.CompoundId,
            visitorPass.Compound.Name,
            visitorPass.PropertyUnitId,
            visitorPass.PropertyUnit.UnitNumber,
            visitorPass.VisitorName,
            visitorPass.VisitorPhoneNumber,
            visitorPass.VisitReason,
            displayOnceAccessCode ?? MaskedAccessCode,
            visitorPass.Status,
            visitorPass.ValidFrom,
            visitorPass.ValidUntil,
            visitorPass.CheckedInAt,
            visitorPass.CheckedOutAt,
            visitorPass.CreatedAt,
            visitorPass.UpdatedAt,
            visitorPass.CancelledAt,
            visitorPass.DenialReason);
    }

    private static Expression<Func<VisitorPass, VisitorPassResponse>> ToVisitorPassProjection(bool includeAccessCode = true)
    {
        return visitorPass => new VisitorPassResponse(
            visitorPass.Id,
            visitorPass.ResidentProfileId,
            visitorPass.ResidentProfile.FullName,
            visitorPass.CompoundId,
            visitorPass.Compound.Name,
            visitorPass.PropertyUnitId,
            visitorPass.PropertyUnit.UnitNumber,
            visitorPass.VisitorName,
            visitorPass.VisitorPhoneNumber,
            visitorPass.VisitReason,
            MaskedAccessCode,
            visitorPass.Status,
            visitorPass.ValidFrom,
            visitorPass.ValidUntil,
            visitorPass.CheckedInAt,
            visitorPass.CheckedOutAt,
            visitorPass.CreatedAt,
            visitorPass.UpdatedAt,
            visitorPass.CancelledAt,
            visitorPass.DenialReason);
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

    private async Task<IQueryable<VisitorPass>> ApplyCurrentCompoundScopeAsync(
        IQueryable<VisitorPass> visitorPasses,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return visitorPasses;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return visitorPasses.ApplyCompoundAccess(scope, pass => pass.CompoundId);
    }

    private async Task<IQueryable<VisitorPass>> ApplyGuardCompoundScopeAsync(
        IQueryable<VisitorPass> visitorPasses,
        Guid? guardUserId,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return visitorPasses;
        }

        if (!guardUserId.HasValue)
        {
            return visitorPasses.Where(_ => false);
        }

        var compoundIds = await compoundAccessService.GetAllowedCompoundIdsForUserRoleAsync(
            guardUserId.Value,
            UserRole.Guard,
            cancellationToken);

        return compoundIds.Length == 0
            ? visitorPasses.Where(_ => false)
            : visitorPasses.Where(pass => compoundIds.Contains(pass.CompoundId));
    }

    private async Task<bool> CanGuardAccessPassAsync(
        Guid? guardUserId,
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || (guardUserId.HasValue
                && await compoundAccessService.CanUserAccessCompoundAsync(
                    guardUserId.Value,
                    compoundId,
                    UserRole.Guard,
                    cancellationToken));
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}

