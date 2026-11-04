# ADR-0002: Purpose-built, matrix-driven approval engine (no generic BPM)

**Status:** Accepted
**Date:** 2026-11-04

## Context
Part 2 requires a purpose-built, matrix-driven engine and allows a generic BPM engine (Camunda, Elsa, Flowable and similar) only if an ADR justifies it. Routing is a deterministic function of configuration (Part 4), and the approval lifecycle has a fixed set of states (Part 22). The Phase 2 Workflow Designer edits the delegation matrix and must not become a second routing system.

## Decision
- Routing is a pure domain service (no database, no HTTP). Input: a request snapshot and a policy version. Output: route type, ordered steps, rate snapshot and a decision trace (Part 4.10).
- The approval lifecycle is an explicit state machine in the Approval Engine domain. Transitions are methods on aggregates with guards. Illegal transitions throw a domain error.
- Persistence stores routes, steps, actions and votes as separate tables per nature (Part 13).
- A generic BPM engine is rejected. Its runtime, persistence model and designer would duplicate the matrix, and they would make the decision trace harder to guarantee.

## Consequences
- Full control over the 14 routing scenarios and the decision trace.
- We own the state machine and its tests. The 100% branch coverage target on routing is stated in Part 19.

## Alternatives considered
- Elsa, Camunda, Flowable: rejected (see Decision). Their licences would also need review.
