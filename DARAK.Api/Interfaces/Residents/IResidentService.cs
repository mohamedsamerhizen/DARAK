using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.EmergencyContacts;
using DARAK.Api.DTOs.FamilyMembers;
using DARAK.Api.DTOs.Residents;

namespace DARAK.Api.Interfaces;

public interface IResidentService
{
    Task<PagedResult<ResidentProfileResponse>> SearchResidentProfilesAsync(
        ResidentProfileSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentProfileResponse>> GetResidentProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentProfileResponse>> GetResidentProfileForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentProfileResponse>> CreateResidentProfileAsync(
        CreateResidentProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentProfileResponse>> UpdateResidentProfileAsync(
        Guid id,
        UpdateResidentProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateResidentProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<FamilyMemberResponse>>> GetFamilyMembersAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FamilyMemberResponse>> AddFamilyMemberAsync(
        Guid residentProfileId,
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FamilyMemberResponse>> UpdateFamilyMemberAsync(
        Guid residentProfileId,
        Guid familyMemberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateFamilyMemberAsync(
        Guid residentProfileId,
        Guid familyMemberId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<EmergencyContactResponse>>> GetEmergencyContactsAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EmergencyContactResponse>> AddEmergencyContactAsync(
        Guid residentProfileId,
        CreateEmergencyContactRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EmergencyContactResponse>> UpdateEmergencyContactAsync(
        Guid residentProfileId,
        Guid contactId,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateEmergencyContactAsync(
        Guid residentProfileId,
        Guid contactId,
        CancellationToken cancellationToken = default);
}
