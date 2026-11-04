# ADR-0005: Append-only audit store and single write service

**Status:** Accepted
**Date:** 2026-11-04

## Decision
- One `audit_events` table serves the whole product (Part 10). Module audit tabs, the request timeline and the global viewer are filtered reads of it.
- All writes go through a single `IAuditWriter` application service. No module writes audit rows directly. Every mutating endpoint is covered by an audit test.
- The application database role has `INSERT` and `SELECT` only on `audit_events`. It has no `UPDATE` or `DELETE` grant. A database trigger also rejects `UPDATE` and `DELETE` as a second line of defence.
- Each event contains the fields in Part 10.1: tenant, UTC timestamp, actor id, name, department, roles, source IP, user agent, correlation id, module, entity type and id, action, outcome, failure reason, remarks, request reference, policy version, and before and after as JSON with a field-level diff.
- Audit writes happen in the same transaction as the business change. If the audit write fails, the change is rolled back. For events that must be recorded even when the business change fails (routing failure, access denied, failed sign-in), an independent transaction is used.
- Viewing the audit store is itself audited (Part 10.2).
- Proposed Enhancement (Part 10.3, needs approval): hash chaining of events with a verification job.
- Retention is configurable. Archival keeps events readable and exportable (Part 18 item 24).

## Consequences
- Volume is about 5 million events a year (Part 19). The table is indexed on tenant, timestamp, actor, module and entity.

## Implementation note (Phase 0, added after the first build)

- **Schema by versioned SQL, not EF Core migrations.** `backend/src/Modules/Audit/Edm.Audit.Infrastructure/Migrations/0001_audit_schema.sql` creates `audit_events`, its indexes, the append-only trigger (UPDATE, DELETE and TRUNCATE are rejected for every role, including the owner) and the CHECK constraints. EF Core migrations cannot express the trigger and the grants exactly, so Phase 0 uses versioned SQL. `AuditSchemaMigrator` applies the scripts once each, recorded in `schema_migrations`, under an advisory lock. EF Core migrations are used from Phase 1A onwards, and the decision for Phase 1A is recorded there.
- **Runtime mapping by EF Core.** `AuditDbContext` maps the table with explicit snake_case column names. An integration test checks that the mapping and the SQL agree.
- **Application role.** The integration test `The_application_role_can_append_and_read_but_cannot_change_or_delete` checks the grants the production role needs: INSERT and SELECT only.
- **One write service.** `IAuditWriter` is the only path into the store. It fills the time (UTC), tenant, actor and correlation id from the request context. Modules never write to `audit_events` directly.
- **Viewer.** `GET /api/v1/audit-events` requires `audit.view`. Each view is itself written as a `VIEW` event (Part 10.2).
