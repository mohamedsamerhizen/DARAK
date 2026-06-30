# DARAK Environment Variables Reference

## Purpose

This reference lists the runtime configuration values that should be provided through environment variables or a local `.env` file. Real values must never be committed to source control or included in buyer source archives.

## Core Runtime

| Variable | Required | Notes |
|---|---:|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | Use `Development` only for local Swagger/testing. Use `Production` for deployment. |
| `SQLSERVER_DATABASE` | Docker | Database name used by `docker-compose.yml`. |
| `SQLSERVER_SA_PASSWORD` | Docker | Local SQL Server SA password. Must be strong and not a placeholder. |
| `ConnectionStrings__DefaultConnection` | Non-Docker | Full SQL Server connection string when not using compose mapping. |

## JWT

| Variable | Required | Notes |
|---|---:|---|
| `JWT_ISSUER` | Yes | Maps to `Jwt:Issuer`. |
| `JWT_AUDIENCE` | Yes | Maps to `Jwt:Audience`. |
| `JWT_SECRET_KEY` | Yes | Must be a strong secret with at least 32 bytes. Never use placeholder text. |
| `JWT_ACCESS_TOKEN_MINUTES` | Yes | Recommended local default: `15`. |
| `JWT_REFRESH_TOKEN_DAYS` | Yes | Recommended local default: `7`. |

## Registration

| Variable | Required | Notes |
|---|---:|---|
| `REGISTRATION_ENABLE_PUBLIC_REGISTRATION` | Optional | Defaults to `true` in local compose and `false` in production compose. |
| `REGISTRATION_AUTO_CONFIRM_REGISTERED_USERS` | Optional | Intended only for Development/Testing/Demo; production startup rejects auto-confirm. |

## First SuperAdmin Bootstrap

| Variable | Required | Notes |
|---|---:|---|
| `BOOTSTRAP_ADMIN_ENABLED` | Optional | Defaults to `false`. Set to `true` only for guarded first-admin provisioning. |
| `BOOTSTRAP_ADMIN_EMAIL` | Bootstrap only | Required when bootstrap is enabled. Must not be a placeholder. |
| `BOOTSTRAP_ADMIN_PASSWORD` | Bootstrap only | Required when bootstrap is enabled. Must be strong and not committed. |
| `BOOTSTRAP_ADMIN_FULLNAME` | Optional | Display name for the first SuperAdmin. |

## Demo Seed

| Variable | Required | Notes |
|---|---:|---|
| `DEMO_SEED_ENABLED` | Optional | Defaults to `false`. Enable only for local portfolio/demo data. |
| `DEMO_SEED_USERS` | Optional | Defaults to `true` locally and `false` in production compose. |
| `DEMO_SEED_PASSWORD` | Demo users only | Required when demo user seeding is enabled. Must be strong and not committed. |
| `DEMO_SEED_ALLOW_PRODUCTION` | Optional | Defaults to `false`. Leave disabled unless a controlled non-local demo environment intentionally needs seed data. |

## Notification Delivery

| Variable | Required | Notes |
|---|---:|---|
| `NOTIFICATIONS_WORKER_ENABLED` | Optional | Keep `false` until providers are configured and tested. |
| `NOTIFICATIONS_EMAIL_ENABLED` | Optional | Keep `false` unless SMTP settings are complete. |
| `NOTIFICATIONS_EMAIL_HOST` | Optional | SMTP host. |
| `NOTIFICATIONS_EMAIL_PORT` | Optional | Default usually `587`. |
| `NOTIFICATIONS_EMAIL_ENABLE_SSL` | Optional | Usually `true`. |
| `NOTIFICATIONS_EMAIL_USERNAME` | Optional | SMTP username. |
| `NOTIFICATIONS_EMAIL_PASSWORD` | Optional | SMTP password/secret. |
| `NOTIFICATIONS_EMAIL_FROM` | Optional | Sender address. |
| `NOTIFICATIONS_SMS_ENABLED` | Optional | Keep `false` unless HTTP SMS settings are complete. |
| `NOTIFICATIONS_SMS_ENDPOINT_URL` | Optional | Provider endpoint. |
| `NOTIFICATIONS_SMS_API_KEY` | Optional | Provider API key. |
| `NOTIFICATIONS_SMS_SENDER_ID` | Optional | Sender ID approved by provider. |

## Test-Only SQL Integration

| Variable | Required | Notes |
|---|---:|---|
| `DARAK_SQLSERVER_TEST_CONNECTION` | Optional | Enables optional SQL Server integration tests. Tests create and delete a unique temporary database. |

## Production Rules

- Do not set production to `Development`.
- Do not enable registration auto-confirm in production.
- Do not enable `BootstrapAdmin` unless first-admin provisioning is intentionally being performed.
- Do not enable `DemoSeed` outside Development/Demo/Testing unless explicitly approved.
- Do not enable SMTP/SMS without complete credentials.
- Do not use `.env.example` values as real deployment values.
- Do not store real secrets in `appsettings.json`.
- Keep `.env` outside Git and outside commercial source packages.
