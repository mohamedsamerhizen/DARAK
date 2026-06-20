using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Payments;

public sealed class CreatePaymentReconciliationBatchRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(80)]
    public string Provider { get; init; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string StatementReference { get; init; } = string.Empty;

    public DateOnly StatementDate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<CreatePaymentReconciliationItemRequest> Items { get; init; } = [];
}
