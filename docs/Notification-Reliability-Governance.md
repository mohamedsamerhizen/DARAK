# Notification Reliability Governance

DARAK uses an outbox-driven notification model. Notification reliability work must preserve these rules:

1. Notifications are claimed before delivery by moving them from `Pending` to `Processing`.
2. A processing notification must not be manually retried while a worker may still be delivering it.
3. Stale processing notifications are recovered by the delivery processor using `ProcessingTimeoutMinutes`.
4. Failed provider deliveries are retried with bounded exponential backoff.
5. Manual retry resets failed retry counters so an exhausted notification can be deliberately re-processed.
6. Sent notifications remain immutable and cannot be retried.

## Runtime Options

The `Notifications` configuration section supports:

- `RetryDelayMinutes`
- `RetryBackoffMultiplier`
- `MaxRetryDelayMinutes`
- `ProcessingTimeoutMinutes`
- `WorkerEnabled`
- `WorkerIntervalSeconds`
- `BatchSize`

## Migration Policy

This phase does not require a database migration. It changes runtime behavior and configuration defaults only.
