using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.EmergencyContacts;
using DARAK.Api.DTOs.FamilyMembers;
using DARAK.Api.DTOs.Residents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ICompoundAccessService? compoundAccessService = null,
    ICurrentUserService? currentUserService = null)
    : IResidentService
{
    public async Task<PagedResult<ResidentProfileResponse>> SearchResidentProfilesAsync(
        ResidentProfileSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var residentProfiles = await ApplyCurrentResidentProfileScopeAsync(
            dbContext.ResidentProfiles.AsNoTracking(),
            cancellationToken);

        if (query.CompoundId.HasValue)
        {
            residentProfiles = residentProfiles.Where(profile => profile.CompoundId == query.CompoundId.Value);
        }

        if (query.IsActive.HasValue)
        {
            residentProfiles = residentProfiles.Where(profile => profile.IsActive == query.IsActive.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            residentProfiles = residentProfiles.Where(profile =>
                profile.FullName.Contains(searchTerm)
                || (profile.PhoneNumber != null && profile.PhoneNumber.Contains(searchTerm))
                || (profile.NationalId != null && profile.NationalId.Contains(searchTerm)));
        }

        return await ToPagedResultAsync(
            residentProfiles.OrderBy(profile => profile.FullName),
            query,
            profile => new ResidentProfileResponse(
                profile.Id,
                profile.UserId,
                profile.CompoundId,
                profile.Compound.Name,
                profile.FullName,
                profile.NationalId,
                profile.PhoneNumber,
                profile.AlternativePhoneNumber,
                profile.DateOfBirth,
                profile.IsActive,
                profile.CreatedAt,
                profile.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<ResidentProfileResponse>> GetResidentProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var residentProfiles = await ApplyCurrentResidentProfileScopeAsync(
            dbContext.ResidentProfiles
                .AsNoTracking()
                .Include(profile => profile.Compound),
            cancellationToken);

        var residentProfile = await residentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

        return residentProfile is null
            ? ServiceResult<ResidentProfileResponse>.NotFound("Resident profile was not found.")
            : ServiceResult<ResidentProfileResponse>.Success(ToResidentProfileResponse(residentProfile));
    }

    public async Task<ServiceResult<ResidentProfileResponse>> GetResidentProfileForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var residentProfile = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Include(profile => profile.Compound)
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .OrderBy(profile => profile.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return residentProfile is null
            ? ServiceResult<ResidentProfileResponse>.NotFound("Resident profile was not found.")
            : ServiceResult<ResidentProfileResponse>.Success(ToResidentProfileResponse(residentProfile));
    }

    public async Task<ServiceResult<ResidentProfileResponse>> CreateResidentProfileAsync(
        CreateResidentProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return ServiceResult<ResidentProfileResponse>.NotFound("User was not found.");
        }

        if (!await userManager.IsInRoleAsync(user, nameof(UserRole.Resident)))
        {
            return ServiceResult<ResidentProfileResponse>.BadRequest("User must have the Resident role.");
        }

        var compound = await dbContext.Compounds
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.CompoundId, cancellationToken);

        if (compound is null)
        {
            return ServiceResult<ResidentProfileResponse>.NotFound("Compound was not found.");
        }

        if (!compound.IsActive)
        {
            return ServiceResult<ResidentProfileResponse>.BadRequest("Compound is inactive.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentProfileResponse>.Forbidden("Current user cannot access this compound.");
        }

        var duplicateExists = await dbContext.ResidentProfiles.AnyAsync(
            profile => profile.UserId == request.UserId && profile.CompoundId == request.CompoundId,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<ResidentProfileResponse>.Conflict(
                "Resident profile already exists for this user and compound.");
        }

        var residentProfile = new ResidentProfile
        {
            UserId = request.UserId,
            CompoundId = request.CompoundId,
            FullName = request.FullName.Trim(),
            NationalId = TrimOrNull(request.NationalId),
            PhoneNumber = TrimOrNull(request.PhoneNumber),
            AlternativePhoneNumber = TrimOrNull(request.AlternativePhoneNumber),
            DateOfBirth = request.DateOfBirth,
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.ResidentProfiles.Add(residentProfile);
        await dbContext.SaveChangesAsync(cancellationToken);

        residentProfile.Compound = compound;
        return ServiceResult<ResidentProfileResponse>.Success(ToResidentProfileResponse(residentProfile));
    }

    public async Task<ServiceResult<ResidentProfileResponse>> UpdateResidentProfileAsync(
        Guid id,
        UpdateResidentProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var residentProfile = await dbContext.ResidentProfiles
            .Include(profile => profile.Compound)
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

        if (residentProfile is null)
        {
            return ServiceResult<ResidentProfileResponse>.NotFound("Resident profile was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(residentProfile.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentProfileResponse>.NotFound("Resident profile was not found.");
        }

        residentProfile.FullName = request.FullName.Trim();
        residentProfile.NationalId = TrimOrNull(request.NationalId);
        residentProfile.PhoneNumber = TrimOrNull(request.PhoneNumber);
        residentProfile.AlternativePhoneNumber = TrimOrNull(request.AlternativePhoneNumber);
        residentProfile.DateOfBirth = request.DateOfBirth;
        residentProfile.Notes = TrimOrNull(request.Notes);
        residentProfile.IsActive = request.IsActive;
        residentProfile.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ResidentProfileResponse>.Success(ToResidentProfileResponse(residentProfile));
    }

    public async Task<ServiceResult<object?>> DeactivateResidentProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var residentProfile = await dbContext.ResidentProfiles
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

        if (residentProfile is null)
        {
            return ServiceResult<object?>.NotFound("Resident profile was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(residentProfile.CompoundId, cancellationToken))
        {
            return ServiceResult<object?>.NotFound("Resident profile was not found.");
        }

        residentProfile.IsActive = false;
        residentProfile.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<ServiceResult<IReadOnlyCollection<FamilyMemberResponse>>> GetFamilyMembersAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default)
    {
        if (!await ResidentProfileExistsAsync(residentProfileId, cancellationToken))
        {
            return ServiceResult<IReadOnlyCollection<FamilyMemberResponse>>.NotFound(
                "Resident profile was not found.");
        }

        var familyMembers = await dbContext.FamilyMembers
            .AsNoTracking()
            .Where(member => member.ResidentProfileId == residentProfileId)
            .OrderBy(member => member.FullName)
            .Select(member => new FamilyMemberResponse(
                member.Id,
                member.ResidentProfileId,
                member.FullName,
                member.Relationship,
                member.DateOfBirth,
                member.PhoneNumber,
                member.IsActive,
                member.CreatedAt,
                member.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<FamilyMemberResponse>>.Success(familyMembers);
    }

    public async Task<ServiceResult<FamilyMemberResponse>> AddFamilyMemberAsync(
        Guid residentProfileId,
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var profileValidation = await ValidateActiveResidentProfileAsync(residentProfileId, cancellationToken);
        if (profileValidation is not null)
        {
            return ToResult<FamilyMemberResponse>(profileValidation);
        }

        var familyMember = new FamilyMember
        {
            ResidentProfileId = residentProfileId,
            FullName = request.FullName.Trim(),
            Relationship = request.Relationship.Trim(),
            DateOfBirth = request.DateOfBirth,
            PhoneNumber = TrimOrNull(request.PhoneNumber)
        };

        dbContext.FamilyMembers.Add(familyMember);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FamilyMemberResponse>.Success(ToFamilyMemberResponse(familyMember));
    }

    public async Task<ServiceResult<FamilyMemberResponse>> UpdateFamilyMemberAsync(
        Guid residentProfileId,
        Guid familyMemberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var profileValidation = await ValidateActiveResidentProfileAsync(residentProfileId, cancellationToken);
        if (profileValidation is not null)
        {
            return ToResult<FamilyMemberResponse>(profileValidation);
        }

        var familyMember = await dbContext.FamilyMembers.FirstOrDefaultAsync(
            member => member.Id == familyMemberId && member.ResidentProfileId == residentProfileId,
            cancellationToken);

        if (familyMember is null)
        {
            return ServiceResult<FamilyMemberResponse>.NotFound("Family member was not found.");
        }

        familyMember.FullName = request.FullName.Trim();
        familyMember.Relationship = request.Relationship.Trim();
        familyMember.DateOfBirth = request.DateOfBirth;
        familyMember.PhoneNumber = TrimOrNull(request.PhoneNumber);
        familyMember.IsActive = request.IsActive;
        familyMember.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FamilyMemberResponse>.Success(ToFamilyMemberResponse(familyMember));
    }

    public async Task<ServiceResult<object?>> DeactivateFamilyMemberAsync(
        Guid residentProfileId,
        Guid familyMemberId,
        CancellationToken cancellationToken = default)
    {
        var profileValidation = await ValidateActiveResidentProfileAsync(residentProfileId, cancellationToken);
        if (profileValidation is not null)
        {
            return ToResult<object?>(profileValidation);
        }

        var familyMember = await dbContext.FamilyMembers.FirstOrDefaultAsync(
            member => member.Id == familyMemberId && member.ResidentProfileId == residentProfileId,
            cancellationToken);

        if (familyMember is null)
        {
            return ServiceResult<object?>.NotFound("Family member was not found.");
        }

        familyMember.IsActive = false;
        familyMember.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<ServiceResult<IReadOnlyCollection<EmergencyContactResponse>>> GetEmergencyContactsAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default)
    {
        if (!await ResidentProfileExistsAsync(residentProfileId, cancellationToken))
        {
            return ServiceResult<IReadOnlyCollection<EmergencyContactResponse>>.NotFound(
                "Resident profile was not found.");
        }

        var emergencyContacts = await dbContext.EmergencyContacts
            .AsNoTracking()
            .Where(contact => contact.ResidentProfileId == residentProfileId)
            .OrderBy(contact => contact.FullName)
            .Select(contact => new EmergencyContactResponse(
                contact.Id,
                contact.ResidentProfileId,
                contact.FullName,
                contact.Relationship,
                contact.PhoneNumber,
                contact.IsActive,
                contact.CreatedAt,
                contact.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<EmergencyContactResponse>>.Success(emergencyContacts);
    }

    public async Task<ServiceResult<EmergencyContactResponse>> AddEmergencyContactAsync(
        Guid residentProfileId,
        CreateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var profileValidation = await ValidateActiveResidentProfileAsync(residentProfileId, cancellationToken);
        if (profileValidation is not null)
        {
            return ToResult<EmergencyContactResponse>(profileValidation);
        }

        var emergencyContact = new EmergencyContact
        {
            ResidentProfileId = residentProfileId,
            FullName = request.FullName.Trim(),
            Relationship = request.Relationship.Trim(),
            PhoneNumber = request.PhoneNumber.Trim()
        };

        dbContext.EmergencyContacts.Add(emergencyContact);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<EmergencyContactResponse>.Success(ToEmergencyContactResponse(emergencyContact));
    }

    public async Task<ServiceResult<EmergencyContactResponse>> UpdateEmergencyContactAsync(
        Guid residentProfileId,
        Guid contactId,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var profileValidation = await ValidateActiveResidentProfileAsync(residentProfileId, cancellationToken);
        if (profileValidation is not null)
        {
            return ToResult<EmergencyContactResponse>(profileValidation);
        }

        var emergencyContact = await dbContext.EmergencyContacts.FirstOrDefaultAsync(
            contact => contact.Id == contactId && contact.ResidentProfileId == residentProfileId,
            cancellationToken);

        if (emergencyContact is null)
        {
            return ServiceResult<EmergencyContactResponse>.NotFound("Emergency contact was not found.");
        }

        emergencyContact.FullName = request.FullName.Trim();
        emergencyContact.Relationship = request.Relationship.Trim();
        emergencyContact.PhoneNumber = request.PhoneNumber.Trim();
        emergencyContact.IsActive = request.IsActive;
        emergencyContact.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<EmergencyContactResponse>.Success(ToEmergencyContactResponse(emergencyContact));
    }

    public async Task<ServiceResult<object?>> DeactivateEmergencyContactAsync(
        Guid residentProfileId,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        var profileValidation = await ValidateActiveResidentProfileAsync(residentProfileId, cancellationToken);
        if (profileValidation is not null)
        {
            return ToResult<object?>(profileValidation);
        }

        var emergencyContact = await dbContext.EmergencyContacts.FirstOrDefaultAsync(
            contact => contact.Id == contactId && contact.ResidentProfileId == residentProfileId,
            cancellationToken);

        if (emergencyContact is null)
        {
            return ServiceResult<object?>.NotFound("Emergency contact was not found.");
        }

        emergencyContact.IsActive = false;
        emergencyContact.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    private async Task<bool> ResidentProfileExistsAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        var residentProfile = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == residentProfileId)
            .Select(profile => new { profile.CompoundId, profile.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        return residentProfile is not null
            && await CanCurrentUserAccessResidentProfileAsync(
                residentProfile.CompoundId,
                residentProfile.UserId,
                cancellationToken);
    }

    private async Task<ValidationFailure?> ValidateActiveResidentProfileAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        var residentProfile = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == residentProfileId, cancellationToken);

        if (residentProfile is null)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Resident profile was not found.");
        }

        if (!residentProfile.IsActive)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Resident profile is inactive.");
        }

        return await CanCurrentUserAccessResidentProfileAsync(
            residentProfile.CompoundId,
            residentProfile.UserId,
            cancellationToken)
            ? null
            : new ValidationFailure(ServiceResultStatus.NotFound, "Resident profile was not found.");
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

    private async Task<bool> CanCurrentUserAccessResidentProfileAsync(
        Guid compoundId,
        Guid profileUserId,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return true;
        }

        if (await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken))
        {
            return true;
        }

        return currentUserService?.UserId == profileUserId;
    }

    private async Task<IQueryable<ResidentProfile>> ApplyCurrentResidentProfileScopeAsync(
        IQueryable<ResidentProfile> residentProfiles,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return residentProfiles;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return residentProfiles.ApplyCompoundAccess(scope, profile => profile.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static ResidentProfileResponse ToResidentProfileResponse(ResidentProfile residentProfile)
    {
        return new ResidentProfileResponse(
            residentProfile.Id,
            residentProfile.UserId,
            residentProfile.CompoundId,
            residentProfile.Compound.Name,
            residentProfile.FullName,
            residentProfile.NationalId,
            residentProfile.PhoneNumber,
            residentProfile.AlternativePhoneNumber,
            residentProfile.DateOfBirth,
            residentProfile.IsActive,
            residentProfile.CreatedAt,
            residentProfile.UpdatedAt);
    }

    private static FamilyMemberResponse ToFamilyMemberResponse(FamilyMember familyMember)
    {
        return new FamilyMemberResponse(
            familyMember.Id,
            familyMember.ResidentProfileId,
            familyMember.FullName,
            familyMember.Relationship,
            familyMember.DateOfBirth,
            familyMember.PhoneNumber,
            familyMember.IsActive,
            familyMember.CreatedAt,
            familyMember.UpdatedAt);
    }

    private static EmergencyContactResponse ToEmergencyContactResponse(EmergencyContact emergencyContact)
    {
        return new EmergencyContactResponse(
            emergencyContact.Id,
            emergencyContact.ResidentProfileId,
            emergencyContact.FullName,
            emergencyContact.Relationship,
            emergencyContact.PhoneNumber,
            emergencyContact.IsActive,
            emergencyContact.CreatedAt,
            emergencyContact.UpdatedAt);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
