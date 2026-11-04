# Architecture Decision Records (EDM 2.0)

Each significant technical decision is recorded here. Status values: **Accepted**, **Proposed** (awaiting product-owner or review sign-off), **Superseded**.

| ID | Title | Status |
|---|---|---|
| [ADR-0001](0001-modular-monolith-clean-architecture.md) | Modular monolith with clean architecture | Accepted |
| [ADR-0002](0002-purpose-built-approval-state-machine.md) | Purpose-built, matrix-driven approval engine (no generic BPM) | Accepted |
| [ADR-0003](0003-frontend-component-library-fluent-ui.md) | Frontend component library: Fluent UI React v9 | Accepted, pending Phase 0 design review |
| [ADR-0004](0004-postgresql-efcore-data-conventions.md) | PostgreSQL, EF Core and data conventions | Accepted |
| [ADR-0005](0005-append-only-audit-store.md) | Append-only audit store and single write service | Accepted |
| [ADR-0006](0006-authentication-and-sso.md) | Authentication: local accounts, OIDC and SAML, MFA hooks | Accepted for design (provider choice pending, Part 18 item 25) |
| [ADR-0007](0007-api-conventions.md) | API conventions: versioning, ProblemDetails, ETag, idempotency | Accepted |
| [ADR-0008](0008-money-rounding-and-time.md) | Money, rounding and time handling | Accepted (rounding default, Part 18 item 23) |
| [ADR-0009](0009-background-jobs-and-outbox.md) | Background jobs, scheduler and transactional outbox | Accepted |
| [ADR-0010](0010-policy-versioning-and-maker-checker.md) | Policy versioning and maker-checker: apply on approval | Proposed (for Phase 1A review) |
| [ADR-0011](0011-reference-number-pattern.md) | Reference number pattern | Accepted (V3 format Not Yet Verified, Part 18 item 16) |
