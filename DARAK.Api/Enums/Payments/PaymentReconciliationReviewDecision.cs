namespace DARAK.Api.Enums;

public enum PaymentReconciliationReviewDecision
{
    None = 0,
    AcceptedAsProviderException = 1,
    RequiresDarakCorrection = 2,
    RequiresProviderCorrection = 3,
    DuplicateProviderRecord = 4,
    IgnoredAsNonFinancial = 5
}
