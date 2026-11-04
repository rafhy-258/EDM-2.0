# ADR-0001: Modular monolith with clean architecture

**Status:** Accepted
**Date:** 2026-11-04

## Context
EDM 2.0 is a bank-deployable, on-premise product with nine business areas (Part 2). The team is small, deployment must be simple (Docker or Windows/IIS), and the data is strongly transactional. Microservices would add distributed-transaction and operations cost without a scaling need (Part 19 assumes 10,000 users and 300 concurrent users).

## Decision
- One deployable ASP.NET Core (.NET 10 LTS) application composed of modules: Identity & Organisation, Configuration, Expense-OPEX, Expense-CAPEX, Approval Engine, Notifications, Reporting, Audit, Integrations.
- Each module has four projects or folders: `Domain`, `Application`, `Infrastructure`, `Api`.
- Dependency rule: Api → Application → Domain. Infrastructure implements Application ports. Domain has no framework dependencies.
- No module reads or writes another module's tables. Cross-module work goes through application interfaces (synchronous queries) or domain events published through the outbox (asynchronous).
- Architecture tests (NetArchTest) fail the build if the dependency rule or the no-cross-module-table rule is broken.
- OPEX and CAPEX are separate modules with separate tables. Shared code is extracted into a shared kernel or shared domain services, not copied (Part 24.6).

## Consequences
- Clear seams for a later split if ever needed, without paying that cost now.
- Some discipline is needed to keep module boundaries honest. Architecture tests make that cheap.

## Alternatives considered
- Microservices: rejected for operational cost and distributed transactions.
- Single flat project: rejected, because it would repeat V3's mixed responsibilities.
