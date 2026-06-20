using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;

namespace DARAK.Api.Interfaces;

public interface IAccessControlOperationsService
{
    Task<ServiceResult<AccessControlOperationsSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AccessControlProDashboardResponse>> GetProDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccessSecurityCommandQueueItemResponse>> GetSecurityCommandQueueAsync(
        AccessSecurityCommandQueueQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccessCredentialRiskQueueItemResponse>> GetCredentialRiskQueueAsync(
        AccessCredentialRiskQueueQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ContractorEscortQueueItemResponse>> GetContractorEscortQueueAsync(
        ContractorEscortQueueQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccessAuditTrailItemResponse>> GetAccessAuditTrailAsync(
        AccessAuditTrailQueryRequest query,
        CancellationToken cancellationToken = default);


    Task<ServiceResult<AccessGateSituationReportResponse>> GetGateSituationReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<VisitorVerificationBoardItemResponse>> GetVisitorVerificationBoardAsync(
        VisitorVerificationBoardQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ContractorAccessComplianceBoardItemResponse>> GetContractorAccessComplianceBoardAsync(
        ContractorAccessComplianceBoardQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccessCredentialControlBoardItemResponse>> GetCredentialControlBoardAsync(
        AccessCredentialControlBoardQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<GuardShiftHandoverReportResponse>> GetGuardShiftHandoverReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);


    Task<PagedResult<ContractorWorkPermitResponse>> SearchContractorWorkPermitsAsync(
        ContractorWorkPermitQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ContractorWorkPermitResponse>> SearchTodayContractorWorkPermitsForGuardAsync(
        Guid? guardUserId,
        ContractorWorkPermitQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractorWorkPermitResponse>> GetContractorWorkPermitAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractorWorkPermitResponse>> CreateContractorWorkPermitAsync(
        Guid? createdByUserId,
        CreateContractorWorkPermitRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractorWorkPermitResponse>> ApproveContractorWorkPermitAsync(
        Guid id,
        Guid? approvedByUserId,
        ContractorPermitDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractorWorkPermitResponse>> DenyContractorWorkPermitAsync(
        Guid id,
        Guid? deniedByUserId,
        DenyContractorWorkPermitRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractorWorkPermitResponse>> GuardCheckInContractorWorkPermitAsync(
        Guid id,
        Guid? guardUserId,
        GuardContractorPermitAccessRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ContractorWorkPermitResponse>> GuardCheckOutContractorWorkPermitAsync(
        Guid id,
        Guid? guardUserId,
        GuardContractorPermitAccessRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccessCredentialResponse>> SearchAccessCredentialsAsync(
        AccessCredentialQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AccessCredentialResponse>> CreateAccessCredentialAsync(
        CreateAccessCredentialRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AccessCredentialResponse>> RevokeAccessCredentialAsync(
        Guid id,
        Guid? revokedByUserId,
        RevokeAccessCredentialRequest request,
        CancellationToken cancellationToken = default);
}
