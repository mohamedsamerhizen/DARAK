# DARAK Security Notes

This file records security expectations for the current GitHub-ready backend package.

## Secrets and Configuration

- JWT secrets must come from environment variables, user secrets, or ignored local configuration.
- No real secrets should be committed to source control.
- `.env` and `.env.*` files are ignored, except safe example templates.
- Placeholder secrets are rejected in production startup validation.

## Registration

- Public registration is controlled by `Registration:EnablePublicRegistration`.
- Auto-confirming registered users is controlled by `Registration:AutoConfirmRegisteredUsers`.
- Production startup validation rejects registration auto-confirm.
- When public registration is disabled, the API returns a clear provisioning message instead of creating an account.

## First SuperAdmin Bootstrap

- `BootstrapAdmin` is disabled by default.
- Bootstrap requires explicit email and password configuration.
- Bootstrap refuses placeholder and weak credentials.
- Bootstrap skips when a SuperAdmin already exists.
- Bootstrap logs account creation/skip status without logging the password.

## Demo Seed Data

- `DemoSeed` is disabled by default.
- Demo seeding runs only in Development, Demo, or Testing unless `DemoSeed:AllowProduction` is explicitly enabled.
- Demo user seeding requires a strong local `DemoSeed:DemoPassword` and refuses placeholder values.
- Demo visitor and contractor access values are stored as hashes, not plaintext codes.

## JWT and Login

- JWT issuer, audience, token lifetimes, and secret must be configured.
- Outside Testing, JWT secrets must be strong enough for HMAC signing and must not use placeholders.
- Unconfirmed users cannot login.

## Operational Notes

- Swagger should remain Development-only.
- Real email/SMS providers are not configured in this backend package.
- Notification preferences suppress optional communications; critical/urgent operational notices bypass opt-outs.
- Report export completion stores sanitized filenames under the controlled report export root and rejects traversal or absolute paths.
- Production deployments need real secret management, monitoring, backup, and incident response plans.
