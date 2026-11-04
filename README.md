# EDM 2.0 — Expense Delegation Matrix

Enterprise approval-governance product for banks and regulated organisations, by Warmbytes (Pvt) Ltd.

The authoritative specification is [docs/EDM2-Master-Prompt.md](docs/EDM2-Master-Prompt.md). Where the specification and any other document differ, the specification wins.

## Current status

| Area | Status |
|---|---|
| Specification saved in repository | Done (`docs/EDM2-Master-Prompt.md`) |
| Phase 0 design note | Drafted, awaiting product-owner review (`docs/phase-0/design-note.md`) |
| Architecture decision records | 11 records, see `docs/adr/` |
| Permission matrix (Part 20) | Drafted (`docs/permission-matrix.md`) |
| Phase 1A data-model design note | Drafted, awaiting approval (`docs/phase-1a/data-model-design-note.md`) |
| Open questions for the product owner | Batch ready (`docs/open-questions.md`) |
| Web application shell (`web/`) | Built: navigation from Part 14.1, design-system primitives, role-based menu, tests |
| Backend (`backend/`) | **Not started.** The build sandbox cannot reach NuGet or the .NET 10 SDK. See the design note, section 2. |
| Database migrations | Not started (Phase 1A, after approval) |

No business module (currency, categories, routing, requests) is built yet. Phase 0 is in progress.

## Repository layout

```
docs/         specification, ADRs, design notes, open questions, permission matrix
web/          React + strict TypeScript + Vite shell (Fluent UI React v9, Tailwind for layout)
.github/      CI workflows (web-ci: typecheck, lint, tests, build, licence scan, audit)
```

The backend solution layout is fixed in `docs/phase-0/design-note.md`, section 3.

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

In development the header offers a **role switcher** so the menu can be checked for each role in Part 20.2. Production builds never show it and have no session until the backend provides one, so protected screens show the sign-in state.

## Principles that apply to every change

- Business rules live in the application layer, never in stored procedures.
- Every endpoint checks permissions on the server. UI permission checks are cosmetic only.
- Money is decimal, never floating point. Timestamps are UTC and shown in Asia/Karachi.
- Every state change writes an audit event (once the audit store exists).
- Unspecified behaviour is logged as an open question, marked Not Yet Verified, and never guessed.

## Contributing

Work on the feature branch for the session. Keep commits small and write clear messages. Add an ADR for any significant technical decision. Do not commit secrets, build output or `node_modules`.
