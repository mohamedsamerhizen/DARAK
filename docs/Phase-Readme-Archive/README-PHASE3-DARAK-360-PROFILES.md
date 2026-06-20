# DARAK Phase 3 â€” DARAK 360 Profiles

This patch adds DARAK 360 Profiles as the first intelligence presentation layer after commercial demo preparation.

## Added endpoints

- `GET /api/admin/darak-360/residents/{residentId}`
- `GET /api/admin/darak-360/units/{unitId}`
- `GET /api/admin/darak-360/compounds/{compoundId}/overview`

## What it provides

- Resident 360: current unit, finance, operations, communication, legal/risk, signals, and recommended actions.
- Unit 360: current resident, finance, maintenance, meters, readiness, damage liability, signals, and recommended actions.
- Compound 360: inventory, residents, finance, operations, legal/risk, communications, signals, and recommended actions.

## Migration impact

No migration. No schema change. This is a read-only aggregation and intelligence layer over existing DARAK data.

