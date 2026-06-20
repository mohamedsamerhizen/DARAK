using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;

namespace DARAK.Api.Interfaces;

public interface IDocumentService
{
    Task<ServiceResult<DocumentFileResponse>> UploadDocumentAsync(
        Guid? currentUserId,
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentFileResponse>> UpdateMetadataAsync(
        Guid id,
        UpdateDocumentMetadataRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentFileResponse>> GetDocumentAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<DocumentFileResponse>>> SearchDocumentsAsync(
        DocumentQueryRequest query,
        Guid? currentUserId,
        bool isManager,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentDownloadResponse>> DownloadDocumentAsync(
        Guid id,
        Guid? currentUserId,
        bool isManager,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> SoftDeleteDocumentAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<DocumentAccessLogResponse>>> GetAccessLogsAsync(
        Guid documentFileId,
        DocumentAccessLogQueryRequest query,
        CancellationToken cancellationToken = default);
}
