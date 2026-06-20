using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface ICommunityPollService
{
    Task<PagedResult<CommunityPollResponse>> SearchPollsAsync(CommunityPollSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<CommunityPollResponse>>> SearchOpenPollsAsync(CommunityPollSearchQuery query, Guid? currentUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> GetPollAsync(Guid id, Guid? currentUserId, bool isManager, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> CreatePollAsync(Guid? currentUserId, CreateCommunityPollRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> UpdatePollAsync(Guid id, UpdateCommunityPollRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> OpenPollAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> ClosePollAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> ArchivePollAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResponse>> SubmitVoteAsync(Guid id, Guid? currentUserId, SubmitCommunityPollVoteRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<CommunityPollResultResponse>> GetPollResultsAsync(Guid id, CancellationToken cancellationToken = default);
}
