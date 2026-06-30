# Tenant Isolation

This remediation pass hardens operational tenant boundaries for staff, vendors, and high-risk role behavior.

## Implemented

- `StaffMember` and `ServiceVendor` are now compound-scoped entities.
- Staff and vendor create/update/search/get/status flows enforce current compound access.
- Work orders reject staff or vendor assignment when the assignee belongs to another compound.
- Contractor work permits reject vendors from a different compound.
- Purchase orders reject vendors from a different compound.
- Resident risk flag management no longer allows `Accountant`; accountants remain read-only where reader roles allow them.

## Migration

`20260630115926_AddCompoundScopeToStaffAndVendors` adds `CompoundId` to `StaffMembers` and `ServiceVendors`, backfills existing rows to the first available compound, then makes the columns required and adds restrictive foreign keys.

If staff/vendor rows exist but no compounds exist, the migration fails with an explicit error instead of writing `Guid.Empty`.

## Tests

Coverage includes staff/vendor search scoping, blocked cross-compound create, blocked work-order assignee mismatch, contractor permit vendor mismatch, purchase-order vendor mismatch, and accountant risk-flag mutation denial.
