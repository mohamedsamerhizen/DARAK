# Maintenance Reliability

Status: Phase 7 remediation implemented on 2026-06-30.

## Scope

This phase covers maintenance assets, preventive maintenance generation, SLA breach/escalation behavior, work order assignment integrity, and operational checklist/task scope checks. It does not add scheduling workers, notification escalation delivery, or frontend/mobile work-order screens.

## Implemented Controls

- Preventive maintenance generation now writes `PreventiveMaintenanceOccurrenceKey` on generated work orders.
- A unique index prevents duplicate preventive work orders for the same compound, source plan, and occurrence key.
- Preventive plans keep `LastGeneratedOccurrenceKey` so no-window reruns can return the most recent generated work order until the next due date is reached.
- SLA refresh escalates active work orders whose resolution deadline is overdue.
- SLA breach timestamps are preserved on the work order.
- Escalation metadata is tracked with `SlaEscalatedAtUtc`, `LastSlaEscalatedAtUtc`, and `SlaEscalationCount`.
- Completed and cancelled work orders are excluded from SLA refresh escalation.
- Preventive plan staff/vendor assignment now requires active staff/vendor in the same compound as the asset.
- Existing work-order assignment, terminal-state, status-history, and rating rules remain in `OperationsService`.
- Operational task assignment now rejects assigned users that lack access to the task compound, while SuperAdmin users remain allowed.

## API Impact

No existing maintenance route was removed. Preventive generation remains on the existing route. Response shapes are unchanged for SLA snapshot APIs.

## Migration Impact

Migration: `20260630133311_HashAccessCodesContractorAuditMaintenanceInventoryGuards`.

The migration adds preventive occurrence metadata and SLA escalation metadata to work orders/plans.

## Remaining Limitations

- No background scheduler is included; generation and SLA refresh still run when the existing API/service method is invoked.
- SLA escalation records are stored on the work order, not in a separate escalation-history table.
- SQL Server migration application was verified; load/concurrency testing under production-like scheduler conditions is still not included.
