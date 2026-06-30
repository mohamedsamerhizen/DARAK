# DARAK Demo Seed Data

DARAK includes an opt-in demo seed for local portfolio review and integration testing. It is disabled by default and must not be treated as production data.

## Configuration

```json
"DemoSeed": {
  "Enabled": false,
  "SeedUsers": true,
  "DemoPassword": "",
  "AllowProduction": false
}
```

## Guardrails

- `DemoSeed:Enabled` defaults to `false`.
- Seeding runs only in Development, Demo, or Testing unless `DemoSeed:AllowProduction` is explicitly set to `true`.
- When demo users are seeded, `DemoSeed:DemoPassword` must be a strong local password and must not be a placeholder.
- Running the seed more than once is idempotent; if both demo compounds already exist, it skips duplicate data creation.
- Visitor pass and contractor access values are stored as hashes, not plaintext access codes.

## Seed Coverage

The seed creates a compact but broad demo dataset:

- Two compounds with buildings, floors, units, and parking spots.
- Admin, operations, accounting, maintenance, guard, and resident users.
- Resident profiles, family members, emergency contacts, and active occupancies.
- Utility billing, payments, receipts, ledger entries, rent, sale installments, and collections.
- Visitor passes, guard logs, contractor permits, and hashed access credentials.
- Staff, vendors, maintenance assets, requests, work orders, stock, inventory movements, and purchase orders.
- Announcements, outages, polls, conversations, preferences, notifications, and outbox rows.
- Document requirements, document files, access logs, saved reports, export jobs, and audit entries.

## Local Use

Set these values through environment variables, user secrets, or an ignored local configuration file:

```powershell
$env:DemoSeed__Enabled = "true"
$env:DemoSeed__SeedUsers = "true"
$env:DemoSeed__DemoPassword = "Use-A-Strong-Local-Demo-Password1!"
```

Then start the API or call the seeder from tests. Do not commit real demo passwords.
