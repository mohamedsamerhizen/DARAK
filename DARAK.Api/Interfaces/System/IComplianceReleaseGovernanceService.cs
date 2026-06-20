using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface IComplianceReleaseGovernanceService
{
    Task<ServiceResult<ReleaseReadinessBoardResponse>> GetReleaseReadinessBoardAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuditEvidenceDashboardResponse>> GetAuditEvidenceDashboardAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ComplianceExceptionQueueResponse>> GetComplianceExceptionQueueAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BuyerHandoffReadinessResponse>> GetBuyerHandoffReadinessAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<GovernanceTimelineResponse>> GetGovernanceTimelineAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default);
}
