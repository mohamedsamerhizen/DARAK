# Phase 4 — Intelligence & Escalation

Phase 4 adds a read-only management intelligence layer that turns existing DARAK data into escalation queues and decision briefs.

## Added capabilities

- Compound escalation dashboard.
- Cross-module escalation queue.
- Resident decision brief.
- Severity scoring across financial, legal, communication, operations, and notification reliability signals.
- Compound access enforcement.

## No database migration

This phase does not introduce new entities or tables. It reads from existing commercial modules and computes operational intelligence at request time.

## API surface

- `GET /api/admin/intelligence-escalation/compounds/{compoundId}/dashboard`
- `GET /api/admin/intelligence-escalation/compounds/{compoundId}/queue`
- `GET /api/admin/intelligence-escalation/residents/{residentId}/decision-brief`

## Commercial value

The module gives management a prioritized command layer for deciding what must be handled now, who is blocked, and which resident or unit workflow requires escalation before operational or financial risk grows.
