# DARAK Security Checklist

## Identity and Authentication

- [ ] JWT issuer, audience, and signing key are configured through environment variables.
- [ ] JWT secret is at least 32 bytes and is not committed.
- [ ] Refresh-token reuse protection remains enabled.
- [ ] Auth rate limiting remains configured.
- [ ] Development SuperAdmin credentials are not used in production.

## Authorization

- [ ] Admin routes require admin roles.
- [ ] Resident routes derive access from the authenticated resident user.
- [ ] Guard routes expose only guard-specific visitor workflows.
- [ ] Compound-scoped services use `ICompoundAccessService`.
- [ ] SuperAdmin-only actions remain restricted.

## Data Protection

- [ ] Document uploads validate size, extension, MIME type, and signature.
- [ ] Visitor access codes are masked in list/search outputs.
- [ ] Password hashes, refresh tokens, and credentials are never exported.
- [ ] Audit logs hide sensitive values where required.

## Financial Safety

- [ ] Payment and billing changes are audited.
- [ ] Refund/correction logic is scoped and documented.
- [ ] RowVersion concurrency remains configured for key financial entities.

## Packaging

- [ ] `.env` is excluded.
- [ ] `appsettings.Development.json` is excluded from clean archives.
- [ ] `bin`, `obj`, `TestResults`, logs, and ZIP artifacts are excluded.
- [ ] Release gate passes before handoff.
