using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class UtilityOutageUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UtilityOutageId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public UtilityOutageUpdateType UpdateType { get; set; } = UtilityOutageUpdateType.Information;

    public string Message { get; set; } = string.Empty;

    public DateTime? NewEstimatedEndAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public UtilityOutage UtilityOutage { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }
}
