using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Identity;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class UserCompoundAssignmentService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager)
    : IUserCompoundAssignmentService
{
    private static readonly UserRole[] AssignableRoles =
    [
        UserRole.CompoundAdmin,
        UserRole.Accountant,
        UserRole.Guard
    ];

    public async Task<PagedResult<UserCompoundAssignmentResponse>> SearchAsync(
        UserCompoundAssignmentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var assignments = ApplyFilters(GetDetailsQuery(asNoTracking: true), query);
        var totalCount = await assignments.CountAsync(cancellationToken);
        var items = await assignments
            .OrderBy(assignment => assignment.Compound.Name)
            .ThenBy(assignment => assignment.Role)
            .ThenBy(assignment => assignment.User.FullName)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(assignment => ToResponse(assignment))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<UserCompoundAssignmentResponse>(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<UserCompoundAssignmentResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var assignment = await GetDetailsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return assignment is null
            ? ServiceResult<UserCompoundAssignmentResponse>.NotFound("Compound assignment was not found.")
            : ServiceResult<UserCompoundAssignmentResponse>.Success(ToResponse(assignment));
    }

    public async Task<ServiceResult<UserCompoundAssignmentResponse>> CreateAsync(
        Guid? createdByUserId,
        CreateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateCreateAsync(request, cancellationToken);
        if (validation is not null)
        {
            return ToResult<UserCompoundAssignmentResponse>(validation);
        }

        var duplicateActive = await dbContext.UserCompoundAssignments.AnyAsync(
            assignment =>
                assignment.UserId == request.UserId
                && assignment.CompoundId == request.CompoundId
                && assignment.Role == request.Role
                && assignment.IsActive,
            cancellationToken);
        if (duplicateActive)
        {
            return ServiceResult<UserCompoundAssignmentResponse>.Conflict(
                "An active assignment already exists for this user, compound, and role.");
        }

        var assignment = new UserCompoundAssignment
        {
            UserId = request.UserId,
            CompoundId = request.CompoundId,
            Role = request.Role,
            CreatedByUserId = createdByUserId
        };

        dbContext.UserCompoundAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(assignment.Id, cancellationToken);
    }

    public async Task<ServiceResult<UserCompoundAssignmentResponse>> UpdateAsync(
        Guid id,
        UpdateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var assignment = await GetDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment is null)
        {
            return ServiceResult<UserCompoundAssignmentResponse>.NotFound("Compound assignment was not found.");
        }

        if (request.IsActive)
        {
            var duplicateActive = await dbContext.UserCompoundAssignments.AnyAsync(
                item =>
                    item.Id != id
                    && item.UserId == assignment.UserId
                    && item.CompoundId == assignment.CompoundId
                    && item.Role == assignment.Role
                    && item.IsActive,
                cancellationToken);
            if (duplicateActive)
            {
                return ServiceResult<UserCompoundAssignmentResponse>.Conflict(
                    "Another active assignment already exists for this user, compound, and role.");
            }
        }

        assignment.IsActive = request.IsActive;
        assignment.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserCompoundAssignmentResponse>.Success(ToResponse(assignment));
    }

    public async Task<ServiceResult<object?>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.UserCompoundAssignments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment is null)
        {
            return ServiceResult<object?>.NotFound("Compound assignment was not found.");
        }

        assignment.IsActive = false;
        assignment.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    private IQueryable<UserCompoundAssignment> GetDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.UserCompoundAssignments
            .Include(assignment => assignment.User)
            .Include(assignment => assignment.Compound)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private static IQueryable<UserCompoundAssignment> ApplyFilters(
        IQueryable<UserCompoundAssignment> assignments,
        UserCompoundAssignmentSearchQuery query)
    {
        if (query.UserId.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.UserId == query.UserId.Value);
        }

        if (query.CompoundId.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.CompoundId == query.CompoundId.Value);
        }

        if (query.Role.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.Role == query.Role.Value);
        }

        if (query.IsActive.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.IsActive == query.IsActive.Value);
        }

        return assignments;
    }

    private async Task<ValidationFailure?> ValidateCreateAsync(
        CreateUserCompoundAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "User id is required.");
        }

        if (request.CompoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        if (!AssignableRoles.Contains(request.Role))
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Only CompoundAdmin, Accountant, and Guard can be assigned to compounds.");
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "User was not found.");
        }

        if (!await userManager.IsInRoleAsync(user, request.Role.ToString()))
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "User must have the assigned role before being assigned to a compound.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == request.CompoundId && compound.IsActive, cancellationToken);
        if (!compoundExists)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Active compound was not found.");
        }

        return null;
    }

    private static UserCompoundAssignmentResponse ToResponse(UserCompoundAssignment assignment)
    {
        return new UserCompoundAssignmentResponse(
            assignment.Id,
            assignment.UserId,
            assignment.User.Email,
            assignment.User.FullName,
            assignment.CompoundId,
            assignment.Compound.Name,
            assignment.Role,
            assignment.IsActive,
            assignment.CreatedAtUtc,
            assignment.UpdatedAtUtc,
            assignment.CreatedByUserId);
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

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
