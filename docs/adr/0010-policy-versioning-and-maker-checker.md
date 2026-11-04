# ADR-0010: Policy versioning and maker-checker: apply on approval

**Status:** Proposed (for Phase 1A review)
**Date:** 2026-11-04

## Context
Part 5.0 says a maker-checker decision lets a second authorised user approve a configuration change *before it goes live*. Part 4.8 says every change creates a policy version. Part 13 lists `config_change_requests` with maker, checker, status, before and after.

## Options
1. **Apply, then approve.** The change is saved to live tables and flagged pending. Rejected for Phase 1A, because live routing would see unapproved configuration.
2. **Stage, and apply on approval (chosen).** The maker's proposed change is stored in `config_change_requests` as a full JSON snapshot (before and after). Live configuration does not change. When a checker approves, the change is applied, a new `policy_versions` row is written and the audit event is written, all in one transaction.

## Decision
- Option 2.
- A change request records the base `row_version` of each target record. On apply, a stale base returns `EDM-CONC-001`, so the checker must review again.
- Maker ≠ checker is enforced in the application and by a CHECK constraint.
- A pending change request can be rejected or withdrawn by the maker. Withdrawn and rejected requests keep their history.
- Policy versions are immutable. The `policy_versions` table has no update path. Each version holds a JSON snapshot of the configuration it describes.
- When maker-checker is switched off for a module, changes apply immediately. They still create a policy version and an audit event.

## Open points (to confirm in review)
- Whether a checker can edit before approving. Proposed: no. The checker rejects with remarks and the maker resubmits.
- Whether several pending change requests on the same record are allowed. Proposed: no, one pending request per target.
