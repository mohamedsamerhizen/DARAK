using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface IIntelligenceEscalationService
{
    Task<ServiceResult<IntelligenceEscalationDashboardResponse>> GetCompoundEscalationDashboardAsync(
        Guid compoundId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IntelligenceEscalationQueueResponse>> GetCompoundEscalationQueueAsync(
        Guid compoundId,
        string? area = null,
        string? severity = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentDecisionBriefResponse>> GetResidentDecisionBriefAsync(
        Guid residentId,
        CancellationToken cancellationToken = default);
}
