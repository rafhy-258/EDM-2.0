# ADR-0011: Reference number pattern

**Status:** Accepted (V3 format Not Yet Verified)
**Date:** 2026-11-04

## Decision
- Default pattern `TYPE-YEAR-6-digit sequence`, for example `OPEX-2026-000123`. Configurable through settings: prefix per type, year reset and sequence width.
- Generation uses a database sequence per tenant, type and year. The sequence is allocated inside the submit transaction. Gaps caused by rollbacks are acceptable and documented.
- Unique index on the reference number.
- V3 references are preserved on migration (Part 17). The V3 format is Not Yet Verified and must be confirmed before migration (Part 18 item 16).
