# DARAK Backend Architecture Diagrams

These diagrams are source-controlled Mermaid diagrams. They are not screenshots.

## Backend Module Map

```mermaid
flowchart LR
    Client["API clients\nSwagger, admin UI, resident app, guard app"] --> Api["DARAK.Api\nASP.NET Core Web API"]
    Api --> Auth["Identity and JWT\nroles, refresh tokens, bootstrap"]
    Api --> Scope["Compound access scope\nresident and admin boundaries"]
    Api --> Domain["Domain services"]
    Domain --> Finance["Billing, payments,\nrent, sales, collections"]
    Domain --> Ops["Maintenance, staff,\nvendors, procurement, inventory"]
    Domain --> Access["Visitors, guards,\ncontractor access"]
    Domain --> Comms["Announcements, outages,\npreferences, outbox"]
    Domain --> Docs["Documents, approvals,\naudit, reports"]
    Auth --> Db["SQL Server\nEF Core migrations"]
    Scope --> Db
    Finance --> Db
    Ops --> Db
    Access --> Db
    Comms --> Db
    Docs --> Db
```

## Notification Outbox Flow

```mermaid
sequenceDiagram
    participant Admin as Admin or system action
    participant Service as DARAK service
    participant Prefs as Resident preferences
    participant Db as SQL Server
    participant Worker as Notification worker
    participant Provider as Email/SMS/In-app provider

    Admin->>Service: Publish announcement, outage, campaign, approval, or financial event
    Service->>Prefs: Evaluate resident opt-out and mandatory overrides
    Service->>Db: Store ResidentNotification and NotificationOutbox rows
    Worker->>Db: Claim due Pending rows as Processing
    Worker->>Provider: Attempt delivery
    alt Provider accepts
        Worker->>Db: Mark Sent and append attempt
    else Provider fails
        Worker->>Db: Retry with bounded backoff or mark Failed
    end
```

## Demo Seed Guardrail

```mermaid
flowchart TD
    Start["Application startup"] --> Config["Read DemoSeed section"]
    Config --> Enabled{"DemoSeed:Enabled?"}
    Enabled -- "false" --> Stop["No demo data is created"]
    Enabled -- "true" --> Prod{"Production environment?"}
    Prod -- "yes" --> Override{"AllowProduction true?"}
    Override -- "no" --> Fail["Fail closed before seeding"]
    Override -- "yes" --> Password
    Prod -- "no" --> Password{"SeedUsers requires strong local password"}
    Password -- "invalid" --> FailPassword["Fail closed"]
    Password -- "valid" --> Exists{"Demo compounds already exist?"}
    Exists -- "yes" --> Idempotent["Skip duplicate seed"]
    Exists -- "no" --> Seed["Seed demo users, compounds,\nfinancials, access, ops, docs, reports"]
```

