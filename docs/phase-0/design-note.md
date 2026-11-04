# Phase 0 Design Note: Foundations

**Product:** EDM 2.0 (Expense Delegation Matrix), Warmbytes (Pvt) Ltd
**Specification:** [docs/EDM2-Master-Prompt.md](../EDM2-Master-Prompt.md), Parts 0 to 3, 10, 11, 13, 19 to 21 (read-first list for Phase 0)
**Status:** Draft for product-owner review. Nothing in this note is approved until the owner replies "approved", per Standing Preamble 24.1.

---

## 1. Goal and scope

Goal: a secure, observable, tested skeleton on which every later module is built (Part 24.2).

| # | Scope item (Part 24.2) | Plan in this note | Status today |
|---|---|---|---|
| 1 | Solution layout, modular monolith, React app shell with navigation from 14.1 | Sections 3 and 6 | Web shell and backend (solution, Host, Audit module) built. Both CI workflows green (section 2). |
| 2 | Component library decision as ADR, design system base | ADR-0003, section 5 | Decision recorded. Design-system primitives built in the web shell. |
| 3 | Authentication: local accounts, SSO skeleton (OIDC, SAML), MFA hooks, lockout, session timeout | ADR-0006, section 7 | Deny-by-default authentication built; no sign-in provider yet (SSO pending Q25). Lockout and session timeout not started. |
| 4 | Organisation and identity: users, departments, branches, hierarchy, Department Head mapping, roles, permissions, seeds from Part 20, CSV or API import | Section 7 | Not started. Permission catalogue (49 strings) and matrix are in place. |
| 5 | Audit store with one write service, basic viewer | ADR-0005, section 8 | Built: append-only store, AuditWriter, viewer API, and the Activity Logs viewer screen at `/audit/activity` (web). Export not built. |
| 6 | API conventions: ProblemDetails, error catalogue, paging, ETag and If-Match, Idempotency-Key, OpenAPI with permission per endpoint | ADR-0007, section 9 | ProblemDetails, error catalogue, paging and permission declaration built. ETag and Idempotency-Key not started. |
| 7 | Logging, correlation ids, health checks, CI (build, test, lint, dependency and licence scan), Docker Compose, seed data, README | Section 10 | Logging, correlation ids, health checks and backend-ci built. Docker Compose and seed data not started. |

**Out of scope (Part 24.2):** any business module. No currencies, categories, EC, rules, requests or routing in Phase 0.

---

## 2. Environment constraints and how the backend is verified

The build sandbox cannot reach the .NET SDK download host or NuGet, so the backend cannot be compiled or tested inside it. The backend is verified in two other places:

| Check | Where it runs | Status |
|---|---|---|
| Restore, build (warnings as errors), unit, architecture and integration tests | GitHub Actions, `backend-ci.yml` (ubuntu-latest, `actions/setup-dotnet` 10.x, Docker for Testcontainers) | Green on the current head of the branch: 17 integration, 36 unit and 5 architecture tests pass. |
| Local checks on the product owner's Windows machine: .NET 10 SDK, PostgreSQL 18.6 | Owner's machine. Commands in `backend/README.md`. | Confirmed by the owner on 2026-11-04: PostgreSQL 18.6 (64-bit) on localhost:5432, database `postgres`, user `postgres`, authentication successful. .NET SDKs 9.0.301 and 10.0.403 are installed. Still to confirm: `dotnet --version` run inside `backend/` reports 10.0.x (`global.json` pins the SDK), and `dotnet test EDM.sln` passes with `EDM_TEST_POSTGRES` set. |
| Browser check of the web shell | Owner's browser via the live preview | Not yet done. The sandbox cannot download the Playwright browser. |

Integration tests run against the server named by the `EDM_TEST_POSTGRES` environment variable when it is set (local PostgreSQL 18). Otherwise they start a `postgres:17-alpine` container with Testcontainers. Each test class uses its own freshly created database, which is dropped afterwards. Credentials are never written to the repository or to CI logs.

---

## 3. Solution layout

What exists after Phase 0 (`backend/`), and what is planned:

```
EDM-2.0/
├── README.md
├── docs/                              # specification, ADRs, design notes, open questions, permission matrix
├── backend/
│   ├── global.json                    # SDK 10.0.100, rollForward latestFeature
│   ├── Directory.Build.props          # net10.0, nullable, implicit usings, warnings as errors
│   ├── Directory.Packages.props       # central package versions
│   ├── EDM.sln
│   ├── src/
│   │   ├── Shared/
│   │   │   ├── Edm.SharedKernel/      # framework-free: error catalogue (Part 21.3), permission catalogue (Part 20.1), paging, context abstractions
│   │   │   └── Edm.Web/               # ASP.NET Core adapters: deny-by-default auth, permission policies, ProblemDetails, actor and correlation context
│   │   ├── Host/
│   │   │   └── Edm.Host/              # composition root: pipeline, Serilog, health checks, OpenAPI, optional startup migration
│   │   └── Modules/
│   │       └── Audit/                 # the first module, built to the full four-layer shape
│   │           ├── Edm.Audit.Domain/          # AuditEvent (immutable), AuditDiff, AuditOutcome
│   │           ├── Edm.Audit.Application/     # AuditWriter (the one write service), AuditViewer, ports
│   │           ├── Edm.Audit.Infrastructure/  # EF Core mapping, SQL migration 0001, schema migrator
│   │           └── Edm.Audit.Api/             # GET /api/v1/audit-events (permission audit.view)
│   └── tests/
│       ├── Edm.UnitTests/             # xUnit: domain, application, shared kernel
│       ├── Edm.ArchitectureTests/     # NetArchTest: layer and dependency rules
│       └── Edm.IntegrationTests/      # xUnit + PostgreSQL: schema, append-only guarantees, API, endpoint permission test
├── web/                               # React + strict TypeScript + Vite (Phase 0 shell)
├── deploy/
│   └── docker-compose.yml             # planned: PostgreSQL and app. Not yet written (see section 10).
└── .github/workflows/
    ├── web-ci.yml                     # web: typecheck, lint, test, build, licence scan, audit
    └── backend-ci.yml                 # backend: restore, build, test
```

Planned modules that are **not** created yet: Identity & Organisation, Configuration, Expense-OPEX, Expense-CAPEX, Approval Engine, Notifications, Reporting, Integrations. Each is created only when its design note is approved (Standing Preamble). Each will have the four folders from ADR-0001.

### Deviations from the earlier plan

| Earlier plan | What was built | Why |
|---|---|---|
| `src/BuildingBlocks/` | `src/Shared/Edm.SharedKernel` and `src/Shared/Edm.Web` | The kernel must stay framework-free, so the ASP.NET Core adapters sit in a separate project. The architecture test enforces this. |
| Module folders `IdentityOrganisation/`, `ExpenseOpex/`, and so on | Folder names follow the module names in Part 3, for example `Audit/` with `Edm.Audit.*` projects | Consistent naming. Only Audit exists in Phase 0. |
| `tests/UnitTests`, `IntegrationTests`, `ArchitectureTests` | `tests/Edm.UnitTests`, `Edm.IntegrationTests`, `Edm.ArchitectureTests` | Naming convention `Edm.*`. |
| Audit schema through EF Core migrations | Versioned SQL, `Migrations/0001_audit_schema.sql`, applied by `AuditSchemaMigrator` | The append-only trigger and the grants must be exact. EF Core mapping still handles runtime reads and writes. An integration test checks that the mapping and the SQL agree. EF Core migrations are used from Phase 1A onwards. This is recorded in ADR-0005. |
| Migrations run manually | `Database:MigrateOnStartup` controls a startup migration, default off | Production migrations are a deployment step. Development and tests turn it on. |

---

## 4. Component library

Decision: **Fluent UI React v9**, Tailwind for layout only, TanStack Table with Fluent primitives. Full comparison and rationale are in [ADR-0003](../adr/0003-frontend-component-library-fluent-ui.md).

---

## 5. Design system base (Phase 0 deliverable, built in `web/`)

Built in `web/src/design/` and `web/src/components/`:

- **Tokens:** colour, spacing, typography and elevation in one file, mapped to Fluent theme tokens. Light theme first. Dark theme and high-contrast are checked later.
- **Layout:** app frame with collapsible sidebar, header (search, notifications, profile, tenant context) and content area. Responsive: the sidebar becomes a drawer below the tablet breakpoint.
- **Status badges:** one colour and one label per status across the product (Part 8.1). The label carries the meaning, so colour is never the only signal (Part 14.2). Covers request status (Draft, Pending, Reverted, Approved, Rejected) and step state (Waiting, Active, Approved, Rejected, Reverted, Forwarded, Closed (Quorum Met), Superseded).
- **States:** `LoadingState`, `EmptyState`, `ErrorState` and `NoPermissionState` components, used by every screen (Part 14.2).
- **Tables:** TanStack Table v8 wrapper (pinned, see ADR-0003) with a loading, empty and error state. Search, sorting, paging and column visibility are shared. Server-side paging is wired in a later phase.
- **Dialogs:** a confirmation dialog for sensitive actions (deactivate, delete, approve, reject, revert).
- **Forms:** React Hook Form with Zod. Field-level messages and an unsaved-changes guard.

Forms and tables are not used by a business screen in Phase 0, so only the primitives and their tests are built.

---

## 6. Navigation (Part 14.1)

Navigation is data in `web/src/navigation/navigation.ts`. Each item has: area, label, route, phase (`P1` or `P2`), and a `requiredPermission` string from Part 20.

- **P1 items** appear as routes with the `EmptyState` placeholder ("This area is built in Phase 1X"). Each item is shown only when the user holds its permission (cosmetic UX; the server is the authority).
- **P2 items** are not rendered at all and have no route. A test asserts this.
- The menu filter is a pure function `visibleNavigation(items, permissions)`, unit-tested.

Permissions for the menu come from the server session in a later step. Phase 0 uses a fixture (`devSession`) that is clearly labelled as development-only and is not used in production builds.

---

## 7. Authentication, organisation and identity

**Authentication (ADR-0006).** ASP.NET Core Identity for local accounts. OIDC through the built-in handler. SAML through a library that must pass the licence check (candidate list in Phase 0 review). MFA hook in the sign-in pipeline. Lockout and session timeout from configuration.

**Organisation tables (Part 13):** `users`, `departments`, `branches`, `business_units`, `positions`, `department_heads` (department, user, effective_from, effective_to), `roles`, `permissions`, `role_permissions`, `user_roles`. `business_units` and `positions` are Phase 2 in the navigation but their tables are part of the Part 13 model, so their tables are created with the identity migration and their screens are not built.

**Department Head resolution.** `department_heads` is effective-dated. The "current head" is the row where today falls inside the range. A user without a department cannot submit (Part 11.1).

**Permissions (Part 20).** Seeded from the catalogue: 49 permission strings across requests, approvals, configuration, organisation, identity, reports, audit and system. Roles seeded: Requester, EDM Administrator, Configuration Approver, Auditor, Finance Viewer, System Administrator. Department Head is granted by hierarchy, not stored as a role (Part 20.2). Approver is assignment-based, not a stored role (Part 11.2).

**Import.** Users and departments by CSV upload and by API (`POST /api/v1/users/import`, preview then commit, row-level errors). Each import writes audit events. Directory sync (AD/LDAP) is a Phase 1 item for the org sync; the connector choice waits for the answer to Part 18 item 25.

**Data scope.** Scope values are own, department and all, applied on top of permissions in the query layer (Part 20 preamble).

---

## 8. Audit store (ADR-0005)

- `audit_events` table with the Part 10.1 fields, indexed on tenant plus timestamp, actor, module, and entity.
- Single write service `IAuditWriter`. No module writes audit rows directly.
- Application role has insert and select only. A trigger blocks update and delete.
- Viewer: global list with filters (date range, module, action, entity type, entity id, outcome), server-side paging, and a detail dialog with the field changes. Built in Phase 0.
- Not built yet: filters by user and department, and export to CSV and Excel. Viewing the audit store is itself an audit event, and that is built.
- The Phase 0 "Done when" example (user and department changes) needs the identity module, which is not built. The viewer will show those events once they are written.

---

## 9. API conventions (ADR-0007)

- Base path `/api/v1`. Errors as RFC 9457 ProblemDetails with `code`, `message`, `correlationId` and `errors`.
- Error codes: the starter catalogue of Part 21.3 is defined in code as constants. A test checks the catalogue against the spec list.
- `ETag` and `If-Match` for updates. Stale update returns 412 with `EDM-CONC-001`.
- `Idempotency-Key` header support in the infrastructure, used by the first non-idempotent command (request submission, Phase 1C).
- Paged list contract: `items`, `totalCount`, `page`, `pageSize`.
- **Permission declaration.** Every endpoint is declared with `[RequiresPermission("...")]`. An architecture test reflects over all endpoints and fails if any endpoint lacks the attribute, or if an attribute names a permission that is not in the catalogue. This is the Phase 0 "done" test.

---

## 10. Logging, observability, CI and local run

- **Serilog** structured logs with the correlation id in every entry.
- **Correlation id:** taken from `X-Correlation-Id` or generated, returned in the response, and written to every audit event.
- **OpenTelemetry** traces and metrics. Exporter is environment-configured.
- **Health checks:** `/health/live` and `/health/ready`, including the database and outbox worker when those exist.
- **CI (GitHub Actions):**
  - `web-ci.yml` (buildable now): install, typecheck, lint, unit tests, production build, licence report.
  - `backend-ci.yml` (green on the branch head): restore, build with warnings as errors, unit tests, architecture tests, and integration tests against real PostgreSQL (`EDM_TEST_POSTGRES` locally, Testcontainers in CI). NuGet vulnerability audit at `high` fails the build. Backend licence scan and SBOM generation are not built yet.
- **Docker Compose:** PostgreSQL service and app service, with the app image built from a multi-stage Dockerfile. Needs Docker, so it is not testable here.
- **One-command start:** `scripts/dev.ps1` (Windows PowerShell) checks the SDK and Node, installs web packages on first run, asks for the database password (kept in memory only), creates the database if it is missing, starts the API with the audit schema applied (`Database:MigrateOnStartup`), and starts the web dev server. It needs PostgreSQL already running. Docker Compose, which would also start PostgreSQL, is not written. Seed data is not part of Phase 0 (the PKR seed belongs to Phase 1A).
- **Seed data:** PKR currency with rate 1, the permission catalogue, the six roles, the CAPEX eligibility default (Admin, Information Technology, Digital) as a Phase 1D seed, and development users (development profile only).

---

## 11. Phase 0 Done criteria (Part 24.2) and status

| Done criterion | Status |
|---|---|
| A user can sign in | **Not met.** No sign-in provider yet (SSO choice pending Q25). Deny-by-default is built, so nobody can sign in until a provider is added. The web shell has a sign-in placeholder. Blocked on Q25, which needs the product owner's decision. |
| The menu shows only what the role permits | **Partly met.** Built and tested in the web shell with fixture permissions. Not yet driven by the server session, which needs sign-in. |
| Creating or changing a user or department writes a correct audit event visible in the viewer | **Partly met.** The audit store, writer, viewer API and the Activity Logs screen are built and tested. User and department changes are not built (identity module not started), so no user or department event exists to show yet. |
| A test proves every endpoint declares a permission | **Met** for the endpoints that exist (`Every_endpoint_declares_a_known_permission_or_is_an_explicit_anonymous_health_probe`). It must be kept as new endpoints arrive. |
| CI is green | **Met** for `web-ci` and `backend-ci` on the current head of the branch. |
| One command starts everything locally | **Partly met.** `scripts/dev.ps1` starts the API (with the audit schema) and the web dev server, given a running PostgreSQL. It is written but not yet run on the owner's machine. It does not start PostgreSQL. `deploy/docker-compose.yml` is not written, and Docker has not been verified. |

---

## 12. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Backend cannot be compiled in this sandbox (section 2) | High (confirmed) | High | Request network access or a .NET build runner (E-1). Keep backend work out of the repo until it can be verified. |
| Fluent UI choice reversed late | Low | High | Confirm in design review before screens are built (ADR-0003). |
| SSO provider unknown (Part 18 item 25) | High | Medium | Design both OIDC and SAML. Choose at deployment. Local accounts as fallback. |
| Hosting model unknown (Part 24.2) | High | Medium | Docker is the default. IIS option is Phase 1E. |
| Part 18 gaps block Phase 1A | Medium | High | Part 18 items 1 to 7 are required before the matrix (section 24.3). Phase 1A data model is drafted in its own note. |
| Licence or supply-chain issues | Low | Medium | Licence scan in CI. Licence recorded for each dependency. |
| Migration of V3 data unknown | Medium | Medium | Out of Phase 0. Handled in Phase 1E. |

---

## 13. Questions

The full question batch is in [docs/open-questions.md](../open-questions.md). Phase 0 needs these answers first: E-1 (build environment), 25 (hosting and SSO provider), and the data-model items for Phase 1A (section 24.3).

---

## 14. Recommended next step

1. The product owner reviews this note, ADR-0003 and ADR-0010, and answers the batch in `docs/open-questions.md`.
2. Platform decides the backend build environment (E-1).
3. After approval, the backend skeleton is built in the order of section 3.
