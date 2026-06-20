using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Governance;

namespace DARAK.Api.Interfaces;

public interface IGovernanceArbitrationService
{
    Task<ServiceResult<ArbitrationCaseSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ArbitrationCaseResponse>> SearchCasesAsync(
        ArbitrationCaseQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ArbitrationCaseResponse>> GetCaseAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ArbitrationCaseResponse>> CreateCaseAsync(
        Guid? currentUserId,
        CreateArbitrationCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ArbitrationCaseResponse>> AddEventAsync(
        Guid id,
        Guid? currentUserId,
        AddArbitrationCaseEventRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ArbitrationCaseResponse>> IssueFinalDecisionAsync(
        Guid id,
        Guid? currentUserId,
        IssueArbitrationFinalDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ArbitrationCaseResponse>> CancelCaseAsync(
        Guid id,
        Guid? currentUserId,
        CancelArbitrationCaseRequest request,
        CancellationToken cancellationToken = default);
}
