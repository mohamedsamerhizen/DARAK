using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;

namespace DARAK.Api.Interfaces;

public interface IFinancialGovernanceService
{
    Task<ServiceResult<PagedResult<FinancialDisputeResponse>>> SearchFinancialDisputesAsync(
        FinancialDisputeSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialDisputeResponse>> GetFinancialDisputeAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialDisputeResponse>> CreateFinancialDisputeAsync(
        Guid? currentUserId,
        CreateFinancialDisputeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialDisputeResponse>> CreateResidentFinancialDisputeAsync(
        Guid currentUserId,
        CreateResidentFinancialDisputeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<FinancialDisputeResponse>>> SearchResidentFinancialDisputesAsync(
        Guid currentUserId,
        FinancialDisputeSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialDisputeResponse>> GetResidentFinancialDisputeAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentFinancialGovernanceSummaryResponse>> GetResidentFinancialGovernanceSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminFinancialGovernanceSummaryResponse>> GetAdminFinancialGovernanceSummaryAsync(
        FinancialGovernanceSummaryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminResidentFinancialGovernanceSnapshotResponse>> GetAdminResidentFinancialGovernanceSnapshotAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialDisputeResponse>> TransitionFinancialDisputeAsync(
        Guid? currentUserId,
        Guid id,
        TransitionFinancialDisputeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialDisputeResponse>> CreateAdjustmentForFinancialDisputeAsync(
        Guid? currentUserId,
        Guid id,
        CreateGovernanceFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ViolationAppealResponse>>> SearchViolationAppealsAsync(
        ViolationAppealSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationAppealResponse>> GetViolationAppealAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationAppealResponse>> CreateViolationAppealAsync(
        Guid? currentUserId,
        CreateViolationAppealRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationAppealResponse>> CreateResidentViolationAppealAsync(
        Guid currentUserId,
        CreateResidentViolationAppealRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ViolationAppealResponse>>> SearchResidentViolationAppealsAsync(
        Guid currentUserId,
        ViolationAppealSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationAppealResponse>> GetResidentViolationAppealAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationAppealResponse>> TransitionViolationAppealAsync(
        Guid? currentUserId,
        Guid id,
        TransitionViolationAppealRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ViolationAppealResponse>> CreateAdjustmentForViolationAppealAsync(
        Guid? currentUserId,
        Guid id,
        CreateGovernanceFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default);
}
