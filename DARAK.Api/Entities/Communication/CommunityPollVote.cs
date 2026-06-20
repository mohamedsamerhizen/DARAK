using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class CommunityPollVote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PollId { get; set; }

    public Guid PollOptionId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CommunityPoll Poll { get; set; } = null!;

    public CommunityPollOption PollOption { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
