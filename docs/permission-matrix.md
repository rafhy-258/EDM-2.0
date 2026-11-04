# Permission Matrix (Master Prompt Part 20)

This document lists the permission strings and the roles that hold them. It is a Phase 0 deliverable. The seed data and the server-side checks are the source of truth. This document is derived from Part 20 and must be updated through a Change Request when Part 20 changes.

**Count:** 49 permission strings.
**Rule:** every endpoint declares exactly one required permission (ADR-0007). Approval actions also check the assignment, not the role (Part 20.1).
**Data scope** (own, department, all) applies on top of a permission.

## 1. Permissions

| Group | Permission | Meaning |
|---|---|---|
| Requests | `opex.request.create` | Create an OPEX request |
| Requests | `opex.request.view.own` | View own OPEX requests |
| Requests | `opex.request.view.department` | View OPEX requests in own department |
| Requests | `opex.request.view.all` | View all OPEX requests |
| Requests | `opex.request.edit.returned` | Edit returned OPEX requests |
| Requests | `capex.request.create` | Create a CAPEX request (also needs eligibility, Part 7.1) |
| Requests | `capex.request.view.own` | View own CAPEX requests |
| Requests | `capex.request.view.department` | View CAPEX requests in own department |
| Requests | `capex.request.view.all` | View all CAPEX requests |
| Requests | `capex.request.edit.returned` | Edit returned CAPEX requests |
| Approvals | `approval.inbox.view` | Open the Approval Inbox |
| Approvals | `approval.act` | Act on an approval (also requires an active assignment) |
| Approvals | `approval.view.assigned` | View requests assigned to the user |
| Configuration | `config.category.view` | View categories |
| Configuration | `config.category.manage` | Manage categories |
| Configuration | `config.subcategory.manage` | Manage subcategories |
| Configuration | `config.matrix.view` | View the delegation matrix |
| Configuration | `config.matrix.manage` | Create and edit delegation rules (maker) |
| Configuration | `config.matrix.approve` | Approve delegation matrix changes (checker) |
| Configuration | `config.ec.pool.manage` | Manage the EC member pool |
| Configuration | `config.ec.level.manage` | Manage EC levels |
| Configuration | `config.ra.manage` | Manage recommending authorities |
| Configuration | `config.limit.manage` | Manage the department-head limit |
| Configuration | `config.currency.manage` | Manage currencies and rates |
| Configuration | `config.capex.eligibility.manage` | Manage CAPEX eligible departments |
| Configuration | `config.actiontags.manage` | Manage action tag sets |
| Configuration | `config.policy.view` | View live policy versions |
| Organisation | `org.department.manage` | Manage departments |
| Organisation | `org.branch.manage` | Manage branches |
| Organisation | `org.hierarchy.manage` | Manage the organisational hierarchy |
| Organisation | `org.user.view` | View users |
| Organisation | `org.user.manage` | Manage users |
| Organisation | `org.sync.run` | Run the HR or directory sync |
| Identity and access | `iam.role.manage` | Manage roles |
| Identity and access | `iam.permission.assign` | Assign permissions to roles |
| Identity and access | `iam.sso.manage` | Manage SSO settings |
| Identity and access | `iam.mfa.manage` | Manage MFA settings |
| Reports | `report.view.own` | View own reports |
| Reports | `report.view.department` | View department reports |
| Reports | `report.view.all` | View all reports |
| Reports | `report.export` | Export reports |
| Audit | `audit.view` | View the audit store |
| Audit | `audit.export` | Export the audit store |
| Audit | `audit.security.view` | View security events |
| System | `settings.manage` | Manage general settings |
| System | `notification.template.manage` | Manage notification templates |
| System | `retention.manage` | Manage data retention |
| System | `health.view` | View system health |
| System | `integration.manage` | Manage integrations |

## 2. Roles (Part 20.2)

Department Head is not a stored role. Its extra rights come from the hierarchy (`department_heads`) and from being assigned at a step. Approver is assignment-based.

| Role | Permissions |
|---|---|
| Requester | `opex.request.create`, `opex.request.view.own`, `opex.request.edit.returned`, `capex.request.create`* , `capex.request.view.own`, `capex.request.edit.returned`, `approval.inbox.view`, `report.view.own` |
| Department Head (from hierarchy, added to Requester) | `opex.request.view.department`, `capex.request.view.department`, `report.view.department`, and `approval.act` when assigned |
| EDM Administrator | All `config.*` except `config.matrix.approve`; `org.department.manage`, `org.branch.manage`, `org.hierarchy.manage` and `org.user.view` are **not** granted |
| Configuration Approver | `config.matrix.view`, `config.matrix.approve`, `config.policy.view`. Cannot approve own change. |
| Auditor | `opex.request.view.all`, `capex.request.view.all`, `report.view.all`, `report.export`, `audit.view`, `audit.export`, `audit.security.view`, `config.policy.view` |
| Finance Viewer | `opex.request.view.all`, `capex.request.view.all`, `report.view.all`, `report.export` |
| System Administrator | `org.user.manage`, `org.sync.run`, all `iam.*`, `settings.manage`, `notification.template.manage`, `retention.manage`, `health.view`, `integration.manage`. No approval, no config approval, no audit alteration. |
| System Administrator, **additions approved 04-Nov-2026 (1A-8)** | `org.department.manage`, `org.branch.manage`, `org.hierarchy.manage`, `org.user.view`. **Not in Part 20.2.** Added because no role otherwise holds them, which would make the Departments, Branches, Hierarchy and Users screens unreachable. Marked PROPOSED in `web/src/session/roles.ts` until open question 1A-8 is answered. |

\* CAPEX create also needs CAPEX eligibility (Part 7.1, seeded to Admin, Information Technology and Digital).

**Open points (see open-questions.md, items 1A-8 and 1A-9):**
- Part 20.2 grants no role `org.department.manage`, `org.branch.manage` or `org.hierarchy.manage`. The proposed additions above fix this.
- The Department Head fixture is the hierarchy-derived role from Part 20.2 (Requester rights, plus department view, `report.view.department` and `approval.act` when assigned).

## 3. Permission string note

Part 20.2 uses `iam.*` and `config.*` shorthand. The seed data expands these to the explicit strings listed in section 1. Expansion happens once in the seed, never at check time.

## 4. Tests required (Phase 0)

- Every endpoint declares exactly one permission from section 1 (architecture test).
- Every permission in section 1 is referenced by at least one endpoint or navigation item, or is documented as reserved.
- Role seed matches section 2 (unit test on the seed definition). The web fixtures in `web/src/session/roles.ts` are tested the same way (`roles.test.ts`).
- Every Phase 1 navigation item is held by at least one role (`navigation.test.ts`), so no screen is unreachable.
- Denied case for each role on at least one endpoint per group (integration test).
