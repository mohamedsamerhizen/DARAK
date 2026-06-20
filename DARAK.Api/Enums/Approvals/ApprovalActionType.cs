namespace DARAK.Api.Enums;

public enum ApprovalActionType
{
    RefundPayment = 1,
    CancelPayment = 2,
    WaiveViolationFine = 3,
    AdjustUtilityBill = 4,
    DeleteSensitiveDocument = 5,
    CloseEscalatedDispute = 6,
    ManualFinancialCorrection = 7,
    CancelHighValuePayment = 8,
    OverrideResidentFinancialState = 9,
    OtherSensitiveAdminOperation = 10
}
