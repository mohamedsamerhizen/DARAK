using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed class PaymentReconciliationBatchSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public string? Provider { get; init; }

    public PaymentReconciliationBatchStatus? Status { get; init; }

    public DateOnly? StatementDateFrom { get; init; }

    public DateOnly? StatementDateTo { get; init; }
}
