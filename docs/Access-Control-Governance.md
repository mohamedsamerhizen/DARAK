# Access Control Governance

Status: Phase 6 remediation implemented on 2026-06-30.

## Scope

This phase covers visitor passes, guard verification, contractor work permits, access credentials, and access audit evidence. It does not include physical device integration, real SMS/email delivery, or frontend/mobile guard screens.

## Implemented Controls

- Visitor pass access codes are stored as one-way hashes in `VisitorPasses.AccessCode`.
- Access credentials are stored as one-way hashes in `AccessCredentials.CredentialCode`.
- New generated hashes use salted `AC2$...` values.
- The migration converts existing plaintext visitor/access credential values to deterministic `SHA256HEX$...` hashes.
- Raw visitor/access credential codes are shown only in creation responses.
- Admin, resident, guard, search, risk queue, and control board responses mask access/credential codes as `********`.
- Guard visitor verification compares submitted codes against the stored hash and creates a `Verified` access log on success.
- Wrong visitor codes fail and create a `CredentialFailed` log without storing the submitted secret.
- Contractor guard check-in now requires an active, valid, permit-scoped access credential.
- Contractor wrong-code attempts create a `CredentialFailed` contractor access log without storing the submitted secret.
- Contractor check-in, check-out, and denial events are written to `ContractorAccessLogs`.

## API Impact

- `POST /api/guard/access/contractors/{id}/check-in` now requires `accessCode` in the request body.
- Access/credential response DTOs still include the existing string fields, but normal reads now return `********`.
- Creation responses may include the raw display-once code.

## Migration Impact

Migration: `20260630133311_HashAccessCodesContractorAuditMaintenanceInventoryGuards`.

The migration adds `ContractorAccessLogs` and hashes existing values in `VisitorPasses.AccessCode` and `AccessCredentials.CredentialCode`. Hashing is one-way; down migration does not restore plaintext codes.

## Remaining Limitations

- No physical gate-device integration.
- No real notification delivery of display-once access codes.
- SQL Server migration application was verified, but broader real gate-device and operational security evidence is still outside this backend phase.
