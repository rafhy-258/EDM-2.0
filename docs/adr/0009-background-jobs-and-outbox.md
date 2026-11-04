# ADR-0009: Background jobs, scheduler and transactional outbox

**Status:** Accepted
**Date:** 2026-11-04

## Decision
- **Outbox:** emails, in-app notifications and integrations are written to `email_outbox` or `notifications` in the same transaction as the business change. A background worker dispatches them with retry and exponential back-off, and records delivery status and failures.
- **Scheduler:** Quartz.NET (Apache-2.0) for scheduled work: retention jobs, the audit verification job, and reminders in Phase 2. Job runs are recorded in `background_jobs`.
- **Idempotency:** dispatch is at-least-once. Each outbox message has a unique key so that each recipient receives each event once (Part 23, Notifications).
- **Health:** the worker exposes queue depth and failure count to health checks and metrics.
- **Scaling:** single node first. Multi-node safety uses row locks (`FOR UPDATE SKIP LOCKED`) on the outbox.

## Consequences
- Email is never sent inside a request transaction.
- SMTP settings are environment-based. Templates are editable by the administrator (Part 12).
