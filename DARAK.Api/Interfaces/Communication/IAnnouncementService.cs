using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface IAnnouncementService
{
    Task<PagedResult<AnnouncementResponse>> SearchAnnouncementsAsync(AnnouncementSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<AnnouncementResponse>>> SearchActiveAnnouncementsAsync(AnnouncementSearchQuery query, Guid? currentUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AnnouncementResponse>> GetAnnouncementAsync(Guid id, Guid? currentUserId, bool isManager, CancellationToken cancellationToken = default);
    Task<ServiceResult<AnnouncementResponse>> CreateAnnouncementAsync(Guid? currentUserId, CreateAnnouncementRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AnnouncementResponse>> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AnnouncementResponse>> PublishAnnouncementAsync(Guid id, PublishAnnouncementRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AnnouncementResponse>> ArchiveAnnouncementAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<AnnouncementReadReceiptResponse>> MarkAnnouncementAsReadAsync(Guid id, Guid? currentUserId, CancellationToken cancellationToken = default);
}
