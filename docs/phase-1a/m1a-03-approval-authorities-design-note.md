# M1A-03 Design Note: Approval Authorities

**Status:** Implemented. Domain, web, and isolated backend-build checks pass. The PostgreSQL integration test is added but could not run because Docker is unavailable and `EDM_TEST_POSTGRES` is not configured. The Phase 1A data-model note and Master Prompt Parts 5.3, 5.4, 14.1, 20, and 21 are authoritative.

## Scope

Build the P1 Approval Authorities screen at `/configuration/approval-authorities` with:

- The EC member pool, with members sourced from the active organisation directory, designation and status filters, duplicate/reactivation guidance, and confirmed activation, deactivation, and deletion.
- Recommending authorities with title, department, department-filtered user, status, and confirmed activation, deactivation, and deletion.
- Editable action-tag defaults per designation, including the default set used by recommending authorities. EC members cannot receive `FORWARD`; `APPROVE` remains required.
- Tenant-scoped persistence, server-side permission checks, optimistic concurrency, audit snapshots, and usage checks through the existing configuration usage port.

EC levels and the delegation matrix remain later M1A items. Request, policy-version, and maker-checker behavior are out of scope for this slice.

## Data model

Add the next ordered Configuration SQL migration for:

- `action_tag_sets`: tenant-owned, versioned, active defaults keyed by designation, with the permitted actions stored as a PostgreSQL text array. Seed the Master Prompt defaults idempotently; the configurable tenant is also initialized by the application.
- `ec_members`: tenant-owned membership referencing a same-tenant user; supported designations are `EC_MEMBER`, `CEO`, `CFO`, and `HEAD_ADMIN_GST`. Enforce one member/designation pair across active and inactive rows, and one active holder for each non-EC designation.
- `recommending_authorities`: tenant-owned title, department, user and default action-tag set. Enforce department/user uniqueness among active, non-deleted rows and same-tenant foreign keys.

All mutable rows carry the shared audit metadata and `row_version`. Deletes are soft deletes. Directory reads are through a Configuration-owned application port, adapted by the host to the Identity application interface; Configuration does not query Identity tables.

## API

Use `/api/v1` and existing ProblemDetails conventions:

- `GET /ec-members?designation=&includeInactive=&search=`
- `POST /ec-members`
- `PUT /ec-members/{id}`
- `DELETE /ec-members/{id}?rowVersion=`
- `GET /recommending-authorities?includeInactive=&search=`
- `POST /recommending-authorities`
- `PUT /recommending-authorities/{id}`
- `DELETE /recommending-authorities/{id}?rowVersion=`
- `GET /ec-members/candidates` for active EC-member choices, protected by `config.ec.pool.manage`.
- `GET /recommending-authorities/departments` and `/recommending-authorities/candidates?departmentId=` for scoped authority choices, protected by `config.ra.manage`.
- `GET /action-tag-sets` and `PUT /action-tag-sets/{id}` for designation defaults.

EC pool, recommending-authority, and action-tag endpoints require `config.ec.pool.manage`, `config.ra.manage`, and `config.actiontags.manage` respectively.

## Screen and acceptance

Keep the existing navigation item and its route. Present an enterprise-grade page with pool and authority tabs, searchable/filterable tables, explicit status, focused dialogs, field-level errors, and confirmation for destructive/status actions. Keep directory choices scoped to the selected department and show loading, empty, error, and permission states.

Focused unit tests cover title/action-tag rules, and web tests cover API payloads and navigation access. PostgreSQL integration coverage for endpoint permissions, duplicate/reactivation behavior, department membership, action-tag restrictions, and migration execution is added but remains unverified until run with a test database.

## Known Phase 1A gap

Part 4.8 requires a policy version on configuration changes, while `policy_versions` is M1A-06 and does not exist yet. This screen will audit each applied change and apply it immediately, matching the existing Phase 1A slices. Policy-version creation and maker-checker are explicitly not claimed complete; close the gap in M1A-06 before Phase 1A acceptance.
