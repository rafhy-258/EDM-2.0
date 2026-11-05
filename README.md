# EDM 2.0 — Expense Delegation Matrix

Enterprise approval-governance product for banks and regulated organisations, by Warmbytes (Pvt) Ltd.

The authoritative specification is [docs/EDM2-Master-Prompt.md](docs/EDM2-Master-Prompt.md). Where the specification and any other document differ, the specification wins.

## Current status

| Area | Status |
|---|---|
| Specification saved in repository | Done (`docs/EDM2-Master-Prompt.md`) |
| Phase 0 design note | Approved by the product owner, 04-Nov-2026 (`docs/phase-0/design-note.md`). SSO provider (Q25) still open |
| Architecture decision records | 11 records, see `docs/adr/`. ADR-0003 and ADR-0010 accepted 04-Nov-2026 |
| Permission matrix (Part 20) | Drafted (`docs/permission-matrix.md`) |
| Phase 1A data-model design note | Drafted, awaiting approval (`docs/phase-1a/data-model-design-note.md`) |
| Open questions for the product owner | Batch ready (`docs/open-questions.md`) |
| Web application shell (`web/`) | Built: navigation from Part 14.1, design-system primitives, role-based menu, Activity Logs viewer (`/audit/activity`), tests |
| Backend (`backend/`) | Built: shared kernel, session-cookie authentication, Audit module (store, writer, viewer API), Identity module (local database accounts, sign-in with lockout, first-administrator bootstrap), Configuration module (categories and subcategories, Phase 1A slice 1), Host. Sign-in is built and backend CI was green on b5e6f57 (112 tests). Categories and subcategories (Phase 1A slice 1) passed backend CI on 0455500 (103 unit, 12 architecture, 38 integration tests). SSO waits for Q25. |
| Database migrations | Audit, identity and categories schemas. Currency, EC, RA, limits, delegation and policy migrations follow in Phase 1A |
| One-command local start | `scripts/dev.ps1` for Windows PowerShell, with PostgreSQL already running. Not yet run by the agent (no .NET or PowerShell in the sandbox) |

No business module (currency, categories, routing, requests) is built yet. Phase 0 is in progress; see the done-criteria table in the design note, section 11.

## Repository layout

```
docs/         specification, ADRs, design notes, open questions, permission matrix
web/          React + strict TypeScript + Vite shell (Fluent UI React v9, Tailwind for layout)
backend/      .NET 10 solution: shared kernel, web adapters, host, Audit module, three test projects
.github/      CI workflows (web-ci, backend-ci)
```

The backend solution layout is in `docs/phase-0/design-note.md`, section 3. Backend setup and local checks are in `backend/README.md`.

## Run everything locally (Windows, PowerShell)

Requires the .NET 10 SDK, Node.js 22 or later, and PostgreSQL 18 running on this machine. Docker is not used.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

The `-ExecutionPolicy Bypass` option is needed on a default Windows install, where scripts are blocked. It applies to this run only. The script checks the SDK and Node, installs web packages on first run, asks for the PostgreSQL password (hidden, kept in memory only), creates the `edm` database if it is missing, and starts the API with its database schema applied. Then it starts the web dev server. Open http://localhost:5173. Logs are in `artifacts\local\`. Press Ctrl+C to stop both. Options are listed in the script header (`Get-Help .\scripts\dev.ps1`).

On a new database the script asks you to create the first administrator account (username, then a hidden password of at least 12 characters). Sign in with that account at http://localhost:5173. The password is never written to disk.

Docker Compose is not written yet, and PostgreSQL itself is not started by the script. Docker is not used on this project.

## Run the web shell

Requires Node.js 22 or later.

```bash
cd web
npm ci
npm run dev        # http://localhost:5173 (also exposed on 0.0.0.0 for preview)
npm test           # unit and component tests
npm run typecheck
npm run lint
npm run build
npm run licences   # production dependency licence allow-list
```

Sign in with a local account. In development the sign-in page also offers a **role fixture** (no account needed), and the header shows a role switcher so each role's menu can be checked (Part 20.2). Production builds have no fixture path. The session comes from the server, and without one the sign-in page is shown.

## Principles that apply to every change

- Business rules live in the application layer, never in stored procedures.
- Every endpoint checks permissions on the server. UI permission checks are cosmetic only.
- Money is decimal, never floating point. Timestamps are UTC and shown in Asia/Karachi.
- Every state change writes an audit event (once the audit store exists).
- Unspecified behaviour is logged as an open question, marked Not Yet Verified, and never guessed.

## Contributing

Work on the feature branch for the session. Keep commits small and write clear messages. Add an ADR for any significant technical decision. Do not commit secrets, build output or `node_modules`.
