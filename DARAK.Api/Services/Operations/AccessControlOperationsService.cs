using System.Security.Cryptography;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class AccessControlOperationsService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null,
    IAccessCodeHasher? accessCodeHasher = null)
    : IAccessControlOperationsService
{
    private const string MaskedCredentialCode = "********";
    private readonly IAccessCodeHasher accessCodeHasher = accessCodeHasher ?? new AccessCodeHasher();

    public async Task<ServiceResult<AccessControlOperationsSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var permits = await ApplyCurrentCompoundScopeAsync(
            dbContext.ContractorWorkPermits.AsNoTracking(),
            cancellationToken);
        var credentials = await ApplyCurrentCompoundScopeAsync(
            dbContext.AccessCredentials.AsNoTracking(),
            cancellationToken);

        if (compoundId.HasValue)
        {
            if (!await CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
            {
                return ServiceResult<AccessControlOperationsSummaryResponse>.NotFound("Compound was not found.");
            }

            permits = permits.Where(item => item.CompoundId == compoundId.Value);
            credentials = credentials.Where(item => item.CompoundId == compoundId.Value);
        }

        var now = DateTime.UtcNow;
        var expiringUntil = now.AddDays(7);

        var response = new AccessControlOperationsSummaryResponse(
            await permits.CountAsync(item => item.Status == ContractorWorkPermitStatus.PendingApproval, cancellationToken),
            await permits.CountAsync(item => item.Status == ContractorWorkPermitStatus.Approved, cancellationToken),
            await permits.CountAsync(item => item.Status == ContractorWorkPermitStatus.CheckedIn, cancellationToken),
            await credentials.CountAsync(item => item.Status == AccessCredentialStatus.Active, cancellationToken),
            await credentials.CountAsync(item => item.Status == AccessCredentialStatus.Active
                && item.ValidUntilUtc.HasValue
                && item.ValidUntilUtc.Value >= now
                && item.ValidUntilUtc.Value <= expiringUntil, cancellationToken),
            await credentials.CountAsync(item => item.Status == AccessCredentialStatus.Revoked, cancellationToken));

        return ServiceResult<AccessControlOperationsSummaryResponse>.Success(response);
    }

    public async Task<PagedResult<ContractorWorkPermitResponse>> SearchContractorWorkPermitsAsync(
        ContractorWorkPermitQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var permits = await ApplyCurrentCompoundScopeAsync(
            GetPermitDetailsQuery(asNoTracking: true),
            cancellationToken);

        return await ToPagedPermitResultAsync(ApplyPermitFilters(permits, query), query, cancellationToken);
    }

    public async Task<PagedResult<ContractorWorkPermitResponse>> SearchTodayContractorWorkPermitsForGuardAsync(
        Guid? guardUserId,
        ContractorWorkPermitQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var permits = ApplyPermitFilters(GetPermitDetailsQuery(asNoTracking: true), query)
            .Where(item => item.AllowedFromUtc < tomorrowStart
                && item.AllowedUntilUtc >= todayStart
                && (item.Status == ContractorWorkPermitStatus.Approved
                    || item.Status == ContractorWorkPermitStatus.CheckedIn));

        permits = await ApplyGuardCompoundScopeAsync(permits, guardUserId, cancellationToken);

        return await ToPagedPermitResultAsync(permits, query, cancellationToken);
    }

    public async Task<ServiceResult<ContractorWorkPermitResponse>> GetContractorWorkPermitAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var permits = await ApplyCurrentCompoundScopeAsync(
            GetPermitDetailsQuery(asNoTracking: true),
            cancellationToken);

        var permit = await permits.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return permit is null
            ? ServiceResult<ContractorWorkPermitResponse>.NotFound("Contractor work permit was not found.")
            : ServiceResult<ContractorWorkPermitResponse>.Success(ToPermitResponse(permit));
    }

    public async Task<ServiceResult<ContractorWorkPermitResponse>> CreateContractorWorkPermitAsync(
        Guid? createdByUserId,
        CreateContractorWorkPermitRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreatePermitRequest(request);
        if (validation is not null)
        {
            return ToResult<ContractorWorkPermitResponse>(validation);
        }

        if (!await CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ContractorWorkPermitResponse>.NotFound("Compound was not found.");
        }

        var vendor = await dbContext.ServiceVendors
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.VendorId, cancellationToken);
        if (vendor is null)
        {
            return ServiceResult<ContractorWorkPermitResponse>.NotFound("Service vendor was not found.");
        }

        if (vendor.CompoundId != request.CompoundId)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Service vendor must belong to the selected compound.");
        }

        if (vendor.Status != VendorStatus.Active)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Only active vendors can receive contractor work permits.");
        }

        if (request.RelatedWorkOrderId.HasValue)
        {
            var validWorkOrder = await dbContext.WorkOrders
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.RelatedWorkOrderId.Value
                    && item.CompoundId == request.CompoundId, cancellationToken);
            if (!validWorkOrder)
            {
                return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Related work order is not inside the selected compound.");
            }
        }

        var permit = new ContractorWorkPermit
        {
            CompoundId = request.CompoundId,
            VendorId = request.VendorId,
            RelatedWorkOrderId = request.RelatedWorkOrderId,
            Purpose = request.Purpose.Trim(),
            WorkArea = request.WorkArea.Trim(),
            EquipmentList = TrimOrNull(request.EquipmentList),
            RiskLevel = request.RiskLevel,
            AllowedFromUtc = request.AllowedFromUtc,
            AllowedUntilUtc = request.AllowedUntilUtc,
            RequiresEscort = request.RequiresEscort,
            CreatedByUserId = createdByUserId
        };

        dbContext.ContractorWorkPermits.Add(permit);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetContractorWorkPermitAsync(permit.Id, cancellationToken);
    }

    public async Task<ServiceResult<ContractorWorkPermitResponse>> ApproveContractorWorkPermitAsync(
        Guid id,
        Guid? approvedByUserId,
        ContractorPermitDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await GetScopedPermitForMutationAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToPermitResponseResult(result);
        }

        var permit = result.Value!;
        if (permit.Status != ContractorWorkPermitStatus.PendingApproval)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Only pending contractor work permits can be approved.");
        }

        if (permit.AllowedUntilUtc <= DateTime.UtcNow)
        {
            permit.Status = ContractorWorkPermitStatus.Expired;
            permit.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Expired contractor work permit cannot be approved.");
        }

        permit.Status = ContractorWorkPermitStatus.Approved;
        permit.ApprovedByUserId = approvedByUserId;
        permit.ApprovedAtUtc = DateTime.UtcNow;
        permit.GuardNotes = TrimOrNull(request.Notes);
        permit.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetContractorWorkPermitAsync(permit.Id, cancellationToken);
    }

    public async Task<ServiceResult<ContractorWorkPermitResponse>> DenyContractorWorkPermitAsync(
        Guid id,
        Guid? deniedByUserId,
        DenyContractorWorkPermitRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Denial reason is required.");
        }

        var result = await GetScopedPermitForMutationAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToPermitResponseResult(result);
        }

        var permit = result.Value!;
        if (permit.Status is ContractorWorkPermitStatus.CheckedIn or ContractorWorkPermitStatus.CheckedOut or ContractorWorkPermitStatus.Closed)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Contractor work permit cannot be denied in its current status.");
        }

        permit.Status = ContractorWorkPermitStatus.Denied;
        permit.DeniedByUserId = deniedByUserId;
        permit.DeniedAtUtc = DateTime.UtcNow;
        permit.DenialReason = reason;
        permit.UpdatedAtUtc = DateTime.UtcNow;
        AddContractorAccessLog(permit, ContractorAccessAction.Denied, deniedByUserId, reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetContractorWorkPermitAsync(permit.Id, cancellationToken);
    }

    public async Task<ServiceResult<ContractorWorkPermitResponse>> GuardCheckInContractorWorkPermitAsync(
        Guid id,
        Guid? guardUserId,
        GuardContractorPermitAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessCode = TrimOrNull(request.AccessCode);
        if (accessCode is null)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Contractor access code is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var permit = await GetPermitDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (permit is null
            || !await CanGuardAccessCompoundAsync(guardUserId, permit.CompoundId, cancellationToken))
        {
            return ServiceResult<ContractorWorkPermitResponse>.NotFound("Contractor work permit was not found.");
        }

        var now = DateTime.UtcNow;
        if (permit.AllowedUntilUtc <= now)
        {
            permit.Status = ContractorWorkPermitStatus.Expired;
            permit.UpdatedAtUtc = now;
            AddContractorAccessLog(permit, ContractorAccessAction.Denied, guardUserId, "Expired contractor work permit.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Expired contractor work permit cannot be checked in.");
        }

        if (permit.AllowedFromUtc > now)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Contractor work permit is not valid yet.");
        }

        if (permit.Status != ContractorWorkPermitStatus.Approved)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Only approved contractor work permits can be checked in.");
        }

        var credential = await dbContext.AccessCredentials
            .AsNoTracking()
            .Where(item => item.CompoundId == permit.CompoundId
                && item.SourceContractorWorkPermitId == permit.Id
                && item.Status == AccessCredentialStatus.Active
                && item.ValidFromUtc <= now
                && (!item.ValidUntilUtc.HasValue || item.ValidUntilUtc.Value > now))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (credential is null)
        {
            AddContractorAccessLog(permit, ContractorAccessAction.CredentialFailed, guardUserId, "Active contractor credential was not found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Active contractor credential was not found.");
        }

        if (!accessCodeHasher.Verify(accessCode, credential.CredentialCode))
        {
            AddContractorAccessLog(permit, ContractorAccessAction.CredentialFailed, guardUserId, "Invalid contractor access code.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Contractor access code is invalid.");
        }

        permit.Status = ContractorWorkPermitStatus.CheckedIn;
        permit.GuardCheckedInByUserId = guardUserId;
        permit.CheckedInAtUtc = now;
        permit.GuardNotes = TrimOrNull(request.Notes);
        permit.UpdatedAtUtc = now;
        AddContractorAccessLog(permit, ContractorAccessAction.CheckIn, guardUserId, TrimOrNull(request.Notes));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetContractorWorkPermitAsync(permit.Id, cancellationToken);
    }

    public async Task<ServiceResult<ContractorWorkPermitResponse>> GuardCheckOutContractorWorkPermitAsync(
        Guid id,
        Guid? guardUserId,
        GuardContractorPermitAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var permit = await GetPermitDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (permit is null
            || !await CanGuardAccessCompoundAsync(guardUserId, permit.CompoundId, cancellationToken))
        {
            return ServiceResult<ContractorWorkPermitResponse>.NotFound("Contractor work permit was not found.");
        }

        if (permit.Status != ContractorWorkPermitStatus.CheckedIn)
        {
            return ServiceResult<ContractorWorkPermitResponse>.BadRequest("Contractor work permit must be checked in before checkout.");
        }

        permit.Status = ContractorWorkPermitStatus.CheckedOut;
        permit.GuardCheckedOutByUserId = guardUserId;
        permit.CheckedOutAtUtc = DateTime.UtcNow;
        permit.GuardNotes = TrimOrNull(request.Notes) ?? permit.GuardNotes;
        permit.UpdatedAtUtc = DateTime.UtcNow;
        AddContractorAccessLog(permit, ContractorAccessAction.CheckOut, guardUserId, TrimOrNull(request.Notes));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetContractorWorkPermitAsync(permit.Id, cancellationToken);
    }

    public async Task<PagedResult<AccessCredentialResponse>> SearchAccessCredentialsAsync(
        AccessCredentialQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ApplyCurrentCompoundScopeAsync(
            GetCredentialDetailsQuery(asNoTracking: true),
            cancellationToken);

        credentials = ApplyCredentialFilters(credentials, query);

        var totalCount = await credentials.CountAsync(cancellationToken);
        var credentialItems = await credentials
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = credentialItems.Select(item => ToCredentialResponse(item)).ToArray();

        return new PagedResult<AccessCredentialResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<ServiceResult<AccessCredentialResponse>> CreateAccessCredentialAsync(
        CreateAccessCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateCredentialRequest(request);
        if (validation is not null)
        {
            return ToResult<AccessCredentialResponse>(validation);
        }

        if (!await CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<AccessCredentialResponse>.NotFound("Compound was not found.");
        }

        if (request.SourceVisitorPassId.HasValue)
        {
            var validVisitor = await dbContext.VisitorPasses
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceVisitorPassId.Value
                    && item.CompoundId == request.CompoundId, cancellationToken);
            if (!validVisitor)
            {
                return ServiceResult<AccessCredentialResponse>.BadRequest("Source visitor pass is not inside the selected compound.");
            }
        }

        if (request.SourceContractorWorkPermitId.HasValue)
        {
            var validPermit = await dbContext.ContractorWorkPermits
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceContractorWorkPermitId.Value
                    && item.CompoundId == request.CompoundId, cancellationToken);
            if (!validPermit)
            {
                return ServiceResult<AccessCredentialResponse>.BadRequest("Source contractor work permit is not inside the selected compound.");
            }
        }

        var code = TrimOrNull(request.CredentialCode) ?? await GenerateUniqueCredentialCodeAsync(cancellationToken);

        var credential = new AccessCredential
        {
            CompoundId = request.CompoundId,
            CredentialType = request.CredentialType,
            OwnerType = request.OwnerType,
            OwnerEntityId = request.OwnerEntityId,
            OwnerDisplayName = request.OwnerDisplayName.Trim(),
            CredentialCode = accessCodeHasher.Hash(code),
            ValidFromUtc = request.ValidFromUtc,
            ValidUntilUtc = request.ValidUntilUtc,
            SourceVisitorPassId = request.SourceVisitorPassId,
            SourceContractorWorkPermitId = request.SourceContractorWorkPermitId,
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.AccessCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AccessCredentialResponse>.Success(ToCredentialResponse(await dbContext.AccessCredentials
            .AsNoTracking()
            .Include(item => item.Compound)
            .FirstAsync(item => item.Id == credential.Id, cancellationToken), code));
    }

    public async Task<ServiceResult<AccessCredentialResponse>> RevokeAccessCredentialAsync(
        Guid id,
        Guid? revokedByUserId,
        RevokeAccessCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<AccessCredentialResponse>.BadRequest("Revocation reason is required.");
        }

        var credentials = await ApplyCurrentCompoundScopeAsync(
            GetCredentialDetailsQuery(asNoTracking: false),
            cancellationToken);
        var credential = await credentials.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (credential is null)
        {
            return ServiceResult<AccessCredentialResponse>.NotFound("Access credential was not found.");
        }

        if (credential.Status == AccessCredentialStatus.Revoked)
        {
            return ServiceResult<AccessCredentialResponse>.Conflict("Access credential is already revoked.");
        }

        credential.Status = AccessCredentialStatus.Revoked;
        credential.RevokedByUserId = revokedByUserId;
        credential.RevokedAtUtc = DateTime.UtcNow;
        credential.RevocationReason = reason;
        credential.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AccessCredentialResponse>.Success(ToCredentialResponse(credential));
    }



    public async Task<ServiceResult<AccessControlProDashboardResponse>> GetProDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<AccessControlProDashboardResponse>.NotFound("Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var expiringUntil = now.AddDays(7);

        var visitors = await ApplyCurrentCompoundScopeAsync(GetVisitorPassProQuery(asNoTracking: true), cancellationToken);
        var permits = await ApplyCurrentCompoundScopeAsync(GetPermitDetailsQuery(asNoTracking: true), cancellationToken);
        var credentials = await ApplyCurrentCompoundScopeAsync(GetCredentialDetailsQuery(asNoTracking: true), cancellationToken);

        if (compoundId.HasValue)
        {
            visitors = visitors.Where(item => item.CompoundId == compoundId.Value);
            permits = permits.Where(item => item.CompoundId == compoundId.Value);
            credentials = credentials.Where(item => item.CompoundId == compoundId.Value);
        }

        var securityItems = await BuildSecurityCommandQueueItemsAsync(
            visitors,
            permits,
            credentials,
            includeInformational: false,
            cancellationToken);

        var riskBuckets = securityItems
            .GroupBy(item => item.RiskLevel)
            .OrderBy(group => AccessRiskRank(group.Key))
            .Select(group => new AccessControlMetricBucketResponse(group.Key, group.Count()))
            .ToArray();

        var response = new AccessControlProDashboardResponse(
            compoundId,
            await visitors.CountAsync(item => item.ValidFrom < tomorrowStart
                && item.ValidUntil >= todayStart
                && (item.Status == VisitorPassStatus.Pending || item.Status == VisitorPassStatus.Approved || item.Status == VisitorPassStatus.CheckedIn), cancellationToken),
            await visitors.CountAsync(item => item.Status == VisitorPassStatus.CheckedIn, cancellationToken),
            await visitors.CountAsync(item => item.Status == VisitorPassStatus.Pending, cancellationToken),
            await visitors.CountAsync(item => item.Status == VisitorPassStatus.CheckedIn
                && !item.CheckedOutAt.HasValue
                && item.ValidUntil < now, cancellationToken),
            await visitors.CountAsync(item => item.Status == VisitorPassStatus.Denied
                && item.UpdatedAt.HasValue
                && item.UpdatedAt.Value >= todayStart, cancellationToken),
            await permits.CountAsync(item => item.AllowedFromUtc < tomorrowStart
                && item.AllowedUntilUtc >= todayStart
                && (item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn), cancellationToken),
            await permits.CountAsync(item => item.Status == ContractorWorkPermitStatus.CheckedIn, cancellationToken),
            await permits.CountAsync(item => item.RequiresEscort
                && (item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn), cancellationToken),
            await permits.CountAsync(item => item.RiskLevel >= ContractorWorkPermitRiskLevel.High
                && (item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn), cancellationToken),
            await permits.CountAsync(item => item.Status == ContractorWorkPermitStatus.PendingApproval, cancellationToken),
            await credentials.CountAsync(item => item.Status == AccessCredentialStatus.Active, cancellationToken),
            await credentials.CountAsync(item => item.Status == AccessCredentialStatus.Active
                && item.ValidUntilUtc.HasValue
                && item.ValidUntilUtc.Value >= now
                && item.ValidUntilUtc.Value <= expiringUntil, cancellationToken),
            await credentials.CountAsync(item => item.Status == AccessCredentialStatus.Revoked, cancellationToken),
            securityItems.Length,
            riskBuckets);

        return ServiceResult<AccessControlProDashboardResponse>.Success(response);
    }

    public async Task<PagedResult<AccessSecurityCommandQueueItemResponse>> GetSecurityCommandQueueAsync(
        AccessSecurityCommandQueueQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var visitors = await ApplyCurrentCompoundScopeAsync(GetVisitorPassProQuery(asNoTracking: true), cancellationToken);
        var permits = await ApplyCurrentCompoundScopeAsync(GetPermitDetailsQuery(asNoTracking: true), cancellationToken);
        var credentials = await ApplyCurrentCompoundScopeAsync(GetCredentialDetailsQuery(asNoTracking: true), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            visitors = visitors.Where(item => item.CompoundId == query.CompoundId.Value);
            permits = permits.Where(item => item.CompoundId == query.CompoundId.Value);
            credentials = credentials.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        var items = await BuildSecurityCommandQueueItemsAsync(
            visitors,
            permits,
            credentials,
            query.IncludeInformational,
            cancellationToken);

        var riskLevel = TrimOrNull(query.RiskLevel);
        if (riskLevel is not null)
        {
            items = items
                .Where(item => string.Equals(item.RiskLevel, riskLevel, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var sourceType = TrimOrNull(query.SourceType);
        if (sourceType is not null)
        {
            items = items
                .Where(item => string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        items = items
            .OrderBy(item => AccessRiskRank(item.RiskLevel))
            .ThenBy(item => item.DetectedAtUtc)
            .ToArray();

        return ToPagedInMemory(items, query.PageNumber, query.PageSize);
    }

    public async Task<PagedResult<AccessCredentialRiskQueueItemResponse>> GetCredentialRiskQueueAsync(
        AccessCredentialRiskQueueQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ApplyCurrentCompoundScopeAsync(GetCredentialDetailsQuery(asNoTracking: true), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            credentials = credentials.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.OwnerType.HasValue)
        {
            credentials = credentials.Where(item => item.OwnerType == query.OwnerType.Value);
        }

        var now = DateTime.UtcNow;
        var expiringSoon = now.AddHours(48);
        var credentialItems = await credentials.ToArrayAsync(cancellationToken);
        var riskItems = credentialItems
            .Select(item => ToCredentialRiskItem(item, now, expiringSoon))
            .Where(item => item is not null)
            .Cast<AccessCredentialRiskQueueItemResponse>()
            .ToArray();

        var riskLevel = TrimOrNull(query.RiskLevel);
        if (riskLevel is not null)
        {
            riskItems = riskItems
                .Where(item => string.Equals(item.RiskLevel, riskLevel, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        riskItems = riskItems
            .OrderBy(item => AccessRiskRank(item.RiskLevel))
            .ThenBy(item => item.ValidUntilUtc ?? DateTime.MaxValue)
            .ToArray();

        return ToPagedInMemory(riskItems, query.PageNumber, query.PageSize);
    }

    public async Task<PagedResult<ContractorEscortQueueItemResponse>> GetContractorEscortQueueAsync(
        ContractorEscortQueueQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var permits = await ApplyCurrentCompoundScopeAsync(GetPermitDetailsQuery(asNoTracking: true), cancellationToken);
        var now = DateTime.UtcNow;

        if (query.CompoundId.HasValue)
        {
            permits = permits.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.MinimumRiskLevel.HasValue)
        {
            permits = permits.Where(item => item.RiskLevel >= query.MinimumRiskLevel.Value);
        }

        if (query.OnlyActiveWindow)
        {
            permits = permits.Where(item => item.AllowedFromUtc <= now && item.AllowedUntilUtc >= now);
        }

        permits = permits.Where(item => item.RequiresEscort
            || item.RiskLevel >= ContractorWorkPermitRiskLevel.High
            || item.Status == ContractorWorkPermitStatus.CheckedIn);

        var permitItems = await permits
            .OrderByDescending(item => item.RiskLevel)
            .ThenBy(item => item.AllowedFromUtc)
            .ToArrayAsync(cancellationToken);

        var responseItems = permitItems
            .Select(ToContractorEscortQueueItem)
            .ToArray();

        return ToPagedInMemory(responseItems, query.PageNumber, query.PageSize);
    }

    public async Task<PagedResult<AccessAuditTrailItemResponse>> GetAccessAuditTrailAsync(
        AccessAuditTrailQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(GetVisitorPassProQuery(asNoTracking: true), cancellationToken);
        var permits = await ApplyCurrentCompoundScopeAsync(GetPermitDetailsQuery(asNoTracking: true), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            visitorPasses = visitorPasses.Where(item => item.CompoundId == query.CompoundId.Value);
            permits = permits.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        var visitorPassItems = await visitorPasses.ToArrayAsync(cancellationToken);
        var visitorItems = visitorPassItems
            .SelectMany(pass => pass.AccessLogs.Select(log => new AccessAuditTrailItemResponse(
                "VisitorPass",
                pass.Id,
                pass.CompoundId,
                pass.Compound.Name,
                pass.VisitorName,
                log.Action.ToString(),
                log.GuardUserId,
                log.GuardUser == null ? null : log.GuardUser.FullName,
                log.CreatedAt,
                log.Notes)))
            .ToArray();

        var permitItems = await permits
            .Where(item => item.AccessLogs.Any()
                || item.CheckedInAtUtc.HasValue
                || item.CheckedOutAtUtc.HasValue
                || item.DeniedAtUtc.HasValue)
            .ToArrayAsync(cancellationToken);

        var guardUserIds = permitItems
            .SelectMany(item => new[] { item.GuardCheckedInByUserId, item.GuardCheckedOutByUserId, item.DeniedByUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var guardNames = guardUserIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => guardUserIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var contractorItems = permitItems
            .SelectMany(item => BuildContractorAuditItems(item, guardNames))
            .ToArray();

        var combined = visitorItems
            .Concat(contractorItems)
            .ToArray();

        if (query.FromUtc.HasValue)
        {
            combined = combined.Where(item => item.OccurredAtUtc >= query.FromUtc.Value).ToArray();
        }

        if (query.UntilUtc.HasValue)
        {
            combined = combined.Where(item => item.OccurredAtUtc <= query.UntilUtc.Value).ToArray();
        }

        var sourceType = TrimOrNull(query.SourceType);
        if (sourceType is not null)
        {
            combined = combined
                .Where(item => string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        combined = combined
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToArray();

        return ToPagedInMemory(combined, query.PageNumber, query.PageSize);
    }



    public async Task<ServiceResult<AccessGateSituationReportResponse>> GetGateSituationReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<AccessGateSituationReportResponse>.NotFound("Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var visitors = await ApplyCurrentCompoundScopeAsync(GetVisitorPassProQuery(asNoTracking: true), cancellationToken);
        var permits = await ApplyCurrentCompoundScopeAsync(GetPermitDetailsQuery(asNoTracking: true), cancellationToken);
        var credentials = await ApplyCurrentCompoundScopeAsync(GetCredentialDetailsQuery(asNoTracking: true), cancellationToken);

        if (compoundId.HasValue)
        {
            visitors = visitors.Where(item => item.CompoundId == compoundId.Value);
            permits = permits.Where(item => item.CompoundId == compoundId.Value);
            credentials = credentials.Where(item => item.CompoundId == compoundId.Value);
        }

        var visitorItems = await visitors.ToArrayAsync(cancellationToken);
        var permitItems = await permits.ToArrayAsync(cancellationToken);
        var credentialItems = await credentials.ToArrayAsync(cancellationToken);
        var securityItems = await BuildSecurityCommandQueueItemsAsync(
            visitors,
            permits,
            credentials,
            includeInformational: false,
            cancellationToken);

        var criticalActionCount = securityItems.Count(item => item.RiskLevel == "Critical");
        var highActionCount = securityItems.Count(item => item.RiskLevel == "High");
        var mediumActionCount = securityItems.Count(item => item.RiskLevel == "Medium");
        var topSecurityActions = securityItems
            .OrderBy(item => AccessRiskRank(item.RiskLevel))
            .ThenBy(item => item.DetectedAtUtc)
            .Take(10)
            .ToArray();

        var operationalBuckets = new[]
        {
            new AccessControlMetricBucketResponse("VisitorsOnSite", visitorItems.Count(item => item.Status == VisitorPassStatus.CheckedIn)),
            new AccessControlMetricBucketResponse("ContractorsOnSite", permitItems.Count(item => item.Status == ContractorWorkPermitStatus.CheckedIn)),
            new AccessControlMetricBucketResponse("PendingVisitorApprovals", visitorItems.Count(item => item.Status == VisitorPassStatus.Pending)),
            new AccessControlMetricBucketResponse("PendingContractorApprovals", permitItems.Count(item => item.Status == ContractorWorkPermitStatus.PendingApproval)),
            new AccessControlMetricBucketResponse("ActiveCredentials", credentialItems.Count(item => item.Status == AccessCredentialStatus.Active))
        };

        var response = new AccessGateSituationReportResponse(
            compoundId,
            now,
            visitorItems.Count(item => item.ValidFrom < tomorrowStart
                && item.ValidUntil >= todayStart
                && (item.Status == VisitorPassStatus.Pending || item.Status == VisitorPassStatus.Approved || item.Status == VisitorPassStatus.CheckedIn)),
            visitorItems.Count(item => item.Status == VisitorPassStatus.CheckedIn),
            visitorItems.Count(item => item.Status == VisitorPassStatus.CheckedIn
                && !item.CheckedOutAt.HasValue
                && item.ValidUntil < now),
            visitorItems.Count(item => item.Status == VisitorPassStatus.Pending),
            visitorItems.Count(item => item.Status == VisitorPassStatus.Denied
                && item.UpdatedAt.HasValue
                && item.UpdatedAt.Value >= todayStart),
            permitItems.Count(item => item.AllowedFromUtc < tomorrowStart
                && item.AllowedUntilUtc >= todayStart
                && (item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn)),
            permitItems.Count(item => item.Status == ContractorWorkPermitStatus.CheckedIn),
            permitItems.Count(item => item.Status == ContractorWorkPermitStatus.CheckedIn
                && item.AllowedUntilUtc < now),
            permitItems.Count(item => item.Status == ContractorWorkPermitStatus.PendingApproval),
            permitItems.Count(item => item.RequiresEscort
                && (item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn)),
            permitItems.Count(item => item.RiskLevel >= ContractorWorkPermitRiskLevel.High
                && (item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn)),
            credentialItems.Count(item => item.Status == AccessCredentialStatus.Active),
            credentialItems.Count(item => item.Status == AccessCredentialStatus.Active
                && item.ValidUntilUtc.HasValue
                && item.ValidUntilUtc.Value < now),
            credentialItems.Count(item => item.Status == AccessCredentialStatus.Lost),
            criticalActionCount,
            highActionCount,
            mediumActionCount,
            BuildGateSituationDecision(criticalActionCount, highActionCount, mediumActionCount),
            operationalBuckets,
            topSecurityActions);

        return ServiceResult<AccessGateSituationReportResponse>.Success(response);
    }

    public async Task<PagedResult<VisitorVerificationBoardItemResponse>> GetVisitorVerificationBoardAsync(
        VisitorVerificationBoardQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var visitorPasses = await ApplyCurrentCompoundScopeAsync(GetVisitorPassProQuery(asNoTracking: true), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            visitorPasses = visitorPasses.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.Status.HasValue)
        {
            visitorPasses = visitorPasses.Where(item => item.Status == query.Status.Value);
        }

        if (!query.IncludeFuture)
        {
            var todayStart = now.Date;
            var nextWindow = now.AddHours(24);
            visitorPasses = visitorPasses.Where(item => item.ValidFrom <= nextWindow && item.ValidUntil >= todayStart);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            visitorPasses = visitorPasses.Where(item => item.VisitorName.Contains(searchTerm)
                || item.VisitorPhoneNumber.Contains(searchTerm)
                || item.PropertyUnit.UnitNumber.Contains(searchTerm)
                || item.ResidentProfile.FullName.Contains(searchTerm));
        }

        var visitorItems = await visitorPasses.ToArrayAsync(cancellationToken);
        var boardItems = visitorItems
            .Select(item => ToVisitorVerificationBoardItem(item, now))
            .ToArray();

        if (query.OnlyActionRequired)
        {
            boardItems = boardItems.Where(item => item.ActionRequired).ToArray();
        }

        boardItems = boardItems
            .OrderBy(item => AccessRiskRank(item.RiskLevel))
            .ThenBy(item => item.ValidFromUtc)
            .ToArray();

        return ToPagedInMemory(boardItems, query.PageNumber, query.PageSize);
    }

    public async Task<PagedResult<ContractorAccessComplianceBoardItemResponse>> GetContractorAccessComplianceBoardAsync(
        ContractorAccessComplianceBoardQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var permits = await ApplyCurrentCompoundScopeAsync(GetPermitDetailsQuery(asNoTracking: true), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            permits = permits.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.MinimumRiskLevel.HasValue)
        {
            permits = permits.Where(item => item.RiskLevel >= query.MinimumRiskLevel.Value);
        }

        if (query.Status.HasValue)
        {
            permits = permits.Where(item => item.Status == query.Status.Value);
        }

        var permitItems = await permits.ToArrayAsync(cancellationToken);
        var boardItems = permitItems
            .Select(item => ToContractorAccessComplianceBoardItem(item, now))
            .ToArray();

        if (query.OnlyActionRequired)
        {
            boardItems = boardItems.Where(item => item.ActionRequired).ToArray();
        }

        boardItems = boardItems
            .OrderBy(item => AccessRiskRank(item.RiskLevel.ToString()))
            .ThenBy(item => item.AllowedFromUtc)
            .ToArray();

        return ToPagedInMemory(boardItems, query.PageNumber, query.PageSize);
    }

    public async Task<PagedResult<AccessCredentialControlBoardItemResponse>> GetCredentialControlBoardAsync(
        AccessCredentialControlBoardQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var credentials = await ApplyCurrentCompoundScopeAsync(GetCredentialDetailsQuery(asNoTracking: true), cancellationToken);

        if (query.CompoundId.HasValue)
        {
            credentials = credentials.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.OwnerType.HasValue)
        {
            credentials = credentials.Where(item => item.OwnerType == query.OwnerType.Value);
        }

        if (query.Status.HasValue)
        {
            credentials = credentials.Where(item => item.Status == query.Status.Value);
        }

        var credentialItems = await credentials.ToArrayAsync(cancellationToken);
        var boardItems = credentialItems
            .Select(item => ToCredentialControlBoardItem(item, now))
            .ToArray();

        if (query.OnlyActionRequired)
        {
            boardItems = boardItems.Where(item => item.ActionRequired).ToArray();
        }

        boardItems = boardItems
            .OrderBy(item => AccessRiskRank(item.RiskLevel))
            .ThenBy(item => item.ValidUntilUtc ?? DateTime.MaxValue)
            .ToArray();

        return ToPagedInMemory(boardItems, query.PageNumber, query.PageSize);
    }

    public async Task<ServiceResult<GuardShiftHandoverReportResponse>> GetGuardShiftHandoverReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<GuardShiftHandoverReportResponse>.NotFound("Compound was not found.");
        }

        var shiftEnd = DateTime.UtcNow;
        var shiftStart = shiftEnd.AddHours(-12);
        var auditTrail = await GetAccessAuditTrailAsync(
            new AccessAuditTrailQueryRequest
            {
                CompoundId = compoundId,
                FromUtc = shiftStart,
                UntilUtc = shiftEnd,
                PageSize = 100
            },
            cancellationToken);
        var situation = await GetGateSituationReportAsync(compoundId, cancellationToken);

        if (!situation.IsSuccess)
        {
            return situation.Status switch
            {
                ServiceResultStatus.NotFound => ServiceResult<GuardShiftHandoverReportResponse>.NotFound(situation.Message ?? "Compound was not found."),
                ServiceResultStatus.Forbidden => ServiceResult<GuardShiftHandoverReportResponse>.Forbidden(situation.Message ?? "Forbidden."),
                _ => ServiceResult<GuardShiftHandoverReportResponse>.BadRequest(situation.Message ?? "Unable to build guard shift handover report.")
            };
        }

        var auditItems = auditTrail.Items.ToArray();
        var openActions = situation.Value!.TopSecurityActions.ToArray();
        var response = new GuardShiftHandoverReportResponse(
            compoundId,
            shiftStart,
            shiftEnd,
            auditItems.Count(item => item.SourceType == "VisitorPass" && item.Action == VisitorAccessAction.CheckIn.ToString()),
            auditItems.Count(item => item.SourceType == "VisitorPass" && item.Action == VisitorAccessAction.CheckOut.ToString()),
            auditItems.Count(item => item.SourceType == "VisitorPass" && item.Action == VisitorAccessAction.Denied.ToString()),
            auditItems.Count(item => item.SourceType == "ContractorWorkPermit" && item.Action == "CheckIn"),
            auditItems.Count(item => item.SourceType == "ContractorWorkPermit" && item.Action == "CheckOut"),
            situation.Value.VisitorOnSiteCount,
            situation.Value.ContractorOnSiteCount,
            openActions.Count(item => item.RiskLevel == "Critical"),
            BuildGuardHandoverSummary(situation.Value.CriticalActionCount, situation.Value.HighActionCount, auditItems.Length),
            auditItems.Take(30).ToArray(),
            openActions);

        return ServiceResult<GuardShiftHandoverReportResponse>.Success(response);
    }

    private static VisitorVerificationBoardItemResponse ToVisitorVerificationBoardItem(
        VisitorPass item,
        DateTime now)
    {
        var lastLog = item.AccessLogs
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();
        var riskLevel = GetVisitorRiskLevel(item, now);
        var verificationStatus = item.Status switch
        {
            VisitorPassStatus.CheckedIn when item.ValidUntil < now && !item.CheckedOutAt.HasValue => "Overstaying",
            VisitorPassStatus.Pending => "PendingApproval",
            VisitorPassStatus.Approved => "ReadyForGateVerification",
            VisitorPassStatus.CheckedIn => "OnSite",
            VisitorPassStatus.CheckedOut => "Completed",
            VisitorPassStatus.Denied => "Denied",
            _ => "Closed"
        };
        var actionRequired = riskLevel is "Critical" or "High" or "Medium"
            || item.Status is VisitorPassStatus.Pending or VisitorPassStatus.CheckedIn;

        return new VisitorVerificationBoardItemResponse(
            item.Id,
            item.CompoundId,
            item.Compound.Name,
            item.PropertyUnitId,
            item.PropertyUnit.UnitNumber,
            item.ResidentProfileId,
            item.ResidentProfile.FullName,
            item.VisitorName,
            item.VisitorPhoneNumber,
            item.Status,
            item.ValidFrom,
            item.ValidUntil,
            item.CheckedInAt,
            item.CheckedOutAt,
            riskLevel,
            verificationStatus,
            actionRequired,
            BuildVisitorRecommendedAction(item, riskLevel, verificationStatus),
            lastLog?.Action.ToString(),
            lastLog?.CreatedAt);
    }

    private static ContractorAccessComplianceBoardItemResponse ToContractorAccessComplianceBoardItem(
        ContractorWorkPermit item,
        DateTime now)
    {
        var isOverstaying = item.Status == ContractorWorkPermitStatus.CheckedIn && item.AllowedUntilUtc < now;
        var requiresEscortVerification = item.RequiresEscort && (item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn);
        var complianceStatus = item.Status switch
        {
            ContractorWorkPermitStatus.CheckedIn when isOverstaying => "ExpiredOnSite",
            ContractorWorkPermitStatus.PendingApproval => "AwaitingApproval",
            ContractorWorkPermitStatus.Approved when item.AllowedUntilUtc < now => "ExpiredBeforeEntry",
            ContractorWorkPermitStatus.Approved when requiresEscortVerification => "EscortRequiredBeforeEntry",
            ContractorWorkPermitStatus.CheckedIn when requiresEscortVerification => "EscortActiveVerificationRequired",
            _ when item.RiskLevel >= ContractorWorkPermitRiskLevel.High => "HighRiskMonitor",
            _ => "Compliant"
        };
        var actionRequired = complianceStatus != "Compliant";

        return new ContractorAccessComplianceBoardItemResponse(
            item.Id,
            item.CompoundId,
            item.Compound.Name,
            item.VendorId,
            item.Vendor.Name,
            item.Purpose,
            item.WorkArea,
            item.RiskLevel,
            item.Status,
            item.AllowedFromUtc,
            item.AllowedUntilUtc,
            item.RequiresEscort,
            item.CheckedInAtUtc,
            item.CheckedOutAtUtc,
            isOverstaying,
            requiresEscortVerification,
            actionRequired,
            complianceStatus,
            BuildContractorComplianceAction(complianceStatus));
    }

    private static AccessCredentialControlBoardItemResponse ToCredentialControlBoardItem(
        AccessCredential item,
        DateTime now)
    {
        var controlStatus = item.Status switch
        {
            AccessCredentialStatus.Active when item.ValidUntilUtc.HasValue && item.ValidUntilUtc.Value < now => "ExpiredStillActive",
            AccessCredentialStatus.Active when item.ValidFromUtc > now => "ActiveBeforeValidityWindow",
            AccessCredentialStatus.Active when item.ValidUntilUtc.HasValue && item.ValidUntilUtc.Value <= now.AddHours(48) => "ExpiringSoon",
            AccessCredentialStatus.Lost => "LostCredential",
            AccessCredentialStatus.Suspended => "SuspendedCredential",
            AccessCredentialStatus.Revoked => "Revoked",
            AccessCredentialStatus.Expired => "Expired",
            _ => "Controlled"
        };
        var riskLevel = controlStatus switch
        {
            "ExpiredStillActive" => "Critical",
            "LostCredential" => "High",
            "ActiveBeforeValidityWindow" => "Medium",
            "ExpiringSoon" => "Medium",
            "SuspendedCredential" => "Medium",
            _ => "Low"
        };
        var actionRequired = riskLevel is "Critical" or "High" or "Medium";

        return new AccessCredentialControlBoardItemResponse(
            item.Id,
            item.CompoundId,
            item.Compound.Name,
            item.CredentialType,
            item.OwnerType,
            item.OwnerDisplayName,
            MaskedCredentialCode,
            item.Status,
            item.ValidFromUtc,
            item.ValidUntilUtc,
            actionRequired,
            controlStatus,
            riskLevel,
            BuildCredentialControlAction(controlStatus));
    }

    private static string GetVisitorRiskLevel(VisitorPass item, DateTime now)
    {
        if (item.Status == VisitorPassStatus.CheckedIn && !item.CheckedOutAt.HasValue && item.ValidUntil < now)
        {
            return "Critical";
        }

        if (item.Status == VisitorPassStatus.Pending && item.ValidFrom <= now.AddHours(1))
        {
            return "High";
        }

        if (item.Status == VisitorPassStatus.Pending && item.ValidFrom <= now.AddHours(4))
        {
            return "Medium";
        }

        if (item.Status == VisitorPassStatus.Approved && item.ValidFrom <= now.AddHours(2) && item.ValidUntil >= now)
        {
            return "Low";
        }

        if (item.Status == VisitorPassStatus.Denied && item.UpdatedAt.HasValue && item.UpdatedAt.Value >= now.Date)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string BuildVisitorRecommendedAction(
        VisitorPass item,
        string riskLevel,
        string verificationStatus)
    {
        if (verificationStatus == "Overstaying")
        {
            return "Locate visitor, confirm host, and close checkout immediately.";
        }

        if (verificationStatus == "PendingApproval")
        {
            return riskLevel == "High"
                ? "Approve or deny before the visitor reaches the gate."
                : "Review resident request before the access window.";
        }

        if (verificationStatus == "ReadyForGateVerification")
        {
            return "Verify code, identity, destination unit, and resident approval at the gate.";
        }

        if (verificationStatus == "OnSite")
        {
            return "Monitor on-site visitor until checkout is completed.";
        }

        return item.Status == VisitorPassStatus.Denied
            ? "Review denial reason if similar attempts repeat."
            : "No immediate guard action required.";
    }

    private static string BuildContractorComplianceAction(string complianceStatus)
    {
        return complianceStatus switch
        {
            "ExpiredOnSite" => "Escalate immediately and force contractor checkout or formal extension review.",
            "AwaitingApproval" => "Approve, deny, or reschedule before contractor arrives at gate.",
            "ExpiredBeforeEntry" => "Do not allow entry. Require a fresh permit window.",
            "EscortRequiredBeforeEntry" => "Assign escort before check-in.",
            "EscortActiveVerificationRequired" => "Verify escort remains assigned until contractor checkout.",
            "HighRiskMonitor" => "Supervisor should monitor high-risk contractor access.",
            _ => "No immediate contractor access action required."
        };
    }

    private static string BuildCredentialControlAction(string controlStatus)
    {
        return controlStatus switch
        {
            "ExpiredStillActive" => "Revoke or replace the credential immediately.",
            "LostCredential" => "Block access, revoke credential, and verify owner identity before replacement.",
            "ActiveBeforeValidityWindow" => "Suspend access until the validity window starts or correct the dates.",
            "ExpiringSoon" => "Renew, replace, or let expire based on owner eligibility.",
            "SuspendedCredential" => "Resolve suspension reason or revoke the credential.",
            _ => "No immediate credential control action required."
        };
    }

    private static string BuildGateSituationDecision(
        int criticalActionCount,
        int highActionCount,
        int mediumActionCount)
    {
        if (criticalActionCount > 0)
        {
            return "Lock escalation: critical access issues require immediate operations intervention.";
        }

        if (highActionCount > 0)
        {
            return "Supervisor review required before continuing normal gate flow.";
        }

        if (mediumActionCount > 0)
        {
            return "Operate normally with active queue monitoring.";
        }

        return "Gate access flow is clear for normal operation.";
    }

    private static string BuildGuardHandoverSummary(
        int criticalActionCount,
        int highActionCount,
        int accessEventCount)
    {
        if (criticalActionCount > 0)
        {
            return "Incoming guard must start with critical access actions before routine processing.";
        }

        if (highActionCount > 0)
        {
            return "Incoming guard must review high-risk queue and pending approvals.";
        }

        return accessEventCount == 0
            ? "No access events in the last shift window. Continue standard monitoring."
            : "Shift handover is normal. Review recent events and open monitoring items.";
    }




    private IQueryable<VisitorPass> GetVisitorPassProQuery(bool asNoTracking)
    {
        var query = dbContext.VisitorPasses
            .Include(item => item.ResidentProfile)
            .Include(item => item.Compound)
            .Include(item => item.PropertyUnit)
            .Include(item => item.AccessLogs)
                .ThenInclude(log => log.GuardUser)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private async Task<AccessSecurityCommandQueueItemResponse[]> BuildSecurityCommandQueueItemsAsync(
        IQueryable<VisitorPass> visitorPasses,
        IQueryable<ContractorWorkPermit> permits,
        IQueryable<AccessCredential> credentials,
        bool includeInformational,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nearWindow = now.AddHours(2);
        var expiringSoon = now.AddHours(48);

        var visitorItems = await visitorPasses
            .Where(item => item.Status == VisitorPassStatus.CheckedIn
                    || item.Status == VisitorPassStatus.Pending
                    || item.Status == VisitorPassStatus.Denied
                    || (includeInformational && item.Status == VisitorPassStatus.Approved))
            .ToArrayAsync(cancellationToken);

        var permitItems = await permits
            .Where(item => item.Status == ContractorWorkPermitStatus.CheckedIn
                    || item.Status == ContractorWorkPermitStatus.PendingApproval
                    || item.Status == ContractorWorkPermitStatus.Approved
                    || item.RiskLevel >= ContractorWorkPermitRiskLevel.High
                    || item.RequiresEscort)
            .ToArrayAsync(cancellationToken);

        var credentialItems = await credentials
            .Where(item => item.Status == AccessCredentialStatus.Active
                    || item.Status == AccessCredentialStatus.Suspended
                    || item.Status == AccessCredentialStatus.Lost)
            .ToArrayAsync(cancellationToken);

        var items = new List<AccessSecurityCommandQueueItemResponse>();

        foreach (var pass in visitorItems)
        {
            if (pass.Status == VisitorPassStatus.CheckedIn && !pass.CheckedOutAt.HasValue && pass.ValidUntil < now)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "VisitorPass",
                    pass.Id,
                    pass.CompoundId,
                    pass.Compound.Name,
                    pass.VisitorName,
                    "High",
                    pass.Status.ToString(),
                    "Visitor is checked in after the allowed visit window.",
                    "Call the guard desk to verify location and close the visit.",
                    pass.ValidUntil));
                continue;
            }

            if (pass.Status == VisitorPassStatus.Pending && pass.ValidFrom <= nearWindow)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "VisitorPass",
                    pass.Id,
                    pass.CompoundId,
                    pass.Compound.Name,
                    pass.VisitorName,
                    "Medium",
                    pass.Status.ToString(),
                    "Visitor pass is pending near the access window.",
                    "Approve, deny, or contact the resident before visitor arrival.",
                    pass.ValidFrom));
                continue;
            }

            if (pass.Status == VisitorPassStatus.Denied && pass.UpdatedAt.HasValue && pass.UpdatedAt.Value >= now.Date)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "VisitorPass",
                    pass.Id,
                    pass.CompoundId,
                    pass.Compound.Name,
                    pass.VisitorName,
                    "Low",
                    pass.Status.ToString(),
                    "Visitor entry was denied today.",
                    "Review denial reason if repeated denied attempts occur.",
                    pass.UpdatedAt.Value));
                continue;
            }

            if (includeInformational && pass.Status == VisitorPassStatus.Approved && pass.ValidFrom <= nearWindow)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "VisitorPass",
                    pass.Id,
                    pass.CompoundId,
                    pass.Compound.Name,
                    pass.VisitorName,
                    "Low",
                    pass.Status.ToString(),
                    "Visitor has an approved pass near the access window.",
                    "Prepare gate verification.",
                    pass.ValidFrom));
            }
        }

        foreach (var permit in permitItems)
        {
            if (permit.Status == ContractorWorkPermitStatus.CheckedIn && permit.AllowedUntilUtc < now)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "ContractorWorkPermit",
                    permit.Id,
                    permit.CompoundId,
                    permit.Compound.Name,
                    permit.Vendor.Name,
                    "Critical",
                    permit.Status.ToString(),
                    "Contractor is checked in after permit expiry.",
                    "Escalate to operations and require immediate checkout or permit extension review.",
                    permit.AllowedUntilUtc));
                continue;
            }

            if (permit.Status == ContractorWorkPermitStatus.PendingApproval
                && (permit.RiskLevel >= ContractorWorkPermitRiskLevel.High || permit.AllowedFromUtc <= nearWindow))
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "ContractorWorkPermit",
                    permit.Id,
                    permit.CompoundId,
                    permit.Compound.Name,
                    permit.Vendor.Name,
                    permit.RiskLevel >= ContractorWorkPermitRiskLevel.Critical ? "Critical" : "High",
                    permit.Status.ToString(),
                    "Contractor permit requires approval before work window.",
                    "Approve, deny, or reschedule the work permit before gate arrival.",
                    permit.AllowedFromUtc));
                continue;
            }

            if (permit.RequiresEscort && (permit.Status == ContractorWorkPermitStatus.Approved || permit.Status == ContractorWorkPermitStatus.CheckedIn))
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "ContractorWorkPermit",
                    permit.Id,
                    permit.CompoundId,
                    permit.Compound.Name,
                    permit.Vendor.Name,
                    permit.RiskLevel >= ContractorWorkPermitRiskLevel.High ? "High" : "Medium",
                    permit.Status.ToString(),
                    "Contractor permit requires escort control.",
                    "Assign or verify escort coverage before and during work.",
                    permit.CheckedInAtUtc ?? permit.AllowedFromUtc));
                continue;
            }

            if (includeInformational && permit.Status == ContractorWorkPermitStatus.Approved && permit.AllowedFromUtc <= nearWindow)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "ContractorWorkPermit",
                    permit.Id,
                    permit.CompoundId,
                    permit.Compound.Name,
                    permit.Vendor.Name,
                    "Low",
                    permit.Status.ToString(),
                    "Contractor permit is approved near work window.",
                    "Prepare gate verification.",
                    permit.AllowedFromUtc));
            }
        }

        foreach (var credential in credentialItems)
        {
            if (credential.Status == AccessCredentialStatus.Active && credential.ValidUntilUtc.HasValue && credential.ValidUntilUtc.Value < now)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "AccessCredential",
                    credential.Id,
                    credential.CompoundId,
                    credential.Compound.Name,
                    credential.OwnerDisplayName,
                    "Critical",
                    credential.Status.ToString(),
                    "Access credential is expired but still active.",
                    "Revoke or replace the credential immediately.",
                    credential.ValidUntilUtc.Value));
                continue;
            }

            if (credential.Status == AccessCredentialStatus.Lost)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "AccessCredential",
                    credential.Id,
                    credential.CompoundId,
                    credential.Compound.Name,
                    credential.OwnerDisplayName,
                    "High",
                    credential.Status.ToString(),
                    "Access credential is marked lost.",
                    "Revoke related access and issue replacement only after verification.",
                    credential.UpdatedAtUtc ?? credential.CreatedAtUtc));
                continue;
            }

            if (credential.Status == AccessCredentialStatus.Active && credential.ValidUntilUtc.HasValue && credential.ValidUntilUtc.Value <= expiringSoon)
            {
                items.Add(new AccessSecurityCommandQueueItemResponse(
                    "AccessCredential",
                    credential.Id,
                    credential.CompoundId,
                    credential.Compound.Name,
                    credential.OwnerDisplayName,
                    "Medium",
                    credential.Status.ToString(),
                    "Access credential is expiring soon.",
                    "Renew, replace, or let expire based on owner eligibility.",
                    credential.ValidUntilUtc.Value));
            }
        }

        return items.ToArray();
    }

    private static AccessCredentialRiskQueueItemResponse? ToCredentialRiskItem(
        AccessCredential item,
        DateTime now,
        DateTime expiringSoon)
    {
        if (item.Status == AccessCredentialStatus.Active && item.ValidUntilUtc.HasValue && item.ValidUntilUtc.Value < now)
        {
            return new AccessCredentialRiskQueueItemResponse(
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.CredentialType,
                item.OwnerType,
                item.OwnerDisplayName,
                MaskedCredentialCode,
                item.Status,
                item.ValidFromUtc,
                item.ValidUntilUtc,
                "Critical",
                "Credential is expired but still active.",
                "Revoke or replace the credential immediately.");
        }

        if (item.Status == AccessCredentialStatus.Active && item.ValidFromUtc > now)
        {
            return new AccessCredentialRiskQueueItemResponse(
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.CredentialType,
                item.OwnerType,
                item.OwnerDisplayName,
                MaskedCredentialCode,
                item.Status,
                item.ValidFromUtc,
                item.ValidUntilUtc,
                "Medium",
                "Credential is active before its validity window.",
                "Suspend access until the validity window starts or correct the dates.");
        }

        if (item.Status == AccessCredentialStatus.Lost)
        {
            return new AccessCredentialRiskQueueItemResponse(
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.CredentialType,
                item.OwnerType,
                item.OwnerDisplayName,
                MaskedCredentialCode,
                item.Status,
                item.ValidFromUtc,
                item.ValidUntilUtc,
                "High",
                "Credential is marked lost.",
                "Revoke related access and issue replacement only after verification.");
        }

        if (item.Status == AccessCredentialStatus.Suspended)
        {
            return new AccessCredentialRiskQueueItemResponse(
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.CredentialType,
                item.OwnerType,
                item.OwnerDisplayName,
                MaskedCredentialCode,
                item.Status,
                item.ValidFromUtc,
                item.ValidUntilUtc,
                "Medium",
                "Credential is suspended and requires operational review.",
                "Resolve suspension reason or revoke the credential.");
        }

        if (item.Status == AccessCredentialStatus.Active && item.ValidUntilUtc.HasValue && item.ValidUntilUtc.Value <= expiringSoon)
        {
            return new AccessCredentialRiskQueueItemResponse(
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.CredentialType,
                item.OwnerType,
                item.OwnerDisplayName,
                MaskedCredentialCode,
                item.Status,
                item.ValidFromUtc,
                item.ValidUntilUtc,
                "Medium",
                "Credential is expiring soon.",
                "Renew, replace, or let expire based on owner eligibility.");
        }

        return null;
    }

    private static ContractorEscortQueueItemResponse ToContractorEscortQueueItem(ContractorWorkPermit item)
    {
        var escortStatus = item.Status switch
        {
            ContractorWorkPermitStatus.CheckedIn when item.RequiresEscort => "EscortActiveVerificationRequired",
            ContractorWorkPermitStatus.Approved when item.RequiresEscort => "EscortRequiredBeforeEntry",
            _ when item.RiskLevel >= ContractorWorkPermitRiskLevel.High => "HighRiskEscortRecommended",
            _ => "Monitor"
        };

        var action = escortStatus switch
        {
            "EscortActiveVerificationRequired" => "Verify escort remains with contractor until checkout.",
            "EscortRequiredBeforeEntry" => "Assign escort before guard check-in.",
            "HighRiskEscortRecommended" => "Require supervisor review and escort decision.",
            _ => "Monitor permit during work window."
        };

        return new ContractorEscortQueueItemResponse(
            item.Id,
            item.CompoundId,
            item.Compound.Name,
            item.VendorId,
            item.Vendor.Name,
            item.Purpose,
            item.WorkArea,
            item.RiskLevel,
            item.Status,
            item.RequiresEscort,
            item.AllowedFromUtc,
            item.AllowedUntilUtc,
            item.CheckedInAtUtc,
            escortStatus,
            action);
    }

    private static IEnumerable<AccessAuditTrailItemResponse> BuildContractorAuditItems(
        ContractorWorkPermit item,
        IReadOnlyDictionary<Guid, string> guardNames)
    {
        if (item.AccessLogs.Count > 0)
        {
            foreach (var log in item.AccessLogs.OrderBy(log => log.CreatedAtUtc))
            {
                yield return new AccessAuditTrailItemResponse(
                    "ContractorWorkPermit",
                    item.Id,
                    item.CompoundId,
                    item.Compound.Name,
                    item.Vendor.Name,
                    log.Action.ToString(),
                    log.GuardUserId,
                    log.GuardUser?.FullName
                        ?? (log.GuardUserId.HasValue && guardNames.TryGetValue(log.GuardUserId.Value, out var guardName) ? guardName : null),
                    log.CreatedAtUtc,
                    log.Notes);
            }

            yield break;
        }

        if (item.CheckedInAtUtc.HasValue)
        {
            yield return new AccessAuditTrailItemResponse(
                "ContractorWorkPermit",
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.Vendor.Name,
                "CheckIn",
                item.GuardCheckedInByUserId,
                item.GuardCheckedInByUserId.HasValue && guardNames.TryGetValue(item.GuardCheckedInByUserId.Value, out var checkInName) ? checkInName : null,
                item.CheckedInAtUtc.Value,
                item.GuardNotes);
        }

        if (item.CheckedOutAtUtc.HasValue)
        {
            yield return new AccessAuditTrailItemResponse(
                "ContractorWorkPermit",
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.Vendor.Name,
                "CheckOut",
                item.GuardCheckedOutByUserId,
                item.GuardCheckedOutByUserId.HasValue && guardNames.TryGetValue(item.GuardCheckedOutByUserId.Value, out var checkOutName) ? checkOutName : null,
                item.CheckedOutAtUtc.Value,
                item.GuardNotes);
        }

        if (item.DeniedAtUtc.HasValue)
        {
            yield return new AccessAuditTrailItemResponse(
                "ContractorWorkPermit",
                item.Id,
                item.CompoundId,
                item.Compound.Name,
                item.Vendor.Name,
                "Denied",
                item.DeniedByUserId,
                item.DeniedByUserId.HasValue && guardNames.TryGetValue(item.DeniedByUserId.Value, out var deniedName) ? deniedName : null,
                item.DeniedAtUtc.Value,
                item.DenialReason);
        }
    }

    private static PagedResult<T> ToPagedInMemory<T>(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize)
    {
        var totalCount = items.Count;
        var pagedItems = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedResult<T>(pagedItems, pageNumber, pageSize, totalCount);
    }

    private static int AccessRiskRank(string riskLevel)
    {
        return riskLevel switch
        {
            "Critical" => 0,
            "High" => 1,
            "Medium" => 2,
            "Low" => 3,
            _ => 4
        };
    }

    private IQueryable<ContractorWorkPermit> GetPermitDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.ContractorWorkPermits
            .Include(item => item.Compound)
            .Include(item => item.Vendor)
            .Include(item => item.RelatedWorkOrder)
            .Include(item => item.AccessLogs)
                .ThenInclude(log => log.GuardUser)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private IQueryable<AccessCredential> GetCredentialDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.AccessCredentials
            .Include(item => item.Compound)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private static IQueryable<ContractorWorkPermit> ApplyPermitFilters(
        IQueryable<ContractorWorkPermit> query,
        ContractorWorkPermitQueryRequest request)
    {
        if (request.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == request.CompoundId.Value);
        }

        if (request.VendorId.HasValue)
        {
            query = query.Where(item => item.VendorId == request.VendorId.Value);
        }

        if (request.RelatedWorkOrderId.HasValue)
        {
            query = query.Where(item => item.RelatedWorkOrderId == request.RelatedWorkOrderId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        if (request.RiskLevel.HasValue)
        {
            query = query.Where(item => item.RiskLevel == request.RiskLevel.Value);
        }

        if (request.ActiveFromUtc.HasValue)
        {
            query = query.Where(item => item.AllowedUntilUtc >= request.ActiveFromUtc.Value);
        }

        if (request.ActiveUntilUtc.HasValue)
        {
            query = query.Where(item => item.AllowedFromUtc <= request.ActiveUntilUtc.Value);
        }

        var searchTerm = TrimOrNull(request.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item => item.Purpose.Contains(searchTerm)
                || item.WorkArea.Contains(searchTerm)
                || item.Vendor.Name.Contains(searchTerm));
        }

        return query;
    }

    private static IQueryable<AccessCredential> ApplyCredentialFilters(
        IQueryable<AccessCredential> query,
        AccessCredentialQueryRequest request)
    {
        if (request.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == request.CompoundId.Value);
        }

        if (request.CredentialType.HasValue)
        {
            query = query.Where(item => item.CredentialType == request.CredentialType.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        if (request.OwnerType.HasValue)
        {
            query = query.Where(item => item.OwnerType == request.OwnerType.Value);
        }

        if (request.OwnerEntityId.HasValue)
        {
            query = query.Where(item => item.OwnerEntityId == request.OwnerEntityId.Value);
        }

        var searchTerm = TrimOrNull(request.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item => item.OwnerDisplayName.Contains(searchTerm));
        }

        return query;
    }

    private async Task<PagedResult<ContractorWorkPermitResponse>> ToPagedPermitResultAsync(
        IQueryable<ContractorWorkPermit> query,
        ContractorWorkPermitQueryRequest pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var permitItems = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = permitItems.Select(ToPermitResponse).ToArray();

        return new PagedResult<ContractorWorkPermitResponse>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    private async Task<ServiceResult<ContractorWorkPermit>> GetScopedPermitForMutationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var permits = await ApplyCurrentCompoundScopeAsync(
            GetPermitDetailsQuery(asNoTracking: false),
            cancellationToken);
        var permit = await permits.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return permit is null
            ? ServiceResult<ContractorWorkPermit>.NotFound("Contractor work permit was not found.")
            : ServiceResult<ContractorWorkPermit>.Success(permit);
    }

    private static ValidationFailure? ValidateCreatePermitRequest(CreateContractorWorkPermitRequest request)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        if (request.VendorId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor id is required.");
        }

        if (TrimOrNull(request.Purpose) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Permit purpose is required.");
        }

        if (TrimOrNull(request.WorkArea) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Work area is required.");
        }

        if (!Enum.IsDefined(request.RiskLevel))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Risk level is invalid.");
        }

        if (request.AllowedUntilUtc <= request.AllowedFromUtc)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Allowed until must be after allowed from.");
        }

        if (request.AllowedUntilUtc <= DateTime.UtcNow)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Contractor work permit validity must end in the future.");
        }

        return null;
    }

    private static ValidationFailure? ValidateCreateCredentialRequest(CreateAccessCredentialRequest request)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        if (TrimOrNull(request.OwnerDisplayName) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Owner display name is required.");
        }

        if (!Enum.IsDefined(request.CredentialType))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Credential type is invalid.");
        }

        if (!Enum.IsDefined(request.OwnerType))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Credential owner type is invalid.");
        }

        if (request.ValidUntilUtc.HasValue && request.ValidUntilUtc.Value <= request.ValidFromUtc)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Credential valid until must be after valid from.");
        }

        return null;
    }

    private static Task<string> GenerateUniqueCredentialCodeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"AC-{DateTime.UtcNow:yyyyMMdd}-{GenerateSecureCodeSegment(12)}");
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

    private void AddContractorAccessLog(
        ContractorWorkPermit permit,
        ContractorAccessAction action,
        Guid? guardUserId,
        string? notes)
    {
        dbContext.ContractorAccessLogs.Add(new ContractorAccessLog
        {
            ContractorWorkPermitId = permit.Id,
            GuardUserId = guardUserId,
            Action = action,
            Notes = TrimOrNull(notes)
        });
    }

    private async Task<IQueryable<ContractorWorkPermit>> ApplyCurrentCompoundScopeAsync(
        IQueryable<ContractorWorkPermit> query,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return query.ApplyCompoundAccess(scope, item => item.CompoundId);
    }

    private async Task<IQueryable<AccessCredential>> ApplyCurrentCompoundScopeAsync(
        IQueryable<AccessCredential> query,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return query.ApplyCompoundAccess(scope, item => item.CompoundId);
    }



    private async Task<IQueryable<VisitorPass>> ApplyCurrentCompoundScopeAsync(
        IQueryable<VisitorPass> query,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return query.ApplyCompoundAccess(scope, item => item.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private async Task<IQueryable<ContractorWorkPermit>> ApplyGuardCompoundScopeAsync(
        IQueryable<ContractorWorkPermit> query,
        Guid? guardUserId,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        if (!guardUserId.HasValue)
        {
            return query.Where(_ => false);
        }

        var compoundIds = await compoundAccessService.GetAllowedCompoundIdsForUserRoleAsync(
            guardUserId.Value,
            UserRole.Guard,
            cancellationToken);

        return compoundIds.Length == 0
            ? query.Where(_ => false)
            : query.Where(item => compoundIds.Contains(item.CompoundId));
    }

    private async Task<bool> CanGuardAccessCompoundAsync(
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

    private static ContractorWorkPermitResponse ToPermitResponse(ContractorWorkPermit item)
    {
        return new ContractorWorkPermitResponse(
            item.Id,
            item.CompoundId,
            item.Compound.Name,
            item.VendorId,
            item.Vendor.Name,
            item.RelatedWorkOrderId,
            item.Purpose,
            item.WorkArea,
            item.EquipmentList,
            item.RiskLevel,
            item.Status,
            item.AllowedFromUtc,
            item.AllowedUntilUtc,
            item.RequiresEscort,
            item.CreatedByUserId,
            item.ApprovedByUserId,
            item.ApprovedAtUtc,
            item.DeniedByUserId,
            item.DeniedAtUtc,
            item.DenialReason,
            item.GuardCheckedInByUserId,
            item.CheckedInAtUtc,
            item.GuardCheckedOutByUserId,
            item.CheckedOutAtUtc,
            item.GuardNotes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static AccessCredentialResponse ToCredentialResponse(AccessCredential item, string? displayOnceCredentialCode = null)
    {
        return new AccessCredentialResponse(
            item.Id,
            item.CompoundId,
            item.Compound.Name,
            item.CredentialType,
            item.Status,
            item.OwnerType,
            item.OwnerEntityId,
            item.OwnerDisplayName,
            displayOnceCredentialCode ?? MaskedCredentialCode,
            item.ValidFromUtc,
            item.ValidUntilUtc,
            item.SourceVisitorPassId,
            item.SourceContractorWorkPermitId,
            item.RevokedByUserId,
            item.RevokedAtUtc,
            item.RevocationReason,
            item.Notes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static ServiceResult<ContractorWorkPermitResponse> ToPermitResponseResult(
        ServiceResult<ContractorWorkPermit> result)
    {
        return result.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<ContractorWorkPermitResponse>.NotFound(result.Message ?? "Contractor work permit was not found."),
            ServiceResultStatus.Conflict => ServiceResult<ContractorWorkPermitResponse>.Conflict(result.Message ?? "Contractor work permit conflict."),
            ServiceResultStatus.Forbidden => ServiceResult<ContractorWorkPermitResponse>.Forbidden(result.Message ?? "Forbidden."),
            ServiceResultStatus.BadRequest => ServiceResult<ContractorWorkPermitResponse>.BadRequest(result.Message ?? "Contractor work permit request is invalid."),
            _ => ServiceResult<ContractorWorkPermitResponse>.BadRequest(result.Message ?? "Contractor work permit request failed.")
        };
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

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}

