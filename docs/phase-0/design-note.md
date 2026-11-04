# Phase 0 Design Note: Foundations

**Product:** EDM 2.0 (Expense Delegation Matrix), Warmbytes (Pvt) Ltd
**Specification:** [docs/EDM2-Master-Prompt.md](../EDM2-Master-Prompt.md), Parts 0 to 3, 10, 11, 13, 19 to 21 (read-first list for Phase 0)
**Status:** Draft for product-owner review. Nothing in this note is approved until the owner replies "approved", per Standing Preamble 24.1.

---

## 1. Goal and scope

Goal: a secure, observable, tested skeleton on which every later module is built (Part 24.2).

| # | Scope item (Part 24.2) | Plan in this note | Status today |
|---|---|---|---|
| 1 | Solution layout, modular monolith, React app shell with navigation from 14.1 | Sections 3 and 6 | Frontend shell built here. Backend skeleton blocked (section 2). |
| 2 | Component library decision as ADR, design system base | ADR-0003, section 5 | Decision recorded. Design-system primitives built in the web shell. |
| 3 | Authentication: local accounts, SSO skeleton (OIDC, SAML), MFA hooks, lockout, session timeout | ADR-0006, section 7 | Design only. Backend blocked. |
| 4 | Organisation and identity: users, departments, branches, hierarchy, Department Head mapping, roles, permissions, seeds from Part 20, CSV or API import | Section 7 | Design only. Backend blocked. |
| 5 | Audit store with one write service, basic viewer | ADR-0005, section 8 | Design only. Backend blocked. |
| 6 | API conventions: ProblemDetails, error catalogue, paging, ETag and If-Match, Idempotency-Key, OpenAPI with permission per endpoint | ADR-0007, section 9 | Design only. Backend blocked. |
| 7 | Logging, correlation ids, health checks, CI (build, test, lint, dependency and licence scan), Docker Compose, seed data, README | Section 10 | CI workflow for the web shell can be written and run here. Backend CI blocked. |

**Out of scope (Part 24.2):** any business module. No currencies, categories, EC, rules, requests or routing in Phase 0.

---

## 2. Environment constraints and what can be verified now

The build sandbox used for this session was checked during Phase 0 planning:

| Requirement | Reachable from this sandbox? | Impact |
|---|---|---|
| npm registry (`registry.npmjs.org`) | Yes | The React shell can be installed, built, linted and tested here. |
| GitHub | Yes | Repo and CI workflow files are managed here. |
| PyPI | Yes | Not needed for Phase 0 backend. |
| NuGet (`api.nuget.org`) | **No** | .NET packages cannot be restored. The backend cannot be compiled or tested here. |
| .NET 10 SDK download (`dot.net`, `builds.dotnet.microsoft.com`) | **No** | The .NET SDK is not installed and cannot be installed here. |
| Docker, PostgreSQL | Not installed | Integration tests (Testcontainers) cannot run here. |

**Consequence.** Backend C# code is not written in this turn. Writing unverified code would break Part 0 rule 3 and the standing rule "keep the repository always building and the tests green". The backend starts once one of these happens:

1. Outbound access to `api.nuget.org` and the .NET SDK download host is allowed for this sandbox, **or**
2. A machine or CI runner with the .NET 10 SDK, Docker and NuGet access is used for the backend build.

**Decision requested from the product owner or platform team:** choose option 1 or 2 (see open-questions document, item E-1).

---

## 3. Solution layout

```
EDM-2.0/
├── README.md
├── docs/
│   ├── EDM2-Master-Prompt.md          # authoritative specification (single source of truth)
│   ├── open-questions.md              # Part 18 batch and new questions
│   ├── adr/                           # architecture decision records
│   ├── phase-0/design-note.md         # this note
│   ├── phase-1a/data-model-design-note.md
│   └── permission-matrix.md           # Part 20 catalogue (Phase 0 deliverable)
├── backend/                           # BLOCKED until SDK and NuGet access (section 2)
│   ├── EDM.sln
│   ├── src/
│   │   ├── Host/                      # composition root: ASP.NET Core host, OpenAPI, Serilog, health, auth
│   │   ├── BuildingBlocks/            # Result types, Money, ProblemDetails, paging, correlation, audit abstractions
│   │   └── Modules/
│   │       ├── IdentityOrganisation/  # Domain, Application, Infrastructure, Api
│   │       ├── Configuration/         # Phase 1A
│   │       ├── ExpenseOpex/           # Phase 1C
│   │       ├── ExpenseCapex/          # Phase 1D
│   │       ├── ApprovalEngine/        # Phase 1B (routing library first)
│   │       ├── Notifications/         # Phase 1C
│   │       ├── Reporting/             # Phase 1E
│   │       ├── Audit/                 # Phase 0
│   │       └── Integrations/          # Phase 2 (empty shell only)
│   └── tests/
│       ├── UnitTests/                 # xUnit
│       ├── IntegrationTests/          # xUnit + Testcontainers (PostgreSQL)
│       └── ArchitectureTests/         # NetArchTest: layer and module rules, endpoint permission test
├── web/                               # React + strict TypeScript + Vite (BUILT in Phase 0)
├── deploy/
│   ├── docker-compose.yml             # PostgreSQL, app (BLOCKED until Dockerfile can be tested)
│   └── iis/                           # Phase 1E
└── .github/workflows/
    └── web-ci.yml                     # web build, test, lint, licence check
```

Module empty shells follow the four-folder rule (Domain, Application, Infrastructure, Api) from ADR-0001. Shells are created only when the backend is buildable.

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
- Viewer: global list with filters (date range, user, department, module, action, record, outcome), detail with before and after diff, export to CSV and Excel. Viewing the audit store is itself an audit event.
- Phase 0 viewer covers user and department changes, which are the Phase 0 "Done when" examples.

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
  - `backend-ci.yml` (after section 2 is resolved): restore, build with warnings as errors, unit tests, architecture tests, integration tests with Testcontainers, dependency vulnerability scan, licence scan, SBOM generation.
- **Docker Compose:** PostgreSQL service and app service, with the app image built from a multi-stage Dockerfile. Needs Docker, so it is not testable here.
- **One-command start:** `make dev` (or equivalent) starts PostgreSQL, runs migrations and seed, then starts the API and web dev server. Written in Phase 0 once the backend exists.
- **Seed data:** PKR currency with rate 1, the permission catalogue, the six roles, the CAPEX eligibility default (Admin, Information Technology, Digital) as a Phase 1D seed, and development users (development profile only).

---

## 11. Phase 0 Done criteria (Part 24.2) and status

| Done criterion | Status |
|---|---|
| A user can sign in | Blocked (backend). The web shell has a sign-in placeholder. |
| The menu shows only what the role permits | Built and tested in the web shell (cosmetic filter, fixture permissions). Server-side session is blocked. |
| Creating or changing a user or department writes a correct audit event visible in the viewer | Blocked (backend). |
| A test proves every endpoint declares a permission | Blocked (backend). Design fixed in section 9. |
| CI is green | Web CI built here. Backend CI blocked. |
| One command starts everything locally | Blocked (needs Docker and backend). Web-only dev server works now. |

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
