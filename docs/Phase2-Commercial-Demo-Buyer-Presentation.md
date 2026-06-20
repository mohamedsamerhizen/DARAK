# DARAK Phase 2 — Commercial Demo & Buyer Presentation

Phase 2 makes DARAK easier to show to a buyer without adding risky database tables or changing core business workflows.

## Added backend surface

- `GET /api/admin/commercial-presentation/demo-seed-blueprint`
- `GET /api/admin/commercial-presentation/demo-mode`
- `GET /api/admin/commercial-presentation/buyer-presentation-pack`

## Purpose

Phase 1 cleaned and verified the delivery baseline. Phase 2 adds a buyer-facing presentation layer that explains:

1. what demo data is needed,
2. how to run a commercial demo story,
3. how to answer buyer objections,
4. what documents and endpoints should be shown during handoff.

## Important safety rule

This phase does **not** seed fake records into production automatically. It provides a safe demo seed blueprint and presentation pack. Actual seeded demo data should be created only in a dedicated demo database or a clearly named demo compound.

## Buyer demo structure

1. Open with commercial final scorecard.
2. Show commercial presentation demo mode.
3. Show finance and collections.
4. Show resident and communication workflows.
5. Show maintenance, vendors, and access control.
6. Show compliance, testing, deployment, and handoff evidence.

## Commercial value

This phase converts DARAK from a backend-only artifact into a product that can be explained to a non-technical buyer. Swagger remains developer evidence; the buyer demo should use a clear narrative, seeded data, and a short walkthrough.

## Next recommended phase

Phase 3 should add DARAK 360 Profiles:

- Resident 360 Profile
- Unit 360 Profile
- Compound/Digital Twin overview
