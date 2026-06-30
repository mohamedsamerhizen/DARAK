using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.EmergencyContacts;
using DARAK.Api.DTOs.FamilyMembers;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.DTOs.Occupancy;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.ResidentPortal;
using DARAK.Api.DTOs.Residents;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.DTOs.Violations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/account")]
public sealed class ResidentAccountController(
    ICurrentUserService currentUserService,
    IResidentPortalService residentPortalService,
    IResidentService residentService,
    IOccupancyService occupancyService,
    IUtilityBillService utilityBillService,
    IMeterService meterService,
    IComplaintViolationService complaintViolationService,
    IConversationService conversationService,
    IResidentFinancialHealthService residentFinancialHealthService,
    IFinancialControlService financialControlService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ResidentDashboardResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentPortalService.GetDashboardAsync(cancellationToken));
    }



    [HttpGet("financial-health")]
    public async Task<ActionResult<ResidentFinancialHealthResponse>> GetFinancialHealth(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await residentFinancialHealthService.GetCurrentResidentFinancialHealthAsync(
            userId,
            cancellationToken));
    }

    [HttpGet("statement")]
    public async Task<ActionResult<ResidentStatementResponse>> GetStatement(
        [FromQuery] ResidentStatementQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await financialControlService.GetResidentStatementForUserAsync(
            userId,
            query,
            cancellationToken));
    }

    [HttpGet("properties")]
    public async Task<ActionResult<IReadOnlyCollection<ResidentPropertySummaryResponse>>> GetProperties(
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentPortalService.GetMyPropertiesAsync(cancellationToken));
    }

    [HttpGet("bills")]
    public async Task<ActionResult<PagedResult<UtilityBillResponse>>> SearchBills(
        [FromQuery] UtilityBillSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await utilityBillService.SearchResidentBillsAsync(userId, query, cancellationToken));
    }

    [HttpGet("bills/{id:guid}")]
    public async Task<ActionResult<UtilityBillResponse>> GetBill(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await utilityBillService.GetResidentBillAsync(userId, id, cancellationToken));
    }

    [HttpPost("bills/{billId:guid}/dispute")]
    public async Task<ActionResult<ResidentBillDisputeResponse>> DisputeBill(
        Guid billId,
        ResidentBillDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await conversationService.OpenResidentBillDisputeAsync(
            userId,
            billId,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        var response = result.Value!;
        return response.CreatedNew
            ? StatusCode(StatusCodes.Status201Created, response)
            : Ok(response);
    }

    [HttpGet("installments")]
    public async Task<ActionResult<PagedResult<InstallmentScheduleItemResponse>>> SearchInstallments(
        [FromQuery] InstallmentSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentPortalService.GetMyInstallmentsAsync(query, cancellationToken));
    }

    [HttpGet("rent")]
    public async Task<ActionResult<PagedResult<RentInvoiceResponse>>> SearchRentInvoices(
        [FromQuery] RentInvoiceSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentPortalService.GetMyRentAsync(query, cancellationToken));
    }

    [HttpGet("payments")]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> SearchPayments(
        [FromQuery] PaymentSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentPortalService.GetMyPaymentsAsync(query, cancellationToken));
    }

    [HttpGet("meter-readings")]
    public async Task<ActionResult<PagedResult<MeterReadingResponse>>> SearchMeterReadings(
        [FromQuery] MeterReadingSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await meterService.SearchResidentMeterReadingsAsync(userId, query, cancellationToken));
    }

    [HttpGet("meter-readings/{id:guid}")]
    public async Task<ActionResult<MeterReadingResponse>> GetMeterReading(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await meterService.GetResidentMeterReadingAsync(userId, id, cancellationToken));
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ResidentProfileResponse>> GetProfile(
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        return ToActionResult(profileResult.ProfileResult!);
    }

    [HttpGet("occupancies")]
    public async Task<ActionResult<PagedResult<ResidentOccupancyRecordResponse>>> SearchOccupancies(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await occupancyService.SearchOccupanciesForUserAsync(userId, query, cancellationToken));
    }

    [HttpGet("family-members")]
    public async Task<ActionResult<IReadOnlyCollection<FamilyMemberResponse>>> GetFamilyMembers(
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        return ToActionResult(await residentService.GetFamilyMembersAsync(
            profileResult.ProfileResult.Value!.Id,
            cancellationToken));
    }

    [HttpPost("family-members")]
    public async Task<ActionResult<FamilyMemberResponse>> AddFamilyMember(
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        var result = await residentService.AddFamilyMemberAsync(
            profileResult.ProfileResult.Value!.Id,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("family-members/{familyMemberId:guid}")]
    public async Task<ActionResult<FamilyMemberResponse>> UpdateFamilyMember(
        Guid familyMemberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        return ToActionResult(await residentService.UpdateFamilyMemberAsync(
            profileResult.ProfileResult.Value!.Id,
            familyMemberId,
            request,
            cancellationToken));
    }

    [HttpDelete("family-members/{familyMemberId:guid}")]
    public async Task<IActionResult> DeactivateFamilyMember(
        Guid familyMemberId,
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        return ToNoContentResult(await residentService.DeactivateFamilyMemberAsync(
            profileResult.ProfileResult.Value!.Id,
            familyMemberId,
            cancellationToken));
    }

    [HttpGet("emergency-contacts")]
    public async Task<ActionResult<IReadOnlyCollection<EmergencyContactResponse>>> GetEmergencyContacts(
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        return ToActionResult(await residentService.GetEmergencyContactsAsync(
            profileResult.ProfileResult.Value!.Id,
            cancellationToken));
    }

    [HttpPost("emergency-contacts")]
    public async Task<ActionResult<EmergencyContactResponse>> AddEmergencyContact(
        CreateEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        var result = await residentService.AddEmergencyContactAsync(
            profileResult.ProfileResult.Value!.Id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("emergency-contacts/{contactId:guid}")]
    public async Task<ActionResult<EmergencyContactResponse>> UpdateEmergencyContact(
        Guid contactId,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        return ToActionResult(await residentService.UpdateEmergencyContactAsync(
            profileResult.ProfileResult.Value!.Id,
            contactId,
            request,
            cancellationToken));
    }

    [HttpDelete("emergency-contacts/{contactId:guid}")]
    public async Task<IActionResult> DeactivateEmergencyContact(
        Guid contactId,
        CancellationToken cancellationToken)
    {
        var profileResult = await GetCurrentResidentProfileOrErrorAsync(cancellationToken);
        if (profileResult.ErrorResult is not null)
        {
            return profileResult.ErrorResult;
        }

        if (!profileResult.ProfileResult!.IsSuccess)
        {
            return ToErrorObjectResult(profileResult.ProfileResult);
        }

        return ToNoContentResult(await residentService.DeactivateEmergencyContactAsync(
            profileResult.ProfileResult.Value!.Id,
            contactId,
            cancellationToken));
    }

    [HttpGet("violation-fines")]
    public async Task<ActionResult<PagedResult<ViolationFineResponse>>> SearchViolationFines(
        [FromQuery] ViolationFineSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await complaintViolationService.SearchViolationFinesResidentAsync(
            userId,
            query,
            cancellationToken));
    }

    [HttpGet("violation-fines/{id:guid}")]
    public async Task<ActionResult<ViolationFineResponse>> GetViolationFine(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await complaintViolationService.GetViolationFineResidentAsync(
            userId,
            id,
            cancellationToken));
    }

    private async Task<(ServiceResult<ResidentProfileResponse>? ProfileResult, ObjectResult? ErrorResult)>
        GetCurrentResidentProfileOrErrorAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return (null, unauthorizedResult);
        }

        return (await residentService.GetResidentProfileForUserAsync(userId, cancellationToken), null);
    }

    private bool TryGetCurrentUserId(out Guid userId, out ObjectResult unauthorizedResult)
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId.HasValue)
        {
            userId = currentUserId.Value;
            unauthorizedResult = null!;
            return true;
        }

        userId = Guid.Empty;
        unauthorizedResult = Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Current user is invalid."));
        return false;
    }
}
