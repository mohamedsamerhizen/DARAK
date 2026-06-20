using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class StaffMemberService(ApplicationDbContext dbContext)
    : IStaffMemberService
{
    private const int MaxNameLength = 150;
    private const int MaxPhoneLength = 30;
    private const int MaxEmailLength = 256;
    private const int MaxSpecializationLength = 150;
    private const int MaxNationalIdLength = 50;
    private const int MaxNoteLength = 1000;

    public async Task<PagedResult<StaffMemberResponse>> SearchStaffMembersAsync(
        StaffMemberQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var staffMembers = ApplyStaffFilters(dbContext.StaffMembers.AsNoTracking(), query);
        var totalCount = await staffMembers.CountAsync(cancellationToken);
        var items = await staffMembers
            .OrderBy(staffMember => staffMember.FullName)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(staffMember => new StaffMemberResponse(
                staffMember.Id,
                staffMember.FullName,
                staffMember.PhoneNumber,
                staffMember.Email,
                staffMember.StaffType,
                staffMember.Status,
                staffMember.Specialization,
                null,
                null,
                staffMember.UserId,
                staffMember.CreatedAtUtc,
                staffMember.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<StaffMemberResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<ServiceResult<StaffMemberResponse>> GetStaffMemberAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var staffMember = await dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return staffMember is null
            ? ServiceResult<StaffMemberResponse>.NotFound("Staff member was not found.")
            : ServiceResult<StaffMemberResponse>.Success(ToStaffMemberResponse(staffMember));
    }

    public async Task<ServiceResult<StaffMemberResponse>> CreateStaffMemberAsync(
        CreateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateStaffMemberRequestAsync(
            request.FullName,
            request.PhoneNumber,
            request.Email,
            request.StaffType,
            request.Status,
            request.Specialization,
            request.NationalId,
            request.Notes,
            request.UserId,
            null,
            cancellationToken);
        if (validation is not null)
        {
            return ToResult<StaffMemberResponse>(validation);
        }

        var staffMember = new StaffMember
        {
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = TrimOrNull(request.Email),
            StaffType = request.StaffType,
            Status = request.Status,
            Specialization = TrimOrNull(request.Specialization),
            NationalId = TrimOrNull(request.NationalId),
            Notes = TrimOrNull(request.Notes),
            UserId = request.UserId
        };

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<StaffMemberResponse>.Success(ToStaffMemberResponse(staffMember));
    }

    public async Task<ServiceResult<StaffMemberResponse>> UpdateStaffMemberAsync(
        Guid id,
        UpdateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var staffMember = await dbContext.StaffMembers
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (staffMember is null)
        {
            return ServiceResult<StaffMemberResponse>.NotFound("Staff member was not found.");
        }

        var validation = await ValidateStaffMemberRequestAsync(
            request.FullName,
            request.PhoneNumber,
            request.Email,
            request.StaffType,
            request.Status,
            request.Specialization,
            request.NationalId,
            request.Notes,
            request.UserId,
            id,
            cancellationToken);
        if (validation is not null)
        {
            return ToResult<StaffMemberResponse>(validation);
        }

        staffMember.FullName = request.FullName.Trim();
        staffMember.PhoneNumber = request.PhoneNumber.Trim();
        staffMember.Email = TrimOrNull(request.Email);
        staffMember.StaffType = request.StaffType;
        staffMember.Status = request.Status;
        staffMember.Specialization = TrimOrNull(request.Specialization);
        staffMember.NationalId = TrimOrNull(request.NationalId);
        staffMember.Notes = TrimOrNull(request.Notes);
        staffMember.UserId = request.UserId;
        staffMember.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<StaffMemberResponse>.Success(ToStaffMemberResponse(staffMember));
    }

    public async Task<ServiceResult<StaffMemberResponse>> SetStaffMemberStatusAsync(
        Guid id,
        StaffStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            return ServiceResult<StaffMemberResponse>.BadRequest("Staff status is invalid.");
        }

        var staffMember = await dbContext.StaffMembers
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (staffMember is null)
        {
            return ServiceResult<StaffMemberResponse>.NotFound("Staff member was not found.");
        }

        staffMember.Status = status;
        staffMember.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<StaffMemberResponse>.Success(ToStaffMemberResponse(staffMember));
    }

    private static IQueryable<StaffMember> ApplyStaffFilters(
        IQueryable<StaffMember> query,
        StaffMemberQueryRequest request)
    {
        if (request.StaffType.HasValue)
        {
            query = query.Where(item => item.StaffType == request.StaffType.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        var searchTerm = TrimOrNull(request.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item => item.FullName.Contains(searchTerm)
                || item.PhoneNumber.Contains(searchTerm)
                || (item.Email != null && item.Email.Contains(searchTerm))
                || (item.Specialization != null && item.Specialization.Contains(searchTerm))
                || (item.NationalId != null && item.NationalId.Contains(searchTerm)));
        }

        return query;
    }

    private async Task<ValidationFailure?> ValidateStaffMemberRequestAsync(
        string fullName,
        string phoneNumber,
        string? email,
        StaffType staffType,
        StaffStatus status,
        string? specialization,
        string? nationalId,
        string? notes,
        Guid? userId,
        Guid? currentStaffMemberId,
        CancellationToken cancellationToken)
    {
        if (TrimOrNull(fullName) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff full name is required.");
        }

        if (fullName.Trim().Length > MaxNameLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff full name is too long.");
        }

        if (TrimOrNull(phoneNumber) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff phone number is required.");
        }

        if (phoneNumber.Trim().Length > MaxPhoneLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff phone number is too long.");
        }

        if (!Enum.IsDefined(staffType))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff type is invalid.");
        }

        if (!Enum.IsDefined(status))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff status is invalid.");
        }

        if (TrimOrNull(email)?.Length > MaxEmailLength
            || TrimOrNull(specialization)?.Length > MaxSpecializationLength
            || TrimOrNull(nationalId)?.Length > MaxNationalIdLength
            || TrimOrNull(notes)?.Length > MaxNoteLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Staff metadata contains a value that is too long.");
        }

        if (userId.HasValue)
        {
            var userExists = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId.Value, cancellationToken);
            if (!userExists)
            {
                return new ValidationFailure(ServiceResultStatus.NotFound, "Linked user was not found.");
            }

            var userAlreadyLinked = await dbContext.StaffMembers
                .AsNoTracking()
                .AnyAsync(staffMember => staffMember.UserId == userId.Value
                    && (!currentStaffMemberId.HasValue || staffMember.Id != currentStaffMemberId.Value),
                    cancellationToken);
            if (userAlreadyLinked)
            {
                return new ValidationFailure(ServiceResultStatus.Conflict, "Linked user is already assigned to another staff member.");
            }
        }

        return null;
    }

    private static StaffMemberResponse ToStaffMemberResponse(StaffMember staffMember)
    {
        return new StaffMemberResponse(
            staffMember.Id,
            staffMember.FullName,
            staffMember.PhoneNumber,
            staffMember.Email,
            staffMember.StaffType,
            staffMember.Status,
            staffMember.Specialization,
            staffMember.NationalId,
            staffMember.Notes,
            staffMember.UserId,
            staffMember.CreatedAtUtc,
            staffMember.UpdatedAtUtc);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
