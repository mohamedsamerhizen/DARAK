# Guard Visitor Workflow Governance

## Purpose

Phase 8E strengthens the visitor gate workflow without changing the database schema. It adds an explicit guard-side access-code verification path and required check-in code validation.

## Rules

- Guard access remains compound-scoped.
- Unknown or cross-compound access codes return NotFound to avoid leaking visitor existence.
- Empty access codes return BadRequest.
- Check-in may include an access code. If supplied, it must match the visitor pass code.
- Access code matching during check-in is case-insensitive for operational usability.
- List/search responses continue masking access codes.
- Detail and code-verification responses may include the full access code for authorized guard/admin workflows.

## Non-goals

- No demo data.
- No migration.
- No change to the visitor pass table.
- No QR/image generation.
- No external scanner integration.
