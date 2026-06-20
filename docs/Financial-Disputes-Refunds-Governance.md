# DARAK Financial Disputes and Refunds Governance

## Purpose

This document defines the commercial governance rules for resident billing disputes, payment objections, refunds, and financial corrections in DARAK.

Phase 9 does not add demo data, seed data, or a database migration. It formalizes how the existing financial workflows must be reviewed, tested, and handed over.

## Scope

Covered financial flows:

- Resident bill disputes.
- Admin dispute review.
- Payment correction and refund governance.
- Audit traceability for financial actions.
- Compound-scoped access for financial operations.

Not covered in this phase:

- Real payment gateway integration.
- New accounting ledger schema.
- New partial-refund database model unless explicitly approved later.
- Manual edits to production financial records outside audited services.

## Dispute Workflow Rule

A dispute should follow a controlled lifecycle:

```text
Open -> UnderReview -> Resolved
Open -> UnderReview -> Rejected
Open -> Cancelled
```

A resident should not be able to open duplicate active disputes for the same bill or payment target. If the current database model cannot enforce this completely, the limitation must be declared in the handoff notes and covered by service-level validation where available.

## Refund Governance Rule

Refunds and financial corrections are sensitive operations. The minimum expected governance is:

- Only authorized admin roles may initiate or approve refund-related actions.
- Refunds must be compound-scoped.
- Refunds must not exceed the original paid amount.
- Partial refunds require explicit business approval before a schema expansion is introduced.
- Every accepted, rejected, or cancelled financial correction should be auditable.

## Audit Requirements

Financial disputes and refunds must be traceable by:

- Actor user id.
- Actor role.
- Compound id.
- Target entity type and id.
- Timestamp.
- Reason or resolution summary.
- Correlation id when available.

## Commercial Handoff Notes

For a buyer or reviewer, declare the current state clearly:

- The project includes strong financial foundations: billing, payments, receipts, disputes, audit, and scoping.
- Real payment gateway settlement is intentionally mocked/configurable.
- Any live refund integration should be added only after gateway selection and legal/accounting requirements are known.

## Verification Checklist

- Build succeeds.
- Tests succeed.
- Resident financial access remains resident-scoped.
- Admin financial access remains compound-scoped.
- Financial operations reject unauthorized roles.
- No live credentials exist in source code.
