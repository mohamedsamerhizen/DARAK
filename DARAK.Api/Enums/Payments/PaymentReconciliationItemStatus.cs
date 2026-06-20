namespace DARAK.Api.Enums;

public enum PaymentReconciliationItemStatus
{
    Matched = 1,
    MissingInDarak = 2,
    CompoundMismatch = 3,
    StatusMismatch = 4,
    AmountMismatch = 5,
    ReceiptMissing = 6,
    LedgerEntryMissing = 7
}
