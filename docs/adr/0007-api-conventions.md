# ADR-0007: API conventions

**Status:** Accepted
**Date:** 2026-11-04

## Decision (from Part 21)
- Base path `/api/v1`. Plural, lowercase, hyphenated resource names.
- JSON in and out. Money as decimal strings (for example `"125000.50"`). Timestamps as ISO 8601 in UTC.
- Lists take `page`, `pageSize`, `sort` and `filter`, and return `items`, `totalCount` and page metadata.
- Errors are RFC 9457 ProblemDetails, extended with `code` (from the catalogue in Part 21.3), a user-safe `message`, a `correlationId` and, for validation, `errors` (field list, HTTP 422).
- Concurrency: responses carry an `ETag` derived from `row_version`. Updates require `If-Match`. A stale update returns HTTP 412 with `EDM-CONC-001`.
- Non-idempotent commands (request submission first) accept `Idempotency-Key`. Keys are stored with the request result for a retention window.
- Every endpoint declares its required permission (Part 20). A test fails if an endpoint has no declared permission. The permission appears in OpenAPI as an extension.
- Authorisation is server-side only. Client-side menu filtering is cosmetic.
- Versioning: a breaking change creates `/api/v2`. Additive changes stay in v1.

## Consequences
- A shared endpoint-metadata convention is needed in Phase 0 so that the permission test is possible.
