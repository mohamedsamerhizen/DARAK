using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Complaints;
using DARAK.Api.DTOs.Violations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ComplaintViolationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IComplaintViolationService
{
    public async Task<PagedResult<ComplaintResponse>> SearchComplaintsAdminAsync(
        ComplaintSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var complaints = await ApplyCurrentComplaintScopeAsync(
            ApplyComplaintFilters(GetComplaintDetailsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedComplaintResultAsync(complaints, query, cancellationToken);
    }

    public async Task<ServiceResult<ComplaintResponse>> GetComplaintAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var complaints = await ApplyCurrentComplaintScopeAsync(
            GetComplaintDetailsQuery(asNoTracking: true),
            cancellationToken);

        var complaint = await complaints
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return complaint is null
            ? ServiceResult<ComplaintResponse>.NotFound("Complaint was not found.")
            : ServiceResult<ComplaintResponse>.Success(ToComplaintResponse(complaint));
    }

    public async Task<ServiceResult<ComplaintResponse>> MarkComplaintUnderReviewAsync(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var complaints = await ApplyCurrentComplaintScopeAsync(
            GetComplaintDetailsQuery(asNoTracking: false),
            cancellationToken);

        var complaint = await complaints
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return complaint is null
            ? ServiceResult<ComplaintResponse>.NotFound("Complaint was not found.")
            : await UpdateComplaintStatusAsync(
                complaint,
                ComplaintStatus.UnderReview,
                request.AdminResponse,
                cancellationToken);
    }

    public async Task<ServiceResult<ComplaintResponse>> ResolveComplaintAsync(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var complaints = await ApplyCurrentComplaintScopeAsync(
            GetComplaintDetailsQuery(asNoTracking: false),
            cancellationToken);

        var complaint = await complaints
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return complaint is null
            ? ServiceResult<ComplaintResponse>.NotFound("Complaint was not found.")
            : await UpdateComplaintStatusAsync(
                complaint,
                ComplaintStatus.Resolved,
                request.AdminResponse,
                cancellationToken);
    }

    public async Task<ServiceResult<ComplaintResponse>> RejectComplaintAsync(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var complaints = await ApplyCurrentComplaintScopeAsync(
            GetComplaintDetailsQuery(asNoTracking: false),
            cancellationToken);

        var complaint = await complaints
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return complaint is null
            ? ServiceResult<ComplaintResponse>.NotFound("Complaint was not found.")
            : await UpdateComplaintStatusAsync(
                complaint,
                ComplaintStatus.Rejected,
                request.AdminResponse,
                cancellationToken);
    }

    public async Task<ServiceResult<ViolationResponse>> ConvertComplaintToViolationAsync(
        Guid id,
        Guid? createdByUserId,
        ConvertComplaintToViolationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateConvertComplaintRequest(request);
        if (validation is not null)
        {
            return ToResult<ViolationResponse>(validation);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var complaints = await ApplyCurrentComplaintScopeAsync(
            GetComplaintDetailsQuery(asNoTracking: false),
            cancellationToken);

        var complaint = await complaints
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (complaint is null)
        {
            return ServiceResult<ViolationResponse>.NotFound("Complaint was not found.");
        }

        if (complaint.Status is ComplaintStatus.Rejected or ComplaintStatus.ConvertedToViolation)
        {
            return ServiceResult<ViolationResponse>.BadRequest("Complaint cannot be converted in its current status.");
        }

        var violation = new Violation
        {
            CompoundId = complaint.CompoundId,
            ResidentProfileId = complaint.ResidentProfileId,
            PropertyUnitId = complaint.PropertyUnitId,
            ComplaintId = complaint.Id,
            ViolationType = request.ViolationType,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedByUserId = createdByUserId
        };

        complaint.Status = ComplaintStatus.ConvertedToViolation;
        complaint.UpdatedAt = DateTime.UtcNow;
        dbContext.Violations.Add(violation);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetViolationAsync(violation.Id, cancellationToken);
    }

    public async Task<ServiceResult<PagedResult<ComplaintResponse>>> SearchComplaintsResidentAsync(
        Guid userId,
        ComplaintSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return ServiceResult<PagedResult<ComplaintResponse>>.Success(
                new PagedResult<ComplaintResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var complaints = ApplyComplaintFilters(GetComplaintDetailsQuery(asNoTracking: true), query)
            .Where(complaint => profileIds.Contains(complaint.ResidentProfileId));

        return ServiceResult<PagedResult<ComplaintResponse>>.Success(
            await ToPagedComplaintResultAsync(complaints, query, cancellationToken));
    }

    public async Task<ServiceResult<ComplaintResponse>> GetComplaintResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var complaint = await GetComplaintDetailsQuery(asNoTracking: true)
            .Where(item => item.Id == id && item.ResidentProfile.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return complaint is null
            ? ServiceResult<ComplaintResponse>.NotFound("Complaint was not found.")
            : ServiceResult<ComplaintResponse>.Success(ToComplaintResponse(complaint));
    }

    public async Task<ServiceResult<ComplaintResponse>> CreateComplaintResidentAsync(
        Guid userId,
        CreateComplaintRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateComplaintRequest(request);
        if (validation is not null)
        {
            return ToResult<ComplaintResponse>(validation);
        }

        var scope = request.PropertyUnitId.HasValue
            ? await GetActiveResidentOccupancyForUnitAsync(userId, request.PropertyUnitId.Value, cancellationToken)
            : await GetFirstResidentProfileScopeAsync(userId, cancellationToken);

        if (scope is null)
        {
            return ServiceResult<ComplaintResponse>.NotFound("Active resident profile was not found.");
        }

        var complaint = new Complaint
        {
            ResidentProfileId = scope.ResidentProfileId,
            CompoundId = scope.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim()
        };

        dbContext.Complaints.Add(complaint);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetComplaintAdminAsync(complaint.Id, cancellationToken);
    }

    public async Task<PagedResult<ViolationResponse>> SearchViolationsAsync(
        ViolationSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var violations = await ApplyCurrentViolationScopeAsync(
            ApplyViolationFilters(GetViolationDetailsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedViolationResultAsync(violations, query, cancellationToken);
    }

    public async Task<ServiceResult<ViolationResponse>> GetViolationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var violations = await ApplyCurrentViolationScopeAsync(
            GetViolationDetailsQuery(asNoTracking: true),
            cancellationToken);

        var violation = await violations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return violation is null
            ? ServiceResult<ViolationResponse>.NotFound("Violation was not found.")
            : ServiceResult<ViolationResponse>.Success(ToViolationResponse(violation));
    }

    public async Task<ServiceResult<ViolationResponse>> CreateViolationAsync(
        Guid? createdByUserId,
        CreateViolationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateViolationRequest(request);
        if (validation is not null)
        {
            return ToResult<ViolationResponse>(validation);
        }

        var foundationValidation = await ValidateViolationFoundationAsync(
            request.CompoundId,
            request.ResidentProfileId,
            request.PropertyUnitId,
            cancellationToken);
        if (foundationValidation is not null)
        {
            return ToResult<ViolationResponse>(foundationValidation);
        }

        var violation = new Violation
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            PropertyUnitId = request.PropertyUnitId,
            ViolationType = request.ViolationType,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedByUserId = createdByUserId
        };

        dbContext.Violations.Add(violation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetViolationAsync(violation.Id, cancellationToken);
    }

    public async Task<PagedResult<ViolationFineResponse>> SearchViolationFinesAdminAsync(
        ViolationFineSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var fines = await ApplyCurrentViolationFineScopeAsync(
            ApplyViolationFineFilters(GetViolationFineDetailsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedViolationFineResultAsync(fines, query, cancellationToken);
    }

    public async Task<ServiceResult<ViolationFineResponse>> GetViolationFineAdminAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var fines = await ApplyCurrentViolationFineScopeAsync(
            GetViolationFineDetailsQuery(asNoTracking: true),
            cancellationToken);

        var fine = await fines
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return fine is null
            ? ServiceResult<ViolationFineResponse>.NotFound("Violation fine was not found.")
            : ServiceResult<ViolationFineResponse>.Success(ToViolationFineResponse(fine));
    }

    public async Task<ServiceResult<ViolationFineResponse>> CreateViolationFineAsync(
        CreateViolationFineRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateViolationFineRequest(request);
        if (validation is not null)
        {
            return ToResult<ViolationFineResponse>(validation);
        }

        var violations = await ApplyCurrentViolationScopeAsync(
            GetViolationDetailsQuery(asNoTracking: true),
            cancellationToken);

        var violation = await violations
            .FirstOrDefaultAsync(item => item.Id == request.ViolationId, cancellationToken);
        if (violation is null)
        {
            return ServiceResult<ViolationFineResponse>.NotFound("Violation was not found.");
        }

        var duplicateExists = await dbContext.ViolationFines.AnyAsync(
            fine => fine.ViolationId == request.ViolationId
                && fine.Status != ViolationFineStatus.Cancelled,
            cancellationToken);
        if (duplicateExists)
        {
            return ServiceResult<ViolationFineResponse>.Conflict(
                "A non-cancelled fine already exists for this violation.");
        }

        var fine = new ViolationFine
        {
            ViolationId = violation.Id,
            CompoundId = violation.CompoundId,
            ResidentProfileId = violation.ResidentProfileId,
            Amount = request.Amount,
            PaidAmount = 0m,
            Status = ViolationFineStatus.Unpaid,
            Reason = request.Reason.Trim(),
            DueDate = request.DueDate
        };

        dbContext.ViolationFines.Add(fine);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetViolationFineAdminAsync(fine.Id, cancellationToken);
    }

    public async Task<ServiceResult<ViolationFineResponse>> CancelViolationFineAsync(
        Guid id,
        CancelViolationFineRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<ViolationFineResponse>.BadRequest("Cancellation reason is required.");
        }

        var fines = await ApplyCurrentViolationFineScopeAsync(
            GetViolationFineDetailsQuery(asNoTracking: false),
            cancellationToken);

        var fine = await fines
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (fine is null)
        {
            return ServiceResult<ViolationFineResponse>.NotFound("Violation fine was not found.");
        }

        if (fine.Status != ViolationFineStatus.Unpaid || fine.PaidAmount > 0m)
        {
            return ServiceResult<ViolationFineResponse>.BadRequest("Only unpaid violation fines can be cancelled.");
        }

        fine.Status = ViolationFineStatus.Cancelled;
        fine.CancellationReason = reason;
        fine.CancelledAt = DateTime.UtcNow;
        fine.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ViolationFineResponse>.Success(ToViolationFineResponse(fine));
    }

    public async Task<ServiceResult<PagedResult<ViolationFineResponse>>> SearchViolationFinesResidentAsync(
        Guid userId,
        ViolationFineSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return ServiceResult<PagedResult<ViolationFineResponse>>.Success(
                new PagedResult<ViolationFineResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var fines = ApplyViolationFineFilters(GetViolationFineDetailsQuery(asNoTracking: true), query)
            .Where(fine => fine.ResidentProfileId.HasValue && profileIds.Contains(fine.ResidentProfileId.Value));

        return ServiceResult<PagedResult<ViolationFineResponse>>.Success(
            await ToPagedViolationFineResultAsync(fines, query, cancellationToken));
    }

    public async Task<ServiceResult<ViolationFineResponse>> GetViolationFineResidentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var fine = await GetViolationFineDetailsQuery(asNoTracking: true)
            .Where(item => item.Id == id
                && item.ResidentProfile != null
                && item.ResidentProfile.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return fine is null
            ? ServiceResult<ViolationFineResponse>.NotFound("Violation fine was not found.")
            : ServiceResult<ViolationFineResponse>.Success(ToViolationFineResponse(fine));
    }

    private IQueryable<Complaint> GetComplaintDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.Complaints
            .Include(complaint => complaint.ResidentProfile)
            .Include(complaint => complaint.Compound)
            .Include(complaint => complaint.PropertyUnit)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private IQueryable<Violation> GetViolationDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.Violations
            .Include(violation => violation.Compound)
            .Include(violation => violation.ResidentProfile)
            .Include(violation => violation.PropertyUnit)
            .Include(violation => violation.CreatedByUser)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private IQueryable<ViolationFine> GetViolationFineDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.ViolationFines
            .Include(fine => fine.Compound)
            .Include(fine => fine.ResidentProfile)
            .Include(fine => fine.Violation)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private static IQueryable<Complaint> ApplyComplaintFilters(
        IQueryable<Complaint> complaints,
        ComplaintSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            complaints = complaints.Where(complaint => complaint.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            complaints = complaints.Where(complaint => complaint.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            complaints = complaints.Where(complaint => complaint.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.Status.HasValue)
        {
            complaints = complaints.Where(complaint => complaint.Status == query.Status.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            complaints = complaints.Where(complaint =>
                complaint.Title.Contains(searchTerm)
                || complaint.Description.Contains(searchTerm)
                || complaint.ResidentProfile.FullName.Contains(searchTerm));
        }

        return complaints;
    }

    private static IQueryable<Violation> ApplyViolationFilters(
        IQueryable<Violation> violations,
        ViolationSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            violations = violations.Where(violation => violation.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            violations = violations.Where(violation => violation.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            violations = violations.Where(violation => violation.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.ComplaintId.HasValue)
        {
            violations = violations.Where(violation => violation.ComplaintId == query.ComplaintId.Value);
        }

        if (query.ViolationType.HasValue)
        {
            violations = violations.Where(violation => violation.ViolationType == query.ViolationType.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            violations = violations.Where(violation =>
                violation.Title.Contains(searchTerm)
                || violation.Description.Contains(searchTerm)
                || (violation.ResidentProfile != null && violation.ResidentProfile.FullName.Contains(searchTerm)));
        }

        return violations;
    }

    private static IQueryable<ViolationFine> ApplyViolationFineFilters(
        IQueryable<ViolationFine> fines,
        ViolationFineSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            fines = fines.Where(fine => fine.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            fines = fines.Where(fine => fine.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.ViolationId.HasValue)
        {
            fines = fines.Where(fine => fine.ViolationId == query.ViolationId.Value);
        }

        if (query.Status.HasValue)
        {
            fines = fines.Where(fine => fine.Status == query.Status.Value);
        }

        if (query.DueBefore.HasValue)
        {
            fines = fines.Where(fine => fine.DueDate <= query.DueBefore.Value);
        }

        if (query.DueAfter.HasValue)
        {
            fines = fines.Where(fine => fine.DueDate >= query.DueAfter.Value);
        }

        return fines;
    }

    private async Task<PagedResult<ComplaintResponse>> ToPagedComplaintResultAsync(
        IQueryable<Complaint> query,
        ComplaintSearchQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(complaint => complaint.CreatedAt)
            .ThenBy(complaint => complaint.Title)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(ToComplaintProjection())
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ComplaintResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<ViolationResponse>> ToPagedViolationResultAsync(
        IQueryable<Violation> query,
        ViolationSearchQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(violation => violation.CreatedAt)
            .ThenBy(violation => violation.Title)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(ToViolationProjection())
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ViolationResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<ViolationFineResponse>> ToPagedViolationFineResultAsync(
        IQueryable<ViolationFine> query,
        ViolationFineSearchQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(fine => fine.DueDate)
            .ThenByDescending(fine => fine.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(ToViolationFineProjection())
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ViolationFineResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<ServiceResult<ComplaintResponse>> UpdateComplaintStatusAsync(
        Complaint complaint,
        ComplaintStatus newStatus,
        string adminResponse,
        CancellationToken cancellationToken)
    {
        var response = TrimOrNull(adminResponse);
        if (response is null)
        {
            return ServiceResult<ComplaintResponse>.BadRequest("Admin response is required.");
        }

        if (!CanChangeComplaintStatus(complaint.Status, newStatus))
        {
            return ServiceResult<ComplaintResponse>.BadRequest(
                $"Cannot change complaint from {complaint.Status} to {newStatus}.");
        }

        var now = DateTime.UtcNow;
        complaint.Status = newStatus;
        complaint.AdminResponse = response;
        complaint.UpdatedAt = now;

        if (newStatus == ComplaintStatus.Resolved)
        {
            complaint.ResolvedAt = now;
        }
        else if (newStatus == ComplaintStatus.Rejected)
        {
            complaint.RejectedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ComplaintResponse>.Success(ToComplaintResponse(complaint));
    }

    private static bool CanChangeComplaintStatus(ComplaintStatus currentStatus, ComplaintStatus newStatus)
    {
        return currentStatus switch
        {
            ComplaintStatus.Open => newStatus is ComplaintStatus.UnderReview
                or ComplaintStatus.Resolved
                or ComplaintStatus.Rejected,
            ComplaintStatus.UnderReview => newStatus is ComplaintStatus.Resolved
                or ComplaintStatus.Rejected,
            _ => false
        };
    }

    private async Task<ValidationFailure?> ValidateViolationFoundationAsync(
        Guid compoundId,
        Guid? residentProfileId,
        Guid? propertyUnitId,
        CancellationToken cancellationToken)
    {
        if (compoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == compoundId, cancellationToken);
        if (!compoundExists)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken))
        {
            return new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.");
        }

        if (residentProfileId.HasValue)
        {
            var residentMatches = await dbContext.ResidentProfiles
                .AsNoTracking()
                .AnyAsync(profile =>
                    profile.Id == residentProfileId.Value
                    && profile.CompoundId == compoundId,
                    cancellationToken);
            if (!residentMatches)
            {
                return new ValidationFailure(
                    ServiceResultStatus.BadRequest,
                    "Resident profile must belong to the violation compound.");
            }
        }

        if (propertyUnitId.HasValue)
        {
            var unitMatches = await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(unit =>
                    unit.Id == propertyUnitId.Value
                    && unit.CompoundId == compoundId,
                    cancellationToken);
            if (!unitMatches)
            {
                return new ValidationFailure(
                    ServiceResultStatus.BadRequest,
                    "Property unit must belong to the violation compound.");
            }
        }

        return null;
    }

    private async Task<ResidentScope?> GetActiveResidentOccupancyForUnitAsync(
        Guid userId,
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.PropertyUnitId == propertyUnitId
                && record.ResidentProfile.UserId == userId
                && record.ResidentProfile.IsActive
                && record.OccupancyStatus == OccupancyStatus.Active)
            .Select(record => new ResidentScope(
                record.ResidentProfileId,
                record.CompoundId,
                record.PropertyUnitId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ResidentScope?> GetFirstResidentProfileScopeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .OrderBy(profile => profile.CreatedAt)
            .Select(profile => new ResidentScope(profile.Id, profile.CompoundId, null))
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

    private static ValidationFailure? ValidateCreateComplaintRequest(CreateComplaintRequest request)
    {
        if (TrimOrNull(request.Title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Complaint title is required.");
        }

        if (TrimOrNull(request.Description) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Complaint description is required.");
        }

        return null;
    }

    private static ValidationFailure? ValidateConvertComplaintRequest(ConvertComplaintToViolationRequest request)
    {
        if (TrimOrNull(request.Title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Violation title is required.");
        }

        if (TrimOrNull(request.Description) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Violation description is required.");
        }

        return null;
    }

    private static ValidationFailure? ValidateCreateViolationRequest(CreateViolationRequest request)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        if (TrimOrNull(request.Title) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Violation title is required.");
        }

        if (TrimOrNull(request.Description) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Violation description is required.");
        }

        return null;
    }

    private static ValidationFailure? ValidateCreateViolationFineRequest(CreateViolationFineRequest request)
    {
        if (request.ViolationId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Violation id is required.");
        }

        if (request.Amount <= 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Fine amount must be greater than zero.");
        }

        if (TrimOrNull(request.Reason) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Fine reason is required.");
        }

        if (request.DueDate == default)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Fine due date is required.");
        }

        return null;
    }

    private static ComplaintResponse ToComplaintResponse(Complaint complaint)
    {
        return new ComplaintResponse(
            complaint.Id,
            complaint.ResidentProfileId,
            complaint.ResidentProfile.FullName,
            complaint.CompoundId,
            complaint.Compound.Name,
            complaint.PropertyUnitId,
            complaint.PropertyUnit?.UnitNumber,
            complaint.Title,
            complaint.Description,
            complaint.Status,
            complaint.AdminResponse,
            complaint.CreatedAt,
            complaint.UpdatedAt,
            complaint.ResolvedAt,
            complaint.RejectedAt);
    }

    private static ViolationResponse ToViolationResponse(Violation violation)
    {
        return new ViolationResponse(
            violation.Id,
            violation.CompoundId,
            violation.Compound.Name,
            violation.ResidentProfileId,
            violation.ResidentProfile?.FullName,
            violation.PropertyUnitId,
            violation.PropertyUnit?.UnitNumber,
            violation.ComplaintId,
            violation.ViolationType,
            violation.Title,
            violation.Description,
            violation.CreatedByUserId,
            violation.CreatedByUser?.FullName,
            violation.CreatedAt,
            violation.UpdatedAt);
    }

    private static ViolationFineResponse ToViolationFineResponse(ViolationFine fine)
    {
        return new ViolationFineResponse(
            fine.Id,
            fine.ViolationId,
            fine.CompoundId,
            fine.Compound.Name,
            fine.ResidentProfileId,
            fine.ResidentProfile?.FullName,
            fine.Amount,
            fine.PaidAmount,
            Math.Max(0m, fine.Amount - fine.PaidAmount),
            fine.Status,
            fine.Reason,
            fine.DueDate,
            fine.CreatedAt,
            fine.UpdatedAt,
            fine.CancelledAt,
            fine.CancellationReason);
    }

    private static Expression<Func<Complaint, ComplaintResponse>> ToComplaintProjection()
    {
        return complaint => new ComplaintResponse(
            complaint.Id,
            complaint.ResidentProfileId,
            complaint.ResidentProfile.FullName,
            complaint.CompoundId,
            complaint.Compound.Name,
            complaint.PropertyUnitId,
            complaint.PropertyUnit == null ? null : complaint.PropertyUnit.UnitNumber,
            complaint.Title,
            complaint.Description,
            complaint.Status,
            complaint.AdminResponse,
            complaint.CreatedAt,
            complaint.UpdatedAt,
            complaint.ResolvedAt,
            complaint.RejectedAt);
    }

    private static Expression<Func<Violation, ViolationResponse>> ToViolationProjection()
    {
        return violation => new ViolationResponse(
            violation.Id,
            violation.CompoundId,
            violation.Compound.Name,
            violation.ResidentProfileId,
            violation.ResidentProfile == null ? null : violation.ResidentProfile.FullName,
            violation.PropertyUnitId,
            violation.PropertyUnit == null ? null : violation.PropertyUnit.UnitNumber,
            violation.ComplaintId,
            violation.ViolationType,
            violation.Title,
            violation.Description,
            violation.CreatedByUserId,
            violation.CreatedByUser == null ? null : violation.CreatedByUser.FullName,
            violation.CreatedAt,
            violation.UpdatedAt);
    }

    private static Expression<Func<ViolationFine, ViolationFineResponse>> ToViolationFineProjection()
    {
        return fine => new ViolationFineResponse(
            fine.Id,
            fine.ViolationId,
            fine.CompoundId,
            fine.Compound.Name,
            fine.ResidentProfileId,
            fine.ResidentProfile == null ? null : fine.ResidentProfile.FullName,
            fine.Amount,
            fine.PaidAmount,
            fine.Amount > fine.PaidAmount ? fine.Amount - fine.PaidAmount : 0m,
            fine.Status,
            fine.Reason,
            fine.DueDate,
            fine.CreatedAt,
            fine.UpdatedAt,
            fine.CancelledAt,
            fine.CancellationReason);
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

    private async Task<IQueryable<Complaint>> ApplyCurrentComplaintScopeAsync(
        IQueryable<Complaint> complaints,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return complaints;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return complaints.ApplyCompoundAccess(scope, complaint => complaint.CompoundId);
    }

    private async Task<IQueryable<Violation>> ApplyCurrentViolationScopeAsync(
        IQueryable<Violation> violations,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return violations;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return violations.ApplyCompoundAccess(scope, violation => violation.CompoundId);
    }

    private async Task<IQueryable<ViolationFine>> ApplyCurrentViolationFineScopeAsync(
        IQueryable<ViolationFine> fines,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return fines;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return fines.ApplyCompoundAccess(scope, fine => fine.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);

    private sealed record ResidentScope(Guid ResidentProfileId, Guid CompoundId, Guid? PropertyUnitId);
}
