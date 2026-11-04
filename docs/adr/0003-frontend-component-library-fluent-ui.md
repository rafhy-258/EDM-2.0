# ADR-0003: Frontend component library: Fluent UI React v9

**Status:** Accepted, pending Phase 0 design review
**Date:** 2026-11-04

## Context
Part 2 asks us to choose between **shadcn/ui** (Radix primitives, copied into the codebase, styled with Tailwind) and **Fluent UI React v9** (Microsoft's component library, MIT licensed, npm package `@fluentui/react-components`). Part 14.2 names Microsoft Fluent 2 as the design language. The product must meet WCAG 2.2 AA, support keyboard navigation, allow right-to-left layout later, and be maintainable for at least ten years in a bank.

## Options compared

| Criterion | shadcn/ui (Radix) | Fluent UI React v9 |
|---|---|---|
| Licence | MIT (Radix MIT) | MIT |
| Ownership of code | Components copied into our repo. We own all upgrades. | Versioned npm dependency. Upgrades via semver. |
| Accessibility | Radix primitives are strong. We must check each composed component ourselves. | Built-in focus management, ARIA and high-contrast tokens. Tested by Microsoft. |
| Design language | Neutral. We define everything. | Fluent 2, which Part 14.2 names as the reference. |
| Tables | Build on TanStack Table ourselves | DataGrid exists. We intend TanStack Table rendered with Fluent table primitives, for control. |
| Styling | Tailwind native | Design tokens and Griffel. Tailwind used for layout only. |
| RTL | Achievable with logical CSS properties | Built-in direction support through the Fluent provider |
| Supply-chain risk | Smaller surface, but more code we must review | Larger dependency tree from a single well-known publisher |
| Enterprise fit | Good | Good. Common in Microsoft-centric banks (AD, Office, Teams). |

## Decision
Use **Fluent UI React v9** (`@fluentui/react-components`) as the component library. Use **Tailwind CSS** for page-layout utilities only. Use **TanStack Table** for data grids rendered with Fluent table primitives.

Rationale: the accessibility and enterprise design-language requirements are met by a maintained, tested library rather than by assembling and auditing our own. We accept ownership of upgrades as the trade-off.

## Consequences
- Tailwind preflight is disabled so that its global resets do not fight Fluent tokens.
- Theme tokens are defined once in `web/src/design/tokens.ts`.
- The licence scan in CI records MIT for every Fluent package.
- TanStack Table is pinned to **v8** (`useReactTable`). Version 9 has a different hook API (`useTable`, `createTableHook`) that has not been adopted yet. Moving to v9 is a separate decision.
- Icon and component names are checked against the installed package at build time (TypeScript), not from memory.

## Reversal cost
High once many screens exist. The decision must be confirmed in the Phase 0 design review before the design system is built beyond the shell.
