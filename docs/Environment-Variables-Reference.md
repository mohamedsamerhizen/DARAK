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

## Development SuperAdmin Bootstrap

| Variable | Required | Notes |
|---|---:|---|
| `DEVELOPMENT_SUPERADMIN_EMAIL` | Development | Required for local bootstrap only. |
| `DEVELOPMENT_SUPERADMIN_PASSWORD` | Development | Required for local bootstrap only; must not be reused in production. |
| `DEVELOPMENT_SUPERADMIN_FULLNAME` | Development | Display name for local bootstrap user. |

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

## Production Rules

- Do not set production to `Development`.
- Do not enable SMTP/SMS without complete credentials.
- Do not use `.env.example` values as real deployment values.
- Do not store real secrets in `appsettings.json`.
- Keep `.env` outside Git and outside commercial source packages.
