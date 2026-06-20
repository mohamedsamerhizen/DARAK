using DARAK.Api.DTOs.Communication;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface IConversationAdvisoryService
{
    ConversationPriority GetDefaultPriority(ConversationIssueType issueType);

    IReadOnlyList<ConversationAdvisoryFlagResponse> GetAdvisoryFlags(
        ConversationIssueType issueType,
        ConversationLinkedEntityType linkedEntityType = ConversationLinkedEntityType.None);
}
