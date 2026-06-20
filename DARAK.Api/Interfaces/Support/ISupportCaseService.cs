using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Support;

namespace DARAK.Api.Interfaces;

public interface ISupportCaseService
{
    Task<ServiceResult<SupportCaseResponse>> CreateCaseAsync(Guid? currentUserId, CreateSupportCaseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<SupportCaseResponse>>> SearchCasesAsync(SupportCaseSearchQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportCaseDetailsResponse>> GetCaseAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportCaseResponse>> AssignCaseAsync(Guid? currentUserId, Guid id, AssignSupportCaseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportCaseResponse>> EscalateCaseAsync(Guid? currentUserId, Guid id, EscalateSupportCaseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportCaseResponse>> ResolveCaseAsync(Guid? currentUserId, Guid id, ResolveSupportCaseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportCaseResponse>> ReopenCaseAsync(Guid? currentUserId, Guid id, ReopenSupportCaseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportCaseDetailsResponse>> AddNoteAsync(Guid? currentUserId, Guid id, AddSupportCaseNoteRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportDashboardResponse>> GetDashboardAsync(SupportDashboardQuery query, CancellationToken cancellationToken = default);
}
