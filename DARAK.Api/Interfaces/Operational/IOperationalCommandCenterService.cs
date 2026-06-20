using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operational;

namespace DARAK.Api.Interfaces;

public interface IOperationalCommandCenterService
{
    Task<ServiceResult<AdminCommandCenterIntelligenceResponse>> GetIntelligenceAsync(
        AdminCommandCenterIntelligenceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ExecutiveDailySummaryResponse>> GetExecutiveDailySummaryAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DomainSignalBoardResponse>> GetDomainSignalBoardAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CriticalActionQueueResponse>> GetCriticalActionQueueAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalCommandCenterResponse>> GetCommandCenterAsync(
        OperationalCommandCenterQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<SlaBreachResponse>>> GetSlaBreachesAsync(
        SlaBreachQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StaffPerformanceResponse>> GetStaffPerformanceAsync(
        StaffPerformanceQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundHealthResponse>> GetCompoundHealthAsync(
        CompoundHealthQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<OperationalTaskResponse>>> SearchTasksAsync(
        OperationalTaskSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalTaskResponse>> GetTaskAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalTaskResponse>> CreateTaskAsync(
        Guid? currentUserId,
        CreateOperationalTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalTaskResponse>> CompleteTaskAsync(
        Guid? currentUserId,
        Guid id,
        CompleteOperationalTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationalTaskResponse>> CancelTaskAsync(
        Guid? currentUserId,
        Guid id,
        CancelOperationalTaskRequest request,
        CancellationToken cancellationToken = default);
}
