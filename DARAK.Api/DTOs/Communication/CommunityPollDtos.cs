using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed record CommunityPollOptionResponse(
    Guid Id,
    string Text,
    int DisplayOrder);

public sealed record CommunityPollResponse(
    Guid Id,
    string Question,
    string? Description,
    CommunityPollStatus Status,
    Guid CompoundId,
    DateTime StartsAt,
    DateTime EndsAt,
    bool AllowsMultipleChoices,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<CommunityPollOptionResponse> Options,
    IReadOnlyCollection<Guid> SelectedOptionIds);

public sealed record CommunityPollResultResponse(
    Guid PollId,
    string Question,
    CommunityPollStatus Status,
    int TotalVotes,
    int TotalVoters,
    IReadOnlyCollection<CommunityPollOptionResultResponse> Options);

public sealed record CommunityPollOptionResultResponse(
    Guid OptionId,
    string Text,
    int DisplayOrder,
    int VoteCount,
    decimal VotePercentage);

public sealed class CommunityPollSearchQuery : PaginationQuery
{
    public CommunityPollStatus? Status { get; init; }

    public DateTime? StartsFrom { get; init; }

    public DateTime? EndsTo { get; init; }

    public Guid? CompoundId { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateCommunityPollOptionRequest
{
    [Required]
    [MaxLength(300)]
    public string Text { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}

public sealed class CreateCommunityPollRequest
{
    [Required]
    [MaxLength(300)]
    public string Question { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    public Guid? CompoundId { get; init; }

    public bool AllowsMultipleChoices { get; init; }

    [MinLength(2)]
    public List<CreateCommunityPollOptionRequest> Options { get; init; } = [];
}

public sealed class UpdateCommunityPollRequest
{
    [Required]
    [MaxLength(300)]
    public string Question { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    public Guid? CompoundId { get; init; }

    public bool AllowsMultipleChoices { get; init; }

    [MinLength(2)]
    public List<CreateCommunityPollOptionRequest> Options { get; init; } = [];
}

public sealed class SubmitCommunityPollVoteRequest
{
    [MinLength(1)]
    public List<Guid> PollOptionIds { get; init; } = [];
}
