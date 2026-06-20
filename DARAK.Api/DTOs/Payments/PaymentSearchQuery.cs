using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed class PaymentSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public PaymentTargetType? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public PaymentStatus? PaymentStatus { get; init; }

    public DateTime? CreatedFrom { get; init; }

    public DateTime? CreatedTo { get; init; }
}
