namespace DARAK.Api.DTOs.UtilityBills;

public sealed record UtilityBillLineResponse(
    Guid Id,
    Guid UtilityBillId,
    Guid CompoundServiceId,
    string CompoundServiceName,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    DateTime CreatedAt);
