using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface IAuditLogService
{
    Task<Guid> AppendEntryAsync(AuditLogRecord record, CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<AuditLogResponse>>> SearchAsync(
        AuditSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuditLogDetailsResponse>> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<AuditLogResponse>>> GetEntityTrailAsync(
        AuditEntityType entityType,
        Guid entityId,
        AuditEntityTrailQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<AuditLogResponse>>> GetResidentTrailAsync(
        Guid residentProfileId,
        AuditEntityTrailQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuditDashboardResponse>> GetDashboardAsync(
        AuditDashboardQuery query,
        CancellationToken cancellationToken = default);
}
