namespace DARAK.Api.Entities;

public sealed class CommunityPollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PollId { get; set; }

    public string Text { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CommunityPoll Poll { get; set; } = null!;

    public ICollection<CommunityPollVote> Votes { get; set; } = new List<CommunityPollVote>();
}
