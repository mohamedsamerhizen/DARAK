using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class CommunityPoll
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Question { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CommunityPollStatus Status { get; set; } = CommunityPollStatus.Draft;

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public bool AllowsMultipleChoices { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<CommunityPollOption> Options { get; set; } = new List<CommunityPollOption>();

    public ICollection<CommunityPollVote> Votes { get; set; } = new List<CommunityPollVote>();
}
