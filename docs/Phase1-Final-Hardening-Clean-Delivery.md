# DARAK Phase 1 — Final Hardening & Clean Delivery

This phase freezes feature expansion and prepares DARAK for serious commercial review.

## Scope

- Remove local patch/hotfix artifacts from the repository root.
- Keep only final product documentation in `docs/` and the main `README.md`.
- Block local ZIPs, backups, logs, coverage, `.env`, build outputs, and temporary extraction folders.
- Verify source-controlled settings remain placeholder-only.
- Run build, tests, database update check, and pending model changes check.
- Produce a clean delivery ZIP that excludes local/generated files.

## New tools

- `tools/Clean-Phase1DeliveryArtifacts.ps1`
- `tools/Test-Phase1FinalHardening.ps1`
- `tools/New-Phase1CleanDeliveryZip.ps1`

## Delivery rule

After this phase, DARAK should be handled as a release candidate. Do not add new commercial features until the final hardening gate passes and a clean delivery ZIP is created.
