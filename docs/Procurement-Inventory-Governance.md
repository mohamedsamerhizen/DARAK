# Procurement Inventory Governance

Status: Phase 8 remediation implemented on 2026-06-30.

## Scope

This phase covers stock items, inventory movements, purchase orders, purchase order receiving, and operational staff assignment scope. It does not include accounting integration, vendor portals, barcode scanners, or external ERP integration.

## Implemented Controls

- `StockItems` now has a row-version concurrency token.
- Stock adjustments and work-order stock issues run inside service transactions.
- Stock decreases are checked before and during movement application so quantity cannot become negative through the service path.
- Inventory movements support optional `Reference` values with a unique compound/reference index.
- Purchase order item receiving supports optional `ReceiptReference`.
- Retrying a receive request with the same receipt reference returns the existing purchase-order state without increasing stock again.
- Purchase order receive rejects draft, cancelled, closed, and already received purchase orders.
- Purchase order receive validates that the vendor and stock item belong to the purchase order compound.
- Purchase order creation rejects terminal/receiving statuses.
- Explicit purchase order approve and cancel service/controller operations were added.
- Received/closed purchase orders cannot be cancelled.
- Cancelled purchase orders cannot be approved or received.

## API Impact

- Added `PATCH /api/admin/procurement-inventory/purchase-orders/{id}/approve`.
- Added `PATCH /api/admin/procurement-inventory/purchase-orders/{id}/cancel`.
- `ReceivePurchaseOrderItemRequest` now accepts optional `receiptReference`.
- Inventory adjustment and work-order issue requests now accept optional `reference`.
- Inventory movement responses now include optional `reference`.

## Migration Impact

Migration: `20260630133311_HashAccessCodesContractorAuditMaintenanceInventoryGuards`.

The migration adds stock row version and inventory movement reference columns/indexes.

## Remaining Limitations

- EF Core InMemory tests cover service behavior and SQL Server migration application was verified, but high-contention inventory concurrency/load tests are still not included.
- Partial receiving without a receipt reference cannot be idempotently distinguished from a legitimate second partial receipt.
- No external procurement approval workflow or vendor portal was added.
