# Financial Model Repair

This remediation pass closes the main financial consistency gaps for payments, ledger entries, idempotency, balance-forward handling, and sale-plan money movement.

## Implemented

- Manual payments accept an `IdempotencyKey` and replay the same successful payment instead of creating a duplicate.
- Manual payment idempotency keys conflict when reused for a different target, method, or amount.
- Mock provider confirmations generate deterministic provider transaction ids when the client omits one.
- Provider transaction ids are unique per provider across payments.
- Payment-plan installment payments now create a succeeded payment, receipt, payment attempt, and resident ledger credit.
- Cash property sale contracts now create a payment, receipt, receivable debit, and payment credit.
- Installment sale down payments now create a payment, receipt, receivable debit, and payment credit.
- Utility and rent previous-balance calculations subtract already-carried previous-balance amounts to prevent cascading double-counts.

## Source Types

`PaymentTargetType` now includes:

- `PaymentPlanInstallment`
- `PropertySaleContract`

`FinancialLedgerSourceType` now includes:

- `PaymentPlanInstallment`
- `PropertySaleContract`

## Tests

Coverage includes manual payment idempotency replay/conflict, mock provider transaction generation/conflict, payment-plan payment records, property sale cash/down-payment ledger records, and balance-forward non-cascading behavior.
