using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface IConversationService
{
    Task<ServiceResult<ConversationResponse>> CreateConversationAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentConversationResponse>> OpenResidentConversationAsync(
        Guid? currentUserId,
        ResidentOpenConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ResidentConversationResponse>>> SearchResidentConversationsAsync(
        Guid? currentUserId,
        ConversationSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentConversationResponse>> GetResidentConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentConversationResponse>> AddResidentMessageAsync(
        Guid? currentUserId,
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentConversationResponse>> ReopenResidentConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        ReopenConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentBillDisputeResponse>> OpenResidentBillDisputeAsync(
        Guid? currentUserId,
        Guid billId,
        ResidentBillDisputeRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ConversationResponse>>> SearchAdminConversationsAsync(
        Guid? currentUserId,
        ConversationSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminConversationDetailsResponse>> GetAdminConversationDetailsAsync(
        Guid? currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> AddAdminReplyAsync(
        Guid? currentUserId,
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> AssignConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        AssignConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> ChangePriorityAsync(
        Guid? currentUserId,
        Guid conversationId,
        ChangeConversationPriorityRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> EscalateConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        EscalateConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> AddInternalNoteAsync(
        Guid? currentUserId,
        Guid conversationId,
        AddInternalNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> ResolveConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CompleteConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ConversationResponse>> CloseConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CompleteConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportDashboardResponse>> GetSupportDashboardAsync(
        Guid? currentUserId,
        SupportDashboardQuery query,
        CancellationToken cancellationToken = default);
}
