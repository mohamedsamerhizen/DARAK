using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;

namespace DARAK.Api.Interfaces;

public interface IDocumentManagementService
{
    Task<ServiceResult<DocumentManagementDashboardResponse>> GetDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentComplianceReportResponse>> GetComplianceReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<DocumentRequirementResponse>>> SearchRequirementsAsync(
        DocumentRequirementSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentRequirementResponse>> GetRequirementAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentRequirementResponse>> CreateRequirementAsync(
        Guid? currentUserId,
        CreateDocumentRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentRequirementResponse>> UpdateRequirementAsync(
        Guid id,
        UpdateDocumentRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentRequirementResponse>> DeactivateRequirementAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentFileResponse>> ApproveDocumentAsync(
        Guid? currentUserId,
        Guid documentId,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentFileResponse>> RejectDocumentAsync(
        Guid? currentUserId,
        Guid documentId,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentDocumentChecklistResponse>> GetResidentChecklistAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default);
}
