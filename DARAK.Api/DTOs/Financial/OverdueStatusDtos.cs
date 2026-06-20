using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Financial;

public sealed class ProcessOverdueStatusRequest
{
    public Guid CompoundId { get; init; }

    public DateOnly? AsOfDate { get; init; }
}

public sealed record ProcessOverdueStatusResponse(
    Guid CompoundId,
    DateOnly AsOfDate,
    int UtilityBillsUpdated,
    int RentInvoicesUpdated,
    int InstallmentsUpdated);
