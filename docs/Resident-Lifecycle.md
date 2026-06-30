# Resident Lifecycle

This remediation pass hardens resident account visibility and move-out financial clearance.

## Implemented

- Resident-facing account statements are available through the resident account service path.
- A resident user with one active profile can fetch a statement without specifying a profile id.
- A resident user with multiple active profiles must specify `residentProfileId`.
- A resident user cannot fetch another user's resident profile statement.
- Move-out financial clearance is rechecked at confirmation time.
- Move-out completion revalidates financial clearance even if clearance was confirmed earlier.
- Financial blockers include unpaid utility bills, unpaid rent invoices, unpaid sale installments, unpaid payment-plan installments, unpaid fines, active financial disputes, active collection cases, and active legal notices.
- Move-out readiness now includes payment-plan installments as financial items.

## Tests

Coverage includes resident statement single-profile access, multi-profile selection, cross-user denial, hard financial-clearance confirmation blocking, and completion-time revalidation after a new unpaid balance appears.
