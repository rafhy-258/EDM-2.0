# EDM 2.0 — Master Build Prompt

Product owner: Sardar Rafhy, Warmbytes (Pvt) Ltd. Version 1.0, written for the AI build agent.

This prompt is authoritative. Legacy EDM_V3 behaviour is described only to preserve business rules. Where this prompt and V3 differ, this prompt wins. Anything marked Not Yet Verified, or listed in Part 18 (Open Questions), must be confirmed with the product owner and never guessed.

> Stored verbatim in the repository as the single source of truth. Change it only through a Change Request (Part 24.9).

---

## Part 0 — Rules of engagement for the agent

1. Read the whole prompt before writing code. Build in the phase order of Part 15. Finish, test and demonstrate one module before starting the next.
2. Do not invent functionality. If a rule, field, lookup value or screen is not specified, add it to Part 18, mark it Not Yet Verified, and ask. Ideas of your own must be labelled Proposed Enhancement and approved before building.
3. Business rules live in the .NET application and domain layer, never in database stored procedures. Routing is a deterministic, unit-tested domain service that returns a routing decision plus a human-readable decision trace.
4. Every state-changing action writes an audit event with the context defined in Part 10.
5. Security by default: deny by default, server-side authorisation on every endpoint, no business decision trusted from the client.
6. For every module deliver in this order: design note, database migration, domain and application code, API, UI, automated tests, acceptance checklist. Deliver chapter by chapter and never compress a module to save space.
7. Do not repeat the V3 defects listed in section 1.4.
8. Ask questions in one batch per module and keep building everything that is not blocked by the question.

---

## Part 1 — Product vision and principles

### 1.1 What EDM is

EDM (Expense Delegation Matrix) is an enterprise approval-governance product for banks and other regulated organisations. An administrator configures the system once: categories and subcategories, the delegation matrix, the Expense Committee (EC) with its levels, recommending authorities, the department-head limit, and currencies. After that, a user who creates an OPEX or CAPEX request only chooses a category and subcategory. The system detects the correct approval route automatically. Nobody picks approvers by hand beyond the Phase-1 forwarding described in Part 4.

### 1.2 Principles (non-negotiable)

- **Configuration over code.** Every limit, level, member, authority, rule and permitted action is data an administrator can change at any time, with no deployment.
- **Explainable routing.** Each request stores exactly which rule, level, limit, exchange rate and policy version decided its route.
- **Auditable by design.** Every action in every module is logged with who, when, where from, and what changed (before and after). Audit records cannot be edited or deleted through the application.
- **Least privilege.** Real role-based access control on every screen and endpoint.
- **OPEX and CAPEX separation.** Separate request, routing, approval and configuration tables and screens. Shared field definitions and shared domain services are allowed.
- **Bank deployable.** On-premise first, AD/LDAP and SAML/OIDC single sign-on, no mandatory cloud dependency, hardened and penetration-test ready.
- **Tenant-ready.** A tenant_id on every business table from day one, although the first deployments are single-tenant.
- **Accessible and responsive.** Desktop-first, usable on tablet and mobile, keyboard navigable, WCAG-aligned.

### 1.3 Actors

Requester; Department Head (Phase-1 approver); Recommending Authority (RA); EC Member; CFO; CEO; Head of Admin & GST; EDM Administrator; Configuration Approver (maker-checker); Auditor / Compliance (read-only); Finance Viewer; System Administrator. Role definitions are in Part 11.

### 1.4 V3 defects that must not be repeated

- No role checks on admin pages. The Reporting page treated every signed-in user as Finance_Admin.
- CAPEX-eligible departments hard-coded in application config (Admin, Information Technology, Digital).
- Routing and validation hidden inside stored procedures, two parallel table families, legacy tables, and a cross-database dependency on ApplicationPanel.
- Currency deletion checked usage in tables named tbl_Opex and tbl_Capex instead of the real request tables.
- The department-head limit was one global record although the procedures join category tables. The exceeding-limit setting was stored but not enforced.
- YTD and MTD amounts typed in by the user. Unit Head chosen by the user from a free dropdown.

---

## Part 2 — Technology and architecture

| Area | Standard |
|---|---|
| Backend | C# / ASP.NET Core on .NET 10 LTS |
| Architecture | Modular monolith with clean architecture. Modules: Identity & Organisation, Configuration, Expense-OPEX, Expense-CAPEX, Approval Engine, Notifications, Reporting, Audit, Integrations. Each module has Domain, Application, Infrastructure and Api layers. No cross-module table access. Modules talk through application interfaces and domain events. |
| API | REST with OpenAPI, versioned under /api/v1, RFC 9457 ProblemDetails errors, paged lists, idempotency key on request submission. |
| Database | PostgreSQL. EF Core migrations. snake_case names. Money numeric(18,2). Exchange rates numeric(18,4). Timestamps timestamptz stored in UTC and displayed in Asia/Karachi. Optimistic concurrency on every mutable row. Soft delete only where deletion is allowed. tenant_id on every business table. No business rules in stored procedures. |
| Frontend | React with strict TypeScript, Vite, TanStack Query, React Hook Form with Zod, TanStack Table, Tailwind CSS. Accessible component library: choose between shadcn/ui (Radix) and Fluent UI React v9 and justify the choice in a design note before coding. |
| Authentication | AD/LDAP and SAML2/OIDC single sign-on, local accounts as fallback, optional MFA, lockout, session timeout, password policy. |
| Workflow | A purpose-built, matrix-driven approval engine implemented as a state machine (Part 4). Do not adopt a generic BPM engine unless an architecture decision record justifies it. The visual Workflow Designer (Phase 2) edits the delegation matrix and must never become a second routing system. |
| Background work | Scheduler such as Quartz.NET. Transactional outbox for emails and integrations, with retry and back-off. |
| Files | Storage abstraction (file system, network share, S3-compatible). Size and type limits, antivirus scan hook, SHA-256 stored per file, never served from a public web root. |
| Observability | Structured logging (Serilog), OpenTelemetry, health checks, correlation id on every request and audit event. |
| Deployment | Docker images plus a Windows Server / IIS option. Environment-based configuration. No secrets in source. Migrations and seed data run on deploy. Backup and restore documented. |
| Quality | xUnit unit tests, integration tests against real PostgreSQL (Testcontainers), Playwright UI tests, static analysis, dependency and licence scanning, SBOM. Record the licence of every dependency and avoid restrictive licences for a commercial product. |

Reference numbers follow a configurable pattern. Default: TYPE-YEAR-6-digit sequence, for example OPEX-2026-000123. The V3 format is Not Yet Verified.

---

## Part 3 — Glossary

- **Phase 1:** the Department Head approval chain. The first approver is always the requester's Department Head. A Phase-1 approver may forward to another user, so the chain can be A, then B, then C. The last Phase-1 approver is the person who approves.
- **Phase 2:** the stages after Phase 1: Recommending Authority, Expense Committee, then CFO and/or CEO.
- **Delegation rule (EDM rule):** one configuration record per expense nature (OPEX or CAPEX), category and optional subcategory. It defines the route: recommending authorities plus either an EC level route or a standalone approver.
- **EC level:** a named, admin-defined level with a PKR amount range, a list of assigned members (EC members, optionally CEO and CFO), a required member count, a minimum approvals count, and flags for CFO-required and CEO-required. The level name is a free label such as EC Level 1. The number in the name carries no meaning. The amount range alone decides which level applies. The naming convention is an open question (Part 18).
- **Standalone approval:** after the Department Head approves, exactly one named authority approves (CEO, CFO or Head of Admin & GST) and nobody else is involved. No RA, no EC.
- **Quorum:** the minimum number of approvals configured on an EC level. When it is reached, the remaining members' tasks close as Quorum Met.
- **Hot fields:** approval amount, currency, category, subcategory and the Already Approved flag. Changing any of them during a revert correction forces a fresh approval cycle.
- **Policy version:** an immutable snapshot of all configuration used to route a request. In-flight requests always keep the version they were routed with.
- **Decision trace:** the stored, human-readable list of checks the engine performed and the outcome of each.
- **Revert recipient:** the user a reverted request is sent back to. Reverts always go back, never forward.

---

## Part 4 — Routing and approval rules (authoritative)

### 4.1 Currency conversion

- Every currency is configured with an active PKR rate. PKR itself has rate 1 and cannot be deactivated or deleted.
- If the user selects a currency other than PKR, the engine uses that currency's active PKR rate at the moment of submission: PKR amount = approval amount multiplied by active rate, rounded to 2 decimals (half away from zero).
- The request stores a snapshot: currency, rate, rate record id and timestamp. Later rate changes never alter existing requests.
- All limits and ranges (department-head limit, EC level ranges) are compared against the PKR amount.
- A currency with no active rate cannot be selected on a form.

### 4.2 Decision order at submission

The engine runs these checks in this exact order and stops at the first that applies.

1. Validate the request (Parts 6 and 7).
2. Convert the amount to PKR (4.1).
3. Resolve the Department Head (first approver). Default: derived from the requester's department and the organisational hierarchy, with a choice among eligible heads. V3 let the user pick a Unit Head from a dropdown.
4. Already Approved ticked produces Route A.
5. PKR amount at or below the active department-head limit produces Route B. The limit starts at 0 and is set by the administrator to any value. If no active limit exists, use the system setting DefaultDeptHeadLimitPkr, seeded at 20,000 as in V3.
6. Find the delegation rule for nature, category and subcategory. Prefer a rule that names the subcategory, then fall back to a category-wide rule (Not Yet Verified, see Part 18). If no active rule exists, block the submission (4.9).
7. Rule type:
   - Standalone produces Route C.
   - EC-based: pick the EC level whose range contains the PKR amount and produce Route D. If no level matches, block the submission (4.9).

Boundaries follow the same rule as the department-head limit (at or below): an EC level range is lower bound exclusive and upper bound inclusive. An amount exactly equal to the department-head limit therefore belongs to Route B, and an amount exactly equal to a level's upper bound belongs to that level.

```
resolveRoute(request):
  pkr = convertToPkr(request)            # snapshot rate
  head = resolveDepartmentHead(request)
  if request.alreadyApproved:  return RouteA(head)
  if pkr <= activeDeptHeadLimit: return RouteB(head)
  rule = findRule(nature, category, subcategory)
  if rule is null: throw NoRouteConfigured
  if rule.isStandalone: return RouteC(head, rule.standaloneApprover)
  level = rule.ecLevels.first(l => l.min < pkr and pkr <= l.max)
  if level is null: throw NoRouteConfigured
  return RouteD(head, rule.recommendingAuthorities, level)
```

### 4.3 Route templates

- **Route A — Already Approved.** Department Head only. When the Department Head approves, the request is APPROVED and final. The requester and the approver can download the PDF and other export formats. An approved-document attachment is mandatory (V3 rule). Requests using this route are flagged in reports.
- **Route B — Within department-head limit.** Department Head only, then APPROVED. No RA, no EC, no standalone approver.
- **Route C — Standalone.** Department Head first, then the single standalone approver (CEO, CFO or Head of Admin & GST), then APPROVED. No RA and no EC.
- **Route D — EC route.** Department Head (Phase 1, may forward), then Recommending Authority, then the EC members of the selected level (parallel, until quorum), then CFO if required, then CEO if required. When both are required the order is CFO, then CEO. Then APPROVED.

The Department Head approval is always the first approval in every route.

#### 4.3.1 Recommending authority assignment

A rule has at least one recommending authority. If several are assigned, any one of them may act and the first decision completes the RA stage (Not Yet Verified, see Part 18).

### 4.4 Actions and action tags

The available actions are APPROVE, REJECT, REVERT and FORWARD. Which actions a person sees is controlled by an action tag set that the administrator configures per designation and per standalone or EC assignment. Defaults:

| Actor | Default actions |
|---|---|
| Department Head (Phase 1) | Approve, Forward, Reject, Revert |
| Recommending Authority | Approve, Reject, Revert |
| EC Member | Approve, Reject, Revert. Never Forward. |
| CFO | Approve, Reject, Revert |
| CEO | Configurable: approve only, or approve, reject and revert. V3 behaviour at the EC stage is Approve and Revert (the CEO cannot reject). |
| Standalone approver (CEO, CFO, Head of Admin & GST) | Configurable per assignment, default Approve, Reject, Revert |

The server enforces the action tags. The UI only shows the permitted buttons. Remarks are mandatory for Approve, Reject and Revert. A Forward needs a recipient. Forward is available only in Phase 1.

### 4.5 Action semantics

- **Approve:** completes the actor's step. The next approver is notified by email.
- **Reject:** ends the request immediately with status REJECTED. A single rejection by anyone, including one EC member, ends it even when quorum was still possible. A rejected request is closed. No further actions.
- **Forward (Phase 1 only):** passes the request to another user. The forwarder is recorded in the chain.
- **Revert:** sends the request one step back.
  - A Phase-1 approver reverts to the previous person in the chain, or to the requester if there is none.
  - The Recommending Authority, any EC member, the CFO and the CEO revert to the last Phase-1 approver, the person who approved. Example: A sends to B, B forwards to C, C approves, the RA reverts, the request goes back to C.
  - Standalone approvers revert to the last Phase-1 approver (Not Yet Verified).
  - Comments are mandatory.
- Selective revert and revert-recipient editing follow the V3 behaviour: only the designated revert recipient (or the creator in My Requests) can open the editable form. Other users see the read-only approval view. The exact selective-revert semantics are Not Yet Verified (Part 18).

### 4.6 EC stage rules

- The level has N assigned members, minimum approvals M (M must not exceed N), and optional CFO and CEO requirements.
- When the RA approves, the system emails all N members in bulk and opens a task for each.
- Members act in parallel. A member who has already acted cannot act again at the same step.
- When M approvals are recorded, quorum is met. Open tasks of the other members close with the label Quorum Met and are read-only. Quorum evaluation must be transactional and race-safe: two simultaneous approvals can never produce two completions.
- When quorum is met the engine moves to CFO (if required), then CEO (if required), otherwise APPROVED.
- Progress (for example 3 of 5 approvals) is visible on the approval screen.

### 4.7 Resubmission after a revert

- If amount, currency, category, subcategory and the Already Approved flag are all unchanged, the flow continues from the stage that reverted it. Stages completed earlier stay valid. The votes of the reverting stage are cleared and logged (Not Yet Verified for EC votes, see Part 18).
- If any of those hot fields changed, the system discards the current route, keeps the old route in history, re-runs the full decision order using the current policy, and starts a fresh approval cycle from the Department Head.
- Every resubmission records what changed (before and after).

### 4.8 Policy versioning

Every change to categories, rules, EC levels, members, limits, currencies or recommending authorities creates a new policy version. A request stores the policy version it was routed with and keeps using it until a fresh cycle is triggered (4.7). Configuration screens show which version is live.

### 4.9 No matching configuration

If no department-head limit, rule or EC level matches, the system shows a clear error such as: *No approval level is configured for this amount. Please contact Admin or HR.* The request is not saved. The attempt is written to the audit log with the category, subcategory, nature and amount.

### 4.10 Decision trace

Store with every request, as structured data and as readable text: PKR amount and rate used, Department Head resolved, which checks ran and their results, rule id, level id, policy version, and the ordered list of approval steps created. Show it on the approval screen to authorised users and in audit exports.

### 4.11 Deferred

The OPEX/CAPEX unbudgeted (exceeding) limit module is out of scope for now. Reserve an extension point in the routing engine for it but build no behaviour.

### 4.12 Mandatory routing test scenarios

1. Already Approved at any amount goes to the Department Head only and finishes.
2. Amount exactly equal to the department-head limit goes to Route B. One paisa above goes to the rule.
3. Foreign-currency amount converts with the active rate and the snapshot is stored.
4. Standalone CEO rule: Department Head, then CEO, then done.
5. EC level with minimum 5 of 10 members: quorum closes the other five tasks.
6. One EC rejection ends the request.
7. CFO and CEO both required: CFO acts before CEO.
8. EC member revert returns to the last Phase-1 approver, who is C in an A to B to C chain.
9. Revert, edit remarks only, resubmit: continues at the reverting stage.
10. Revert, change amount, resubmit: fresh cycle from the Department Head.
11. Amount in no range: blocked with the configured message, nothing saved.
12. Configuration changed while a request is in flight: the request keeps its policy version.
13. Two EC members approve at the same instant: exactly one quorum completion.
14. Inactive member, deactivated rule or deactivated currency cannot be used for new requests.

---

## Part 5 — Configuration modules (administration)

These modules are configured once by an administrator and then drive every request. The delegation matrix (5.2) is the most important module in the product.

### 5.0 Behaviour common to every configuration module

- List screen with search, filters (including status), sorting, paging and column controls. Add and edit in a side panel or dialog.
- Activate and deactivate with a confirmation dialog. Delete only when the record is unused. Otherwise block the delete and show exactly where it is used (for example: used by 3 delegation rules).
- Validation messages appear next to the field and are specific. Duplicate checks run on the server.
- Every change writes an audit event with old and new values (Part 10) and creates a new policy version (4.8).
- Maker-checker: a setting per module lets a second authorised user approve a configuration change before it goes live. Default on for the delegation matrix, EC levels, limits and currency rates.
- Each module has an Audit Log tab with filters (date range, user, action, record) and export to Excel and CSV.
- Optimistic concurrency: if two administrators edit the same record, the second save is refused with a clear message.
- Permissions are checked on the server (Part 11).

### 5.1 Categories and Subcategories

Three tabs in the original module: Categories, Subcategories, Expense Delegation Matrix. In EDM 2.0 the matrix is its own screen (5.2) but is reachable from the same area.

**Category fields:** name (required, unique among active categories, up to 200 characters), description (optional, up to 500), status.

**Subcategory fields:** category (required), name (required, unique among active subcategories of the same category), description (optional), status.

**Rules**
- A category cannot be deleted while it has subcategories, or while any rule or request uses it. Deactivate instead.
- Deactivating a category or subcategory that active rules use shows a warning listing those rules. New requests can no longer select it. Existing requests are unaffected.
- Forms offer an Others category and Others subcategory that asks the user to type a name (stored as CategoryOther and SubcategoryOther). How V3 routes Others requests is Not Yet Verified (Part 18). Until confirmed, an Others request needs a rule like any other category.
- Audit tabs for categories and subcategories show who changed what, with old and new values.

### 5.2 Delegation Matrix (EDM rules)

**Purpose:** defines, for each nature, category and subcategory, how a request is routed. Once saved and active, the category and subcategory appear on the OPEX form (nature OPEX) or the CAPEX form (nature CAPEX). Only combinations with an active rule are selectable.

**List screen:** filter by nature, category, subcategory, route type, recommending authority and status. Columns: nature, category, subcategory, route type, recommending authorities, EC levels or standalone approver, status, last modified by and on.

**Create and edit (guided steps)**
1. Scope: nature (OPEX or CAPEX), category, and optional subcategory (the subcategory must belong to the category). A rule without a subcategory applies to the whole category.
2. Route type: one of EC Levels or Standalone.
3. EC Levels route: choose one or more active EC levels of the same nature. Their amount ranges must not overlap. Choose at least one Recommending Authority. Set the action tags per approver (4.4).
4. Standalone route: choose exactly one of CEO, CFO, Head of Admin & GST (the active holder of that designation) and its action tags. No recommending authority and no EC level apply. (V3 required a recommending authority on every rule. Confirm in Part 18.)
5. Review: a coverage view that lists the amount bands and the resulting approval chain for each band, and highlights gaps (amounts that would match nothing) and overlaps. A preview of the resulting chain for any test amount.

**Validations**
- Nature, category and route type are required.
- At least one active approver configuration is required.
- Amount limits are positive and have at most two decimal places.
- EC level ranges must not overlap. A gap is a warning, not an error, because an amount in a gap is blocked at submission by 4.9.
- A warning appears when the lowest level starts above the department-head limit, because amounts between them match nothing.
- No two active rules may share the same nature, category and subcategory.
- A rule cannot be activated if it references an inactive level, authority, member or designation holder.
- Deleting a rule that existing requests used is not allowed. Deactivate it.

**Effect of changes:** saving creates a new policy version. Requests already routed keep their version (4.8).

**Audit:** create, update, status change, delete, and failed validations. Each event stores the full before and after of the rule, its levels, its authorities and its action tags.

### 5.3 Expense Committee

Two tabs: Approval Workflow and EC Levels, and EC Member Pool.

**EC Member Pool**
- Members are loaded from the organisation user directory. The employee dropdown excludes people already added with the same designation.
- Designations: EC Member, CEO, CFO, Head of Admin & GST.
- Many active EC Members are allowed. Only one active CEO, one active CFO and one active Head of Admin & GST at a time.
- The same member and designation cannot be added twice. If an inactive record exists, tell the user to reactivate it.
- Activate and deactivate with confirmation. Search and filter by designation, status and free text.
- Action tags per designation (for example, CEO approve only, or approve, reject and revert). Configurable and audited.
- Audit log with filters and export.

**EC Levels (per nature, OPEX or CAPEX)**

Fields: level name (required, unique within a nature, a free label), minimum amount and maximum amount in PKR (lower bound exclusive, upper bound inclusive, two decimals, minimum strictly less than maximum), required member count, minimum approvals (at least 1 and not above the required member count), assigned EC members (up to 10 on creation, and on edit the number selected must match the required count), CFO required and CEO required (when required, a person must be selected), and optional CEO and CFO as members.

- At least one approver must be required.
- Ranges within the same nature must not overlap.
- A level cannot be deleted while any rule uses it.
- A level used by in-flight requests can be edited. The change applies to new requests only (4.8).
- When a delegation rule is configured, the system loads the level, its members and amounts so the administrator sees the effective chain.
- Audit log with filters and export.

### 5.4 Recommending Authority

- Fields: title (required, letters, numbers and spaces only, up to 200 characters, read-only after creation), department (required), user (required, list filtered by the selected department), status.
- Duplicate combinations are rejected. Deletion is blocked while any rule references the authority.
- The authority appears in the Recommending Authority list on the delegation matrix screen.
- Default action tags: approve, reject, revert.
- Audit log with export.

### 5.5 Department Head Approval Limit

- One PKR limit that decides the single-approval Route B. It starts from 0 and can be set to any positive amount by the administrator. Example: with a limit of 50,000, a request of exactly 50,000 goes to the Department Head only. 50,000.01 goes to the matrix.
- Fields: approval amount (required, greater than zero, up to two decimals), description (optional, up to 500 characters), status, effective date.
- One active general limit at a time. If a record exists, even an inactive one, the administrator updates or reactivates it. Keeping history of past values is required.
- Proposed: a separate limit per nature (OPEX and CAPEX), because the two use separate configuration. Confirm in Part 18.
- Summary cards (current limit, status, last changed), status filter, audit log with export.

### 5.6 Currency Management

- Fields: currency code (required, stored uppercase, unique, locked after creation), currency name (required), PKR conversion rate (required, numeric, greater than zero, up to four decimals), status.
- Every currency must carry an active PKR rate. The administrator may add as many currencies as needed. PKR is seeded with rate 1 and cannot be deactivated or deleted.
- Changing a rate creates a new effective-dated rate record. The old rate is kept. Only one rate is active at a time. Requests snapshot the rate (4.1).
- Activate, deactivate and delete with confirmation. Deletion is blocked when any request, draft or rate history uses the currency (check the real request tables, not the V3 tbl_Opex and tbl_Capex).
- The currency dropdown on both request forms lists active currencies only. When the user picks one, the PKR amount appears automatically.
- Audit log records old and new rate, who changed it and when.
- Proposed Enhancement for Phase 2: scheduled import of rates from an approved source, with maker-checker.

---

## Part 6 — OPEX request (Head Office OPEX)

### 6.1 Purpose and access

Creates and resubmits Head Office OPEX requisitions. Permission: opex.request.create. The form fills in the requester, department, date and reference. If the user has no department assigned, submission is disabled and the screen says to contact HR. Proposed Enhancement: save as Draft (the V3 form had no drafts).

### 6.2 Header (system-filled and classification)

| Field | Type and source | Rules |
|---|---|---|
| Reference No | System generated | Pattern in Part 2. Unique. |
| Request On | System, current date | Read-only. |
| Requesting Department | Auto from user | Read-only. |
| Prepared By | Auto from user | Read-only. |
| Location | Auto from user | Read-only. |
| Type | Dropdown, user | Required. Lookup values Not Yet Verified. |
| Category | Dropdown, user | Required. Only categories that have an active OPEX rule. |
| Sub-Category | Dropdown, depends on category | Required. Only subcategories that have an active OPEX rule (or the category-wide rule). |
| Category name | Text, only when category is Others | Required when shown. |
| Sub-category name | Text, only when subcategory is Others | Required when shown. |

### 6.3 Charge allocation

- Branch expense rows, added through an Add Expense (Branch) dialog: branch and amount. Branch total shown.
- Head Office expense rows, added through an Add Expense (Head Office) dialog: department and amount. Head Office total shown.
- Cumulative expense charge is calculated and displayed.
- Whether the charge total must equal the approval amount in OPEX is Not Yet Verified (Part 18).

### 6.4 Vendor, currency, tax and amounts

| Field | Type and source | Rules |
|---|---|---|
| Vendor | Dropdown of department vendors | Required. |
| Add Vendor | Dialog | Fields: vendor name, contact number, CNIC, NTN, STRN, address. Format validations Not Yet Verified. Creates a department vendor. |
| Currency | Dropdown of active currencies | Required. PKR amount recalculates on change using the active rate (4.1). |
| Other Currency | Text, only when currency is Other | Whether Other currency still exists is Not Yet Verified. |
| Approval Amount | Decimal, user | Required, greater than zero, two decimals. |
| PKR Amount | Calculated | Read-only. |
| Amount in Words | Calculated | Read-only. |
| GST | Checkbox plus GST value | Value required when ticked. |
| WHT | Checkbox plus WHT value | Value required when ticked. |
| Sales Tax Authorities | Multi-select | Stored as a related list. |

### 6.5 Business flags and summary

| Field | Type | Rules |
|---|---|---|
| Three Quotations Attached | Checkbox | Makes Quotation 2 and 3 mandatory in the checklist. |
| Already Approved | Checkbox | Requires the approved-document attachment. Triggers Route A (4.3). |
| Tax Exemption | Checkbox | Requires the tax-exemption document. |
| Goods Received Notes (GRN) | Checkbox | Requires the GRN document. |
| Accrued Amount | Checkbox plus Amount | Amount shown when accrued. |
| Last Approved Amount | Decimal | Optional. |
| Invoices / Quotation Attached | Checkbox | As in V3. |
| YTD Expense Amount and MTD Expense Amount | Decimal | V3 asked the user to type these. In EDM 2.0 they are calculated from the expense register and shown read-only (Proposed Enhancement, confirm in Part 18). |
| Recurring | Yes or No | If No, an expenditure justification is required. |
| Budget Status | Budgeted or Un-budgeted | Required. |
| Term of Payment | Option list | Options Not Yet Verified. |
| Detail / Reason of Expenditure | Text area | Required when non-recurring. |
| Unit Head | Department Head | Derived from the organisation hierarchy, with a choice among eligible heads (4.2). |

### 6.6 Grids

- **Invoice / Quotation grid.** Rows with Invoice Number, Amount, Invoice Date, Nature and Reason. A total is calculated. The total must equal the approval amount and be greater than zero.
- **Previous Expense Summary grid.** Rows with Reason of Expenditure, Nature of Maintenance and Approved Amount. A total is calculated.

### 6.7 Required documents checklist

- Quotation 1 is always required.
- Quotation 2 and 3 are required when Three Quotations Attached is ticked. Otherwise, each missing optional quotation requires a comment.
- Approved document is required when Already Approved is ticked.
- Tax-exemption document is required when Tax Exemption is ticked.
- GRN document is required when GRN is ticked.
- Any further checklist items depend on the request type. The list per type is Not Yet Verified.
- File rules: allowed types, size limit and count are configurable. Each upload is scanned, hashed and stored with uploader and time.

### 6.8 Submission flow

1. The user completes the details, then selects Next to open the checklist.
2. The server validates required fields, amount consistency, justification and attachments.
3. The routing engine (Part 4) runs. If it cannot route, the user sees the 4.9 message and nothing is saved.
4. In one transaction the system saves the request, the route, the approval steps, the policy version, the decision trace and the audit event.
5. The first approver is emailed. The requester is taken to My Requests with a confirmation that shows the reference number and the approval chain.

### 6.9 Returned requests

The creator (in My Requests), or the designated revert recipient (in My Approvals), can open a reverted request for correction and resubmit it. Resubmission follows 4.7. Selective revert (choosing a previous approver) is Not Yet Verified in detail (Part 18). The V3 behaviour is described by sp_Master_Opex_Revert, sp_OPEX_ResumeWorkflow and sp_Master_Opex_ResubmitRequest (Part 17).

---

## Part 7 — CAPEX request (Head Office CAPEX)

### 7.1 Purpose and access

Creates and maintains Head Office capital-expenditure requests and sends them into the CAPEX approval route. Permission: capex.request.create.

**Eligible departments.** In V3 only Admin, Information Technology and Digital could raise new CAPEX requests (hard-coded in config as CAPEXAllowedDepartments). In EDM 2.0 this is an administrator-managed CAPEX eligibility policy, with these three departments as the seeded default. Existing returned requests can always be edited by their creator or revert recipient.

### 7.2 Header

The same header fields as OPEX (6.2). Categories and subcategories come from active CAPEX rules.

### 7.3 Asset-book allocation

- Asset Book To Branch: rows with branch and asset amount, with a branch total.
- Asset Book To Head Office: rows with department and asset amount, with a Head Office total.
- A cumulative asset total is calculated.
- The invoice total, the approval amount and the total charges must all be greater than zero and equal.

### 7.4 Vendor, currency, tax and amounts

Same as OPEX (6.4), including Add Vendor, currency conversion, GST, WHT, Sales Tax Authorities and Amount in Words. In CAPEX a Sales Tax Authority selection is required.

### 7.5 CAPEX-specific fields

| Field | Type | Rules |
|---|---|---|
| Nature of Purchase | Option list | Options Not Yet Verified. |
| Budget Status | Option list | Budgeted, Un-budgeted or Over-budget (confirm values). |
| Reason for Over Budget / Un-budgeted CAPEX | Multi-line text | Required for unbudgeted and over-budget requests. Maximum 500 characters. |
| Warranty | Yes or No | |
| Warranty Date | Date | Required when Warranty is Yes. Cannot be in the past. |
| Terms and Conditions of Payment | Multi-line text | |
| Finance Date | Date | Who fills it (requester or Finance) is Not Yet Verified. |
| Budget Comments | Option list | Options Not Yet Verified. |
| Finance Comment | Multi-line text | Who fills it is Not Yet Verified. |
| Detail / Reason of Expenditure | Text area | |
| Unit Head | Department Head | As in 4.2. |
| Flags | Checkboxes | Three Quotations Attached, Already Approved, Tax Exemption, GRN, with the same document rules as OPEX. |

### 7.6 Classification

Checkboxes, each with its own value field when ticked: Leasehold Improvement, Furniture and Fixture, Office Equipment, Computer Equipment, Computer Software. V3 stored them as flags plus values and a combined text string. EDM 2.0 stores structured rows (classification code and value). Whether the classification values must add up to the approval amount is Not Yet Verified.

### 7.7 Grids

- **Invoice / Quotation grid.** Same columns as OPEX.
- **Item-wise detail grid (previous expense summary).** For each row the user enters Actual Rate per Unit, Actual Units, Budget Rate per Unit and Budget Units. The system calculates Actual Amount, Budget Amount, Variance Rate, Variance Units and Variance Amount. Item-wise details are required before the request can proceed. V3 had no item description column. Proposed Enhancement: add one.
- **CAPEX Budget Summary.** Generated by the system from the item-wise grid and saved with the request. Not editable by the user.

### 7.8 Documents, submission and returned requests

- The checklist rules vary with the request type and the options ticked (three quotations, tax exemption, GRN, already approved).
- The flow is: complete details and allocations, pass the amount, item-detail and warranty-date checks, open the checklist, upload documents, submit. Routing, atomic save, notification and redirect follow 6.8.
- Returned requests follow 6.9, using the CAPEX revert and resubmit behaviour (Part 17).
- A failed route must never leave a half-saved request. V3 deleted the request afterwards. EDM 2.0 validates and routes before saving.

---

## Part 8 — Approval workspace and request lifecycle

### 8.1 Statuses

- **Request status:** Pending (in approval), Reverted, Approved, Rejected. Proposed Enhancement: Draft. The V3 list filters are Pending, Approve, Reject, Revert and Forward, and these stay available as filters.
- **Step state:** Waiting, Active, Approved, Rejected, Reverted, Forwarded, Closed (Quorum Met), Superseded (replaced by a fresh cycle, 4.7).
- Use one consistent colour and label per status across the product.

### 8.2 My Requests and My Approvals

OPEX and CAPEX each have a request register with two tabs: My Requests (requests the user created) and My Approvals (requests assigned to the user). Both can be reached from one Approval Inbox with an OPEX / CAPEX switch. They stay backed by separate OPEX and CAPEX data.

- **Filters:** reference number, request type, status (Pending, Approve, Reject, Revert, Forward), date range (defaults to the last 30 days through today), and Head Office / branch / department.
- **Results:** reference, vendor, request date, type, amount, status. For approvals also the current step and age.
- **Actions:** view, export the filtered list to Excel, print or PDF for approved requests, start a new request.
- **Scope:** the server scopes every list by the signed-in user. Nothing is trusted from the client.
- **Destination rules:** a reverted request opens for editing for its creator in My Requests. In My Approvals it opens for editing only for the designated revert recipient. Everyone else goes to the read-only approval view. Print and PDF appear for approved requests in My Requests when the status conditions are met. Approvers can also download once a request is approved (see 8.4).

### 8.3 Approval view (OPEX and CAPEX)

A read-only view of the request with an action panel.

- **Content:** request details, charge or asset allocations, required-document checklist, attached files (preview and download), finance details (CAPEX), the approval timeline with actor, action, remarks and time, the current stage, and the decision trace.
- **EC progress** (only when the request uses Route D): level name, current step, members progress such as 3 of 5 approvals, quorum status, and CFO and CEO status. Hidden for Routes A, B and C.
- **Action panel:** shown only when the signed-in user has a pending assignment at the current step. It offers the actions allowed by the action tags (4.4). Remarks are mandatory for Approve, Reject and Revert. Forward requires a recipient. The user confirms before submitting.
- **Forward recipients:** any active user eligible under a rule the administrator sets. The default eligibility is Not Yet Verified (Part 18).
- **Safety:** a user who already acted at a step cannot act again. An unauthorised or already-processed action is refused on the server. Simultaneous actions are handled with concurrency control.
- **After an action:** status and progress update, the history is recorded, notifications go out, and the user returns to the list.

### 8.4 Completion and documents

When a request is Approved, the requester and every approver who took part can download the request as PDF and in other formats. PDF is mandatory. Other formats (for example Excel) are Not Yet Verified. The PDF carries the reference number, request details, approval trail with timestamps, and the final status.

### 8.5 Not in V3 (Phase 2)

Delegation of approval to a proxy for a period, escalation and reminders when a step is overdue, and approval turnaround reporting. Build these only in Phase 2 and only as Proposed Enhancements.

---

## Part 9 — Reporting

- **Summary cards:** total requests and PKR amount, and counts and PKR amounts for Pending, Approved, Rejected, Forwarded and Reverted. A card can be selected to filter the list.
- **Filters:** request type (OPEX, CAPEX or both), status, date range, department, category, subcategory, currency, amount range, and a free-text search over vendor, reference number and preparer.
- **Validation:** start date not later than end date. Minimum amount not above maximum amount. Clearing filters returns to the default view of both types.
- **List:** sortable, paged, with request detail links to the approval view and an audit trail view per request.
- **Analytics:** spending by category and subcategory. The Executive dashboard (Part 14) adds total expenditure, OPEX versus CAPEX, pending approvals, department spending, monthly trend, and approval turnaround time.
- **Export:** to Excel and CSV for the filtered result. The maximum is configurable (10,000 rows in V3). Export is disabled when there is no data. Every export is audited with its filters.
- **Flags:** requests that used Route A (Already Approved) are flagged and filterable.
- **Access:** governed by roles and data scope (Part 11). Requesters see their own, Department Heads see their department, Auditors and Finance Viewers see all. V3 gave every signed-in user full access, and that must not happen.

---

## Part 10 — Audit and compliance

A single append-only audit store serves the whole product. Each module audit tab, the request timeline and the global audit viewer read from it.

### 10.1 Audit event contents (required on every event)

Event id, tenant, timestamp (UTC), actor user id, actor name, actor department, actor roles, source IP address, user agent, correlation id, module, entity type and id, action, outcome (success or failure) and failure reason, remarks entered by the user, request reference when applicable, policy version, and the before and after values as structured data with a field-level difference.

### 10.2 Actions that must be audited

Create, update, activate, deactivate and delete in every module; configuration publish and maker-checker decisions; request submit, approve, reject, revert, forward, resubmit and fresh-cycle restart; routing decision and routing failure; validation failures on configuration screens; exports and downloads; sign-in, sign-out, failed sign-in, lockout and MFA events; role and permission changes; access denied; and every view of the audit store itself.

### 10.3 Integrity

- No update or delete path exists in the application. The application database account has insert and select rights only on the audit tables.
- Proposed Enhancement: each event stores a hash of the previous event so tampering is detectable, plus a verification job.
- Retention is configurable (Data Retention setting). Archival must keep events readable and exportable.

### 10.4 Views

- **Request timeline:** complete activity history with actor and timestamp, previous and new values, approval decisions and the policy version used.
- **Global viewer:** filter by date range, user, department, module, action, record and outcome. Export to Excel and CSV.
- **Per-module audit tab:** the same data filtered to that module.
- **Menu groups** as in the navigation: Activity Logs, Approval History, Policy Changes, Security Events, Audit Exports.

---

## Part 11 — Identity, access and organisation

### 11.1 Organisation data

Departments, Branches, Business Units, Positions, the organisational hierarchy, and Users (employee id, name, email, department, branch, position, line manager, status). V3 read these from an external ApplicationPanel database. EDM 2.0 owns these tables and fills them through an HR sync (Phase 1: directory sync and CSV or API import, Phase 2: HRMS connector). The Department Head of each department is mapped in the hierarchy with an effective date. A user without a department cannot submit requests.

### 11.2 Roles

| Role | Rights |
|---|---|
| Requester | Create and view own OPEX and CAPEX requests. Edit returned ones. |
| Approver (assignment-based) | Not a stored role. Rights come from being assigned at a step, including Department Head through the hierarchy. |
| EDM Administrator | Maintains categories, rules, EC levels and pool, recommending authorities, limits and currencies (the maker). |
| Configuration Approver | Approves configuration changes (the checker). Cannot approve a change they made. |
| Auditor | Read-only access to requests, audit and reports, with export. |
| Finance Viewer | Reports and request registers. |
| System Administrator | Users, roles, settings, SSO and integrations. No approval powers and no ability to alter audit data. |

Permissions are fine-grained (for example config.matrix.edit, report.export) and assigned to roles. All checks are server-side.

### 11.3 Separation of duties

- Maker and checker must be different people.
- A requester can never approve their own request. If routing resolves an approver who is the requester (for example a Department Head submitting their own request), the step goes to the next head up the hierarchy. This rule is Not Yet Verified (Part 18).
- A person cannot hold two steps on the same request unless the rule allows it, and the duplication is recorded.

### 11.4 Security requirements

OWASP ASVS level 2, TLS everywhere, CSRF and XSS protection, rate limiting, strict input validation, hardened file uploads, secrets outside source control, encryption at rest for attachments and sensitive personal data such as CNIC (masked in the UI), no sensitive data in logs, dependency scanning, and a penetration test before any bank pilot. Access Policies and MFA enforcement are Phase 2 options in the Identity & Access area.

---

## Part 12 — Notifications and documents

| Event | Recipients |
|---|---|
| Request submitted | First approver (Department Head) |
| Forwarded | The new recipient |
| Step approved | The next approver |
| Recommending Authority approved | All members of the EC level in one bulk send |
| Quorum met | Remaining EC members (closing notice) |
| Reverted | The revert recipient |
| Rejected | Requester and previous approvers |
| Final approval | Requester and approvers, with a download link |

- Channels: email and in-app notifications in Phase 1. SMS and chat integrations later.
- Templates are editable by the administrator with placeholders and branding. Emails carry a secure deep link and only minimal request details (configurable), because inboxes are not a secure channel for financial data.
- Delivery uses a transactional outbox with retry and back-off. Administrators can see delivery status and failures.
- Documents: server-side PDF generation with company branding for the approved request and its approval trail, plus print view.

---

## Part 13 — Data model (conceptual)

The agent finalises names, types and indexes in a design note and gets it approved before writing migrations. OPEX and CAPEX keep separate request, routing, approval and configuration tables. Shared lookups are shared. Every table has id (uuid), tenant_id, created_at, created_by, updated_at, updated_by and row_version. Configuration tables also have is_active.

| Group | Tables |
|---|---|
| Identity and organisation | users, departments, branches, business_units, positions, department_heads (department, user, effective from and to), roles, permissions, role_permissions, user_roles |
| Shared configuration | categories, subcategories, currencies, currency_rates (currency, rate numeric(18,4), effective from and to, is_active), recommending_authorities, ec_members (user, designation, active), action_tag_sets, request_types, checklist_definitions (per nature and request type), sales_tax_authorities, vendors (department vendors), settings |
| OPEX configuration | opex_dept_head_limits, opex_ec_levels, opex_ec_level_members, opex_delegation_rules, opex_rule_ec_levels, opex_rule_recommending_authorities |
| CAPEX configuration | capex_dept_head_limits, capex_ec_levels, capex_ec_level_members, capex_delegation_rules, capex_rule_ec_levels, capex_rule_recommending_authorities, capex_eligible_departments |
| Policy | policy_versions (version number, published at and by, snapshot as JSON), config_change_requests (maker, checker, status, before, after) |
| OPEX requests | opex_requests, opex_request_charges (branch and head-office rows), opex_invoices, opex_previous_expenses, opex_attachments, opex_request_sta |
| OPEX routing and approval | opex_routes (request, cycle number, policy version, route type, rule, level, PKR amount, currency, rate snapshot, decision trace as JSON), opex_approval_steps (route, sequence, step type, assignee, state, allowed actions), opex_approval_actions (step, actor, action, remarks, time), opex_ec_votes (step, member, vote, time) |
| CAPEX requests | capex_requests, capex_request_charges (asset allocations), capex_invoices, capex_item_details, capex_budget_summary, capex_classifications (code, value), capex_attachments, capex_request_sta |
| CAPEX routing and approval | capex_routes, capex_approval_steps, capex_approval_actions, capex_ec_votes (same shape as OPEX) |
| Platform | audit_events, files, notifications, email_outbox, background_jobs |

**Design rules**
- A request has one or more routes by cycle number. A fresh cycle marks the earlier route and its steps Superseded and keeps them.
- Use PostgreSQL numeric range types with an exclusion constraint so active EC levels of the same nature and tenant cannot overlap. Ranges are lower bound exclusive and upper bound inclusive.
- CHECK constraints: minimum below maximum, minimum approvals not above required members, amounts greater than zero, one active CEO, CFO and Head of Admin & GST per tenant (partial unique indexes).
- Money is numeric(18,2). Rates are numeric(18,4). Never use floating point.
- Business-rule logic stays in the application layer. The database enforces integrity only.
- Audit tables are append-only (Part 10).

---

## Part 14 — UI/UX standards and screens

### 14.1 Navigation

Phase 1 builds the items marked P1. Items marked P2 are Phase 2 and must not be built early.

| Area | Items |
|---|---|
| Workspace | Overview (P1), My Tasks (P1), My Requests (P1), Notifications (P1) |
| Expense Management | Create Expense (P1), OPEX (P1), CAPEX (P1), Drafts (P2), Submitted Requests (P1), Expense Register (P1) |
| Workflow and Approvals | Approval Inbox (P1), Workflow Designer (P2), Approval Policies (P1, the delegation matrix and EC levels), Delegation (P2), Escalations (P2) |
| Organisation Management | Departments (P1), Branches (P1), Business Units (P2), Positions (P2), Organisational Hierarchy (P1) |
| Configuration and Policies | Categories (P1), Subcategories (P1), Financial Limits (P1, department-head limit and currency), Approval Authorities (P1, recommending authorities and EC pool), OPEX/CAPEX Policies (P1), Rule Builder (P2) |
| Identity and Access | Users (P1), Roles (P1), Permissions (P1), Access Policies (P2), SSO (P1), MFA (P1 basic, P2 enforcement) |
| Reports and Analytics | Expense Reports (P1), Approval Performance (P2), Department Spending (P1), Audit Reports (P1), Exports (P1) |
| Audit and Compliance | Activity Logs, Approval History, Policy Changes, Security Events, Audit Exports (all P1) |
| Integrations | ERP, HRMS, Financial Systems, External APIs, Webhooks, Identity Providers (P2 except identity providers, P1) |
| System Administration | Tenant Settings (P2), General Settings (P1), Notifications (P1), Localisation (P1), Data Retention (P1), System Health (P1) |

### 14.2 Design references and standards

References: Linear for clean navigation and interaction, Stripe for financial dashboard patterns, Atlassian for information-dense business workflows, Microsoft Fluent 2 for a consistent enterprise design language.

| Element | Requirement |
|---|---|
| Sidebar | Collapsible, grouped navigation |
| Header | Search, notifications, profile, tenant context |
| Dashboard | Role-specific content |
| Data tables | Search, filtering, sorting, pagination, column controls, saved views, export |
| Forms | Clear sections, inline validation, helpful descriptions, unsaved-changes warning |
| Approval screens | Timeline, current stage, history and actions |
| Workflow Designer (P2) | Visual drag-and-drop configuration that edits the delegation matrix |
| Status indicators | Consistent colours and labels across the product |
| Dialogs | Confirmation for sensitive actions (deactivate, delete, approve, reject, revert) |
| Responsive | Desktop-first, tablet and mobile support |
| Accessibility | Keyboard navigation, focus management, WCAG 2.2 AA-aligned components, never colour alone to convey status |
| States | Every screen has loading, empty, error and no-permission states |
| Localisation | Text externalised. Layout ready for right-to-left languages such as Urdu. English first. |

### 14.3 Screens to design first

1. Executive dashboard: total expenditure, OPEX versus CAPEX, pending approvals, department spending, monthly expense trends, approval turnaround time.
2. Expense request creation: classification, category and subcategory, department and cost centre, amount and currency, supporting documents, submission validation.
3. Approval workspace: my pending approvals, request details, financial limits, approval history, and approve, reject and revert actions where authorised.
4. Visual workflow designer (P2): add approval stages, configure conditions, define authority, set amount thresholds, configure rejection and return paths, validate and publish versions.
5. Delegation matrix configuration: authorities, financial thresholds, departments, approval levels, effective dates, and the coverage view (5.2).
6. Audit and request timeline: complete activity history, actor and timestamp, previous and new values, approval decisions, policy version used.

Proposed Enhancement: a read-only routing preview on the request form that shows the expected approval chain once category, subcategory and amount are entered.

---

## Part 15 — Build phases and delivery order

- **Phase 0, foundations:** repository and CI, architecture decision records, solution skeleton, authentication and SSO skeleton, organisation and user data, RBAC, the audit store, the design system, seed data.
- **Phase 1A, configuration:** currency and rates, categories and subcategories, EC member pool and levels, recommending authorities, department-head limit, delegation matrix with coverage view, policy versioning, maker-checker. Each with audit and tests.
- **Phase 1B, routing engine:** built and fully tested as a standalone library against Part 4.12 before any form uses it.
- **Phase 1C, OPEX:** request form, My Requests and My Approvals, approval view and actions, notifications, PDF.
- **Phase 1D, CAPEX:** the same for CAPEX, including eligibility policy and item-wise grids.
- **Phase 1E, hardening:** reporting, global audit viewer, access scoping, V3 data migration, security review, performance tests, penetration-test readiness, deployment guides.
- **Phase 2:** Workflow Designer (edits the matrix), Rule Builder, Delegation and proxy approval, Escalations and reminders, Integrations (ERP, HRMS, webhooks), Access Policies and MFA enforcement, tenant administration, Drafts, scheduled currency rates, and the unbudgeted limit once its behaviour is specified.

Your first task as the agent: deliver the Phase 0 plan: architecture decision records, the solution layout, the component-library recommendation, the data model design note for Phase 1A, and one batch of questions drawn from Part 18. Then proceed.

---

## Part 16 — Testing and acceptance

- **Routing engine:** unit tests for every scenario in 4.12, plus property-based tests for boundaries, rounding and range coverage.
- **Integration tests** against real PostgreSQL for every command and query.
- **Concurrency tests:** simultaneous EC approvals, double-click submissions, two administrators editing the same record.
- **Authorisation tests:** every endpoint with every role, including denied cases and data-scope checks.
- **Audit tests:** every mutating endpoint produces exactly the expected audit event with correct before and after values.
- **End-to-end tests (Playwright):** submit OPEX and CAPEX through Routes A, B, C and D, including forward, revert, correction and resubmission, reject, quorum and the no-route error.
- **Performance targets (to confirm):** routing decision under 100 ms, list screens under 500 ms at the 95th percentile with one million requests.
- **Accessibility:** automated checks and keyboard walkthrough on every screen.
- **Security:** dependency scan, static analysis, and the ASVS checklist.
- **Migration:** rehearsal runs on a copy of V3 data with reconciliation reports.
- **Acceptance checklist per module:** functional rules, validations, permissions, audit events, notifications, accessibility, documentation, and demo to the product owner.

---

## Part 17 — V3 migration and legacy map

Do not port V3 stored procedures. Re-implement their business rules as described in this prompt. Use V3 only to confirm intent.

| V3 module | EDM 2.0 module | Key V3 procedures and tables |
|---|---|---|
| HeadOfficeOpex | OPEX request | SP_INSERT_OPEX_HO, SP_VIEW_APPROVAL_OPEX_HO, sp_Master_Opex_Revert, sp_OPEX_ResumeWorkflow, sp_Master_Opex_ResubmitRequest, sp_DeleteOpexRequest (removed, saves are atomic), usp_heads (becomes department head resolution), SP_INSERT_DEPARTWISEVENDOR; tables tbl_OpexExpenseRequest, tbl_OpexInvoice, tbl_OpexExpenseSummary, tbl_OpexAttachments, tbl_OpexCharges_HO, tbl_OpexCharges_Branch, tbl_OpexExpenseRequestSTA |
| HeadOfficeCapex | CAPEX request | SP_INSERT_CAPEX_HO, SP_VIEW_APPROVAL_CAPEX_HO, sp_Master_Capex_Revert, sp_CAPEX_ResumeWorkflow, sp_Master_Capex_ResubmitRequest, sp_DeleteCapexRequest; tables tbl_CapexExpenseRequest, tbl_CapexInvoice, tbl_CapexExpenseSummary, tbl_CapexBudgetSummary, tbl_CapexAttachments, tbl_CapexCharges_HO, tbl_CapexCharges_Branch, tbl_CapexExpenseRequestSTA |
| MyApprovals, MyApprovalsCapex | Approval Inbox and registers | SP_SELECT_OPEX_APPROVALS, SP_SELECT_CAPEX_APPROVALS, usp_get_user_wise_approval |
| Approval, ApprovalCapex | Approval view and actions | sp_OPEX_RecordApprovalAction, sp_CAPEX_RecordApprovalAction, SP_UPDATE_WORKFLOW_OPEX_HO, SP_UPDATE_WORKFLOW_CAPEX_HO; tables tbl_OpexExpenseWorkflow, tbl_Opex_ExpenseApprovalWorkflow, tbl_Opex_ECApprovalLog and the CAPEX equivalents |
| CategorySubcategory | Categories, Subcategories, Delegation Matrix | tbl_Category, tbl_Subcategory, tbl_DelegationMatrix, tbl_DelegationMatrix_ECLevels, tbl_EDM_Step_h, tbl_EDM_Step_Approver_h, and the log tables |
| ExpenseCommittee | EC pool and levels | tbl_ExpenseCommittee, tbl_ECMemberPool, tbl_ApprovalLevel, tbl_ApprovalLevel_Members, tbl_ExpenseCommittee_Log |
| CurrencyManagement | Currency | tbl_Currency, tbl_CurrencyLog |
| DepartmentHeadApprovalLimit | Department-head limit | tbl_DeptHeadApprovalLimit and its log |
| RecommendingAuthority | Recommending authority | tbl_recommending_authority and its log |
| ReportingModule | Reporting | sp_Reporting_GetSummaryMetrics, GetRequests, GetRequestDetail, GetAuditTrail, GetCategoryAnalytics |
| Opex_Capex_Unbudgeted | Deferred | tbl_ExceedingApprovalLimit and its log. Migrate the data, build no behaviour. |

**Legacy tables to drop after confirmation:** tbl_requests, tbl_requests_history, tbl_status, tbl_approval_steps, tbl_checklist, tbl_ho_opex_configs (used only by the legacy usp_get_type), and the duplicate tbl_ECApprovalLog if it holds nothing the OPEX and CAPEX logs lack. The purpose of tbl_MonthlyExpenseHistory (used for average calculations) is Not Yet Verified.

**External data:** ApplicationPanel and AppPanelDB tables (tbl_Users, tbl_Departments, tbl_Branch) map to the EDM 2.0 organisation tables. The unqualified tbl_Users used in helper SQL must be resolved before migration.

**Migration method:** read-only extraction, staging tables, mapping rules, reconciliation reports (counts and amounts per status), at least two dry runs, attachment migration with hash verification, V3 reference numbers preserved, and V3 audit and log history imported into audit_events with a source marker.

---

## Part 18 — Open questions (confirm with the product owner, do not guess)

Each item shows the default to use until it is answered.

1. **Level naming.** Your notes describe Level 1 both as the highest amount and as 10K to 20K. Default: level numbers carry no meaning and the amount range decides.
2. **Rule lookup.** Subcategory-specific rule first, then category-wide. Default as stated in 4.2.
3. **Standalone and recommending authority.** V3 required an RA on every rule. Default: no RA on standalone rules.
4. **Combined routes.** V3 text says EC levels and/or CEO, CFO, Head of Admin. Default: a rule is either EC or standalone.
5. **Several recommending authorities on one rule.** Default: any one may act.
6. **Department-head limit per nature.** Default: separate OPEX and CAPEX limits.
7. **No limit configured.** Default: seeded 20,000 PKR system setting, as in V3.
8. **Revert recipient and selective revert.** Exact V3 semantics of choosing a previous approver. Default: 4.5.
9. **EC votes on resume after a revert.** Default: cleared and logged.
10. **Standalone revert target.** Default: last Phase-1 approver.
11. **Forward recipient eligibility.** Default: any active user in an eligible set the administrator defines.
12. **Self-approval.** Default: next head up the hierarchy.
13. **Others category.** How V3 routes it. Default: needs a rule like any other.
14. **Other currency field.** Default: removed unless a currency named Other is configured.
15. **OPEX charge total.** Must it equal the approval amount? Default: no check until confirmed.
16. **Missing lookup values.** Request types, Term of Payment, Nature of Purchase, Budget Comments, checklist items per type, vendor field formats, reference number format.
17. **CAPEX finance fields.** Who fills Finance Date, Finance Comment and Budget Comments.
18. **Classification values.** Must they sum to the approval amount?
19. **YTD and MTD.** Source for automatic calculation, and the purpose of the monthly expense history table.
20. **Download formats.** Beyond PDF.
21. **Rejected requests.** Can they be copied into a new request? Default: closed, copy allowed as a new request.
22. **Already Approved as a hot field.** Default: yes.
23. **PKR rounding.** Default: two decimals, half away from zero.
24. **Retention periods** for requests, attachments and audit data.
25. **First deployment.** Target bank, hosting model and SSO provider.
26. **Ownership of the product.** The product owner confirms intellectual-property ownership and licensing of EDM in writing before commercial rollout. This is not an agent task.

---

## Part 19 — Non-functional requirements

Targets below are starting values. The product owner confirms them with the first bank. Until then the agent designs to meet them.

| Area | Requirement |
|---|---|
| Availability | 99.9% per month during business hours, excluding planned maintenance. Health checks and graceful shutdown. |
| Recovery | Recovery point objective 15 minutes or better using continuous archiving. Recovery time objective 4 hours. Daily full backup, tested restore every quarter, documented disaster-recovery runbook. |
| Scale assumption | 10,000 users, 300 concurrent, 200,000 requests a year, 5 million audit events a year, 10 years of history online or archived. |
| Performance | Read APIs 95th percentile under 300 ms. Write APIs under 800 ms. Routing decision under 100 ms. PDF generation under 5 seconds. Export of 10,000 rows under 30 seconds. List screens under 500 ms with one million requests. |
| Security | OWASP ASVS level 2, TLS 1.2 or higher, encryption at rest for the database, backups and attachments, secrets in a vault or protected configuration, penetration test before any bank pilot. |
| Compliance | SBP technology governance, outsourcing and internal-control expectations are Not Yet Verified. The product owner or a compliance adviser confirms the applicable regulations. The agent records every control it implements against a control list. |
| Retention | Configurable per data type. Proposed default for requests and audit data: 10 years, to be confirmed with the bank's policy. |
| Browsers | Current and previous major versions of Chrome, Edge, Firefox and Safari. |
| Accessibility | WCAG 2.2 AA. |
| Localisation | English first, ready for Urdu and right-to-left layout. Date format dd-MMM-yyyy. Time zone Asia/Karachi. Amounts show with a user-selectable digit grouping: international (1,000,000) or lakh and crore (10,00,000). |
| Maintainability | At least 80% coverage on domain and application code, 100% branch coverage on the routing engine, an architecture decision record for every significant choice, and a README and runbook per module. |
| Operations | Zero-downtime deployment desirable. Configuration through environment variables. Structured logs with correlation ids. Dashboards and alerts for errors, slow requests, failed emails and failed background jobs. |

---

## Part 20 — Permission catalogue

Permissions are strings checked on the server. Approval actions are checked against the assignment, not a role. Data scope is applied on top (own, department, all).

### 20.1 Permissions

- **Requests:** opex.request.create, opex.request.view.own, opex.request.view.department, opex.request.view.all, opex.request.edit.returned, and the same set with capex in place of opex.
- **Approvals:** approval.inbox.view, approval.act (also requires an active assignment), approval.view.assigned.
- **Configuration:** config.category.view, config.category.manage, config.subcategory.manage, config.matrix.view, config.matrix.manage, config.matrix.approve, config.ec.pool.manage, config.ec.level.manage, config.ra.manage, config.limit.manage, config.currency.manage, config.capex.eligibility.manage, config.actiontags.manage, config.policy.view.
- **Organisation:** org.department.manage, org.branch.manage, org.hierarchy.manage, org.user.view, org.user.manage, org.sync.run.
- **Identity and access:** iam.role.manage, iam.permission.assign, iam.sso.manage, iam.mfa.manage.
- **Reports:** report.view.own, report.view.department, report.view.all, report.export.
- **Audit:** audit.view, audit.export, audit.security.view.
- **System:** settings.manage, notification.template.manage, retention.manage, health.view, integration.manage.

### 20.2 Roles and their permissions

| Role | Permissions |
|---|---|
| Requester | opex and capex request create, view.own, edit.returned (CAPEX create also needs eligibility), approval.inbox.view, report.view.own |
| Department Head (additional, from the hierarchy) | request view.department, report.view.department, plus approval.act when assigned |
| EDM Administrator | all config.* except config.matrix.approve, org.department.manage, org.branch.manage, org.hierarchy.manage, org.user.view |
| Configuration Approver | config.matrix.view, config.matrix.approve, config.policy.view. Cannot approve own change. |
| Auditor | request view.all, report.view.all, report.export, audit.view, audit.export, audit.security.view, config.policy.view |
| Finance Viewer | request view.all, report.view.all, report.export |
| System Administrator | org.user.manage, org.sync.run, iam.*, settings.manage, notification.template.manage, retention.manage, health.view, integration.manage. No approval, no config approval, no audit alteration. |

The agent delivers the catalogue as seed data, a permission matrix document, and tests that every endpoint declares its required permission.

---

## Part 21 — API and error conventions

### 21.1 Conventions

- Base path /api/v1. Resource names plural, lowercase, hyphenated.
- JSON in and out. Money values are sent as decimal strings (for example 125000.50) to avoid floating-point loss. Dates and times are ISO 8601 in UTC.
- Lists accept page, pageSize, sort and filter parameters and return items, total count and page metadata.
- Concurrency: responses carry an ETag derived from row_version. Updates send If-Match. A stale update returns 412 with code EDM-CONC-001.
- Request submission and other non-idempotent commands accept an Idempotency-Key header.
- Errors use RFC 9457 ProblemDetails with an added code, a user-safe message, a correlation id and, for validation, a list of field errors (HTTP 422).
- Every endpoint documents its required permission in OpenAPI.

### 21.2 Endpoint groups (to be finalised in the design note)

| Group | Examples |
|---|---|
| Currency | GET /currencies, POST /currencies, PUT /currencies/{id}, POST /currencies/{id}/rates, POST /currencies/{id}/activate, POST /currencies/{id}/deactivate |
| Categories | /categories and /subcategories with the same verbs, plus /audit |
| EC | /ec-members, /ec-levels (with nature) |
| Recommending authorities | /recommending-authorities |
| Limits | /dept-head-limits |
| Delegation matrix | /delegation-rules, POST /delegation-rules/preview-coverage, POST /delegation-rules/{id}/publish |
| Policy | /policy-versions, /config-changes (maker-checker) |
| OPEX | /opex-requests, POST /opex-requests/validate, POST /opex-requests/{id}/resubmit, GET /opex-requests/{id}/timeline, GET /opex-requests/{id}/pdf |
| CAPEX | The same under /capex-requests |
| Approvals | GET /approvals/inbox, POST /approvals/{stepId}/approve, /reject, /revert, /forward |
| Routing | POST /routing/preview (Proposed Enhancement) |
| Reports | /reports/summary, /reports/requests, /reports/categories, POST /reports/export |
| Audit | /audit-events, POST /audit-events/export |
| Organisation and identity | /users, /departments, /branches, /roles, /permissions |
| Platform | /notifications, /files, /health |

### 21.3 Error code catalogue (starter set)

| Code | Meaning | Message shown |
|---|---|---|
| EDM-VAL-001 | Field validation failed | Field-specific messages |
| EDM-ROUTE-001 | No active rule for nature, category and subcategory | No approval level is configured for this category. Please contact Admin or HR. |
| EDM-ROUTE-002 | No EC level matches the PKR amount | No approval level is configured for this amount. Please contact Admin or HR. |
| EDM-ROUTE-003 | Department head cannot be resolved | Your department head could not be found. Please contact HR. |
| EDM-CUR-001 | Currency has no active rate | This currency has no active exchange rate. |
| EDM-CFG-001 | Duplicate active rule | An active rule already exists for this combination. |
| EDM-CFG-002 | EC level range overlaps | This amount range overlaps another level. |
| EDM-CFG-003 | Record in use | This record is used by other records and cannot be deleted. |
| EDM-CFG-004 | Single-holder designation already active | Only one active holder is allowed for this designation. |
| EDM-WF-001 | User is not the current assignee | You do not have a pending approval on this request. |
| EDM-WF-002 | User already acted at this step | You have already acted on this request. |
| EDM-WF-003 | Action not permitted for this approver | This action is not available to you. |
| EDM-WF-004 | Remarks required | Please enter remarks. |
| EDM-WF-005 | Forward recipient required | Please select a recipient. |
| EDM-WF-006 | Quorum already met | The committee quorum has already been met. |
| EDM-AUTH-001 | Not authenticated | Please sign in. |
| EDM-AUTH-002 | Not permitted | You do not have access to this. |
| EDM-CONC-001 | Stale update | This record was changed by someone else. Please reload. |
| EDM-FILE-001 | File type, size or scan failed | The file could not be accepted. |

---

## Part 22 — State machines and route diagrams

### 22.1 Routes

```
Route A (Already Approved):  Requester -> Department Head -> APPROVED
Route B (within DH limit):   Requester -> Department Head -> APPROVED
Route C (standalone):        Requester -> Department Head -> Standalone approver (CEO | CFO | Head of Admin & GST) -> APPROVED
Route D (EC level):          Requester -> Department Head (may forward, chain A->B->C)
                             -> Recommending Authority
                             -> EC members (parallel, until minimum approvals)
                             -> CFO (if required) -> CEO (if required) -> APPROVED
```

### 22.2 Request status transitions

| From | Event | Guard | To |
|---|---|---|---|
| (none) | Submit | Validation passed and a route found | PENDING |
| PENDING | Final step approved | All steps of the route approved | APPROVED |
| PENDING | Reject | Actor permitted to reject | REJECTED |
| PENDING | Revert | Actor permitted to revert | REVERTED |
| REVERTED | Resubmit | Hot fields unchanged | PENDING, resuming at the reverting stage |
| REVERTED | Resubmit | Any hot field changed | PENDING, fresh cycle from the Department Head |
| APPROVED, REJECTED | Any action | None | Not allowed |

### 22.3 Step states

WAITING becomes ACTIVE when the previous step completes. ACTIVE becomes APPROVED, REJECTED, REVERTED or FORWARDED. EC member steps become CLOSED (Quorum Met) when quorum is reached by others. When a fresh cycle starts, every step of the old route becomes SUPERSEDED. A step never moves out of a final state.

### 22.4 EC stage

On entry the system creates one ACTIVE task for each of the N members and sends one bulk email. Each approval increments the count. When the count reaches the minimum approvals, remaining tasks become CLOSED, the stage completes, and the engine activates CFO, then CEO, or finishes. Any rejection ends the request. Any revert returns the request to the last Phase-1 approver and resets the stage.

---

## Part 23 — Acceptance checklists per module

A module is done only when every item passes, is demonstrated to the product owner, and is covered by automated tests.

**Every module:** rules in this prompt implemented exactly; server-side validation with the messages in Part 21; permission checks; audit events with before and after values; concurrency handling; loading, empty, error and no-permission states; keyboard access; documentation updated.

- **Currency:** unique uppercase code locked after creation; rate up to four decimals; new rate keeps history; PKR cannot be deactivated; delete blocked when used; dropdowns show active currencies only; audit shows old and new rate.
- **Categories and subcategories:** uniqueness rules; delete blocked while children or rules exist; deactivation warning lists affected rules; Others handling as specified.
- **EC pool and levels:** one active CEO, CFO and Head of Admin & GST; duplicate member and designation blocked with a reactivate hint; range overlap rejected at database and application level; minimum approvals not above required members; edit rule on member count; delete blocked when used.
- **Recommending authorities:** title rules; users filtered by department; delete blocked when used by a rule.
- **Department-head limit:** single active limit per nature; history kept; boundary behaviour verified at the limit and one paisa above.
- **Delegation matrix:** duplicate rule blocked; coverage view shows gaps and overlaps; maker-checker works; new policy version on publish; in-flight requests unaffected.
- **Routing engine:** all 14 scenarios in 4.12 pass; decision trace stored; no database dependency inside the pure decision logic.
- **OPEX and CAPEX forms:** every field, validation and document rule in Parts 6 and 7; atomic save; routing failure saves nothing; amounts, conversion and amount-in-words correct; returned-request flow and resubmission rules.
- **Approval workspace:** actions limited by action tags; remarks and recipient rules; quorum race-safe; revert targets correct; read-only for non-assignees; timeline accurate.
- **Notifications:** every event in Part 12 sends once, retries on failure, and shows delivery status.
- **Reporting:** filters, validation, scoped access, 10,000-row export limit, export audited.
- **Audit:** every mutating endpoint produces an event; no update or delete path; viewer filters and export.
- **Identity and access:** every endpoint declares a permission; role tests for allowed and denied cases; separation-of-duty rules enforced.

---

## Part 24 — Phase command prompts (copy and paste)

### 24.0 How to use these

1. Give the agent this whole document once and keep it in the repository (for example `docs/EDM2-Master-Prompt.md`).
2. At the start of every phase, paste the Standing Preamble (24.1), then the prompt for that phase.
3. The agent first returns a design note. Review it and reply *approved*, or reply with corrections. Only then does it build.
4. Do not start the next phase until the Done list of the current one is met and you have read the Phase Report.
5. If a rule changes, use the Change Request prompt (24.9) so this document stays the single source of truth.

### 24.1 Standing Preamble (paste at the start of every phase)

> You are the lead engineer building EDM 2.0 (Expense Delegation Matrix), an enterprise approval-governance product for banks. The document titled *EDM 2.0 Master Build Prompt* is your authoritative specification. Follow Part 0 (rules of engagement) at all times.
>
> Rules for every phase:
> - Work only on the phase named below. Do not start later phases or any Phase 2 item.
> - Before writing code, produce a design note for the phase: scope, data model changes, API list, screens, risks and questions. Then stop and wait for my approval.
> - After approval, build module by module in the order given. For each module deliver: database migration, domain and application code, API, UI, automated tests, and the acceptance checklist results from Part 23.
> - Never invent functionality. Anything unspecified goes to Part 18 as a question with your proposed default, and is marked Not Yet Verified in the code and docs.
> - Business rules live in the application layer, never in stored procedures. Every state change writes an audit event. Every endpoint checks permissions on the server. Money is decimal, never floating point.
> - Keep the repository always building and the tests green. Make small commits with clear messages. Record every significant decision as an architecture decision record.
> - When the phase is finished, give a Phase Report: what was built, tests run and results, deviations from the master prompt, open questions, risks, and the recommended next step.

### 24.2 Phase 0 — Foundations

```
PHASE 0 — FOUNDATIONS
Read first: Parts 0 to 3, 10, 11, 13, 19 to 21.

Goal: a secure, observable, tested skeleton on which every later module is built.

In scope:
1. Repository and solution layout for a modular monolith on ASP.NET Core .NET 10 with the modules named in Part 2 (empty shells where not yet needed). React and TypeScript app shell with the navigation from 14.1 (Phase 1 items as placeholders, Phase 2 items hidden).
2. Component library decision (shadcn/ui or Fluent UI React v9) written as an ADR, and the base design system: tokens, layout, tables, forms, dialogs, status badges, empty and error states.
3. Authentication: local accounts plus an SSO skeleton (OIDC and SAML), MFA hooks, lockout, session timeout.
4. Organisation and identity: users, departments, branches, hierarchy with effective-dated Department Head mapping, roles, permissions, user roles. Seed the permission catalogue and roles from Part 20. Import by CSV or API.
5. The audit store from Part 10 with a single write service used by everything, plus a basic viewer.
6. API conventions from Part 21: ProblemDetails, the error code catalogue, paging, ETag and If-Match, Idempotency-Key, OpenAPI with required permission per endpoint.
7. Logging, correlation ids, health checks, CI pipeline (build, test, lint, dependency and licence scan), Docker Compose for local PostgreSQL and the app, seed data, README.

Out of scope: any business module.

Deliverables: design note first, then ADRs, the running skeleton, tests for permissions and audit, and documentation.

Done when: a user can sign in; the menu shows only what their role permits; creating or changing a user or department writes a correct audit event visible in the viewer; a test proves every endpoint declares a permission; CI is green; one command starts everything locally.

Ask me about: hosting model, the first SSO provider, and anything in Part 18 that blocks you.
```

### 24.3 Phase 1A — Configuration modules

```
PHASE 1A — CONFIGURATION
Read first: Parts 4.1, 4.2, 4.8, 5, 13, 21, 22, 23.

Goal: an administrator can configure everything that drives routing.

Build in this order: Currency and rates; Categories and Subcategories; Expense Committee member pool and EC levels (per nature); Recommending Authorities; Department-Head limit; Delegation Matrix with the coverage view; policy versioning and maker-checker.

Key requirements:
- Exactly the rules, fields and validations in Part 5, including action tags (4.4), single-holder designations, effective dates, and delete-blocked-when-used with an explanation.
- Active EC level ranges per nature must not overlap, enforced by a PostgreSQL range exclusion constraint and by the application. Boundaries: lower bound exclusive, upper bound inclusive.
- Every change writes an audit event with before and after values and creates a new policy version. Maker-checker is on for the matrix, EC levels, limits and currency rates, and the maker can never be the checker.
- The coverage view shows amount bands, the resulting approval chain for each band, and highlights gaps and overlaps. It must also let me test any amount.
- Seed PKR with rate 1.

Out of scope: the routing engine and the request forms.

Done when: the checklists in Part 23 for these modules pass, and I can configure a demonstration set: three EC levels with members, two recommending authorities, a department-head limit of 50,000, three currencies, five categories with rules including one standalone CEO rule, and see correct coverage, a policy version bump and a working maker-checker.

Ask me about: Part 18 items 1 to 7 before building the matrix.
```

### 24.4 Phase 1B — Routing and approval engine

```
PHASE 1B — ROUTING AND APPROVAL ENGINE
Read first: Part 4 in full, Part 22, Part 13 (routing and approval tables), Part 23.

Goal: the heart of EDM as a pure, exhaustively tested library.

In scope:
1. A routing service: given a request snapshot (nature, category, subcategory, amount, currency, already-approved flag, requester, department) and a policy version, return the route (A, B, C or D), the ordered approval steps, the rate snapshot, and a human-readable decision trace. No database or HTTP inside the decision logic.
2. An approval state machine implementing Part 22 and 4.4 to 4.7: approve, reject, revert (with correct targets), forward in Phase 1 only, EC parallel voting with quorum, CFO then CEO, resubmission with hot-field detection (resume or fresh cycle), and supersession of old routes.
3. Persistence design for routes, steps, actions and votes for OPEX and CAPEX (separate tables), reviewed in a design note before migrations.
4. Policy-version snapshotting so in-flight requests keep their version.

Tests: every scenario in 4.12, property-based tests for boundaries, rounding and range coverage, concurrency tests for quorum, and 100 percent branch coverage on the engine.

Out of scope: forms and screens, except a minimal developer test harness.

Done when: all 14 scenarios pass, coverage target is met, a demo script routes sample requests through all four routes and prints their decision traces, and the persistence design note is approved.

Ask me about: Part 18 items 8 to 12 and 22 to 23 before coding revert and resubmission.
```

### 24.5 Phase 1C — OPEX

```
PHASE 1C — OPEX REQUESTS AND APPROVALS
Read first: Parts 4, 6, 8, 12, 17 (OPEX rows), 21, 22, 23.

Goal: a requester can raise an OPEX request and have it routed and approved end to end.

In scope: the OPEX form with every section, field, validation and document rule in Part 6 (vendor creation, charge allocation, invoice and previous-expense grids, GST, WHT, Sales Tax Authorities, PKR conversion, amount in words, checklist and uploads); the validate and submit endpoints with atomic save, routing and decision trace; the OPEX Approval Inbox with My Requests and My Approvals; the approval view with timeline, EC progress and action panel; revert, correction and resubmission; notifications through the outbox for every event in Part 12; PDF download for approved requests; permissions and audit throughout.

Before building, send me one batch of questions for the lookup values in Part 18 item 16 (request types, payment terms, checklist items, formats). Use the Part 18 defaults for the rest.

Out of scope: CAPEX, reporting, drafts and anything marked P2.

Done when: end-to-end tests pass for Routes A, B, C and D including forward, revert and correction, a rejection, an EC quorum, and the no-route error that saves nothing; the Part 23 checklists for the OPEX form, approval workspace and notifications pass; and I can demonstrate each route live.
```

### 24.6 Phase 1D — CAPEX

```
PHASE 1D — CAPEX REQUESTS AND APPROVALS
Read first: Parts 4, 7, 8, 12, 17 (CAPEX rows), 21, 22, 23, and the OPEX implementation from Phase 1C.

Goal: the same end-to-end capability for CAPEX, with its own tables and configuration.

In scope: the CAPEX form per Part 7 (asset-book allocations, classification with values, nature of purchase, budget status and reason, warranty rules, item-wise grid with calculated actual, budget and variance columns, generated budget summary, finance fields); the administrator-managed CAPEX eligibility policy seeded with Admin, Information Technology and Digital; the CAPEX Approval Inbox and approval view; revert, correction, resubmission; notifications; PDF.

Reuse shared domain services and UI components from Phase 1C, but keep CAPEX request, routing and approval data in separate tables. Do not copy and paste code. Extract what is shared.

Ask me about: Part 18 items 16 to 18 before building the finance fields and classification rules.

Done when: the same end-to-end test set as Phase 1C passes for CAPEX, the CAPEX checklists in Part 23 pass, and eligibility is enforced on new requests but not on correction of returned ones.
```

### 24.7 Phase 1E — Reporting, migration and hardening

```
PHASE 1E — REPORTING, MIGRATION AND HARDENING
Read first: Parts 9, 10, 11, 14.3, 17, 19, 23.

Goal: make the product pilot-ready for a bank.

In scope:
1. Reporting per Part 9 with scoped access, summary cards, filters, analytics, audited export, and the Executive dashboard from 14.3.
2. The global audit viewer and exports per Part 10, including the request timeline.
3. V3 data migration tooling per Part 17: extraction, staging, mapping, reconciliation reports, two dry runs on a copy of V3 data, attachment migration with hash verification.
4. Security: a threat model, the ASVS level 2 checklist, dependency and licence report, secrets handling, rate limiting, upload hardening, data masking for CNIC, and penetration-test readiness notes.
5. Performance tests against the targets in Part 19 with a realistic data set.
6. Operations: Docker and IIS deployment guides, backup and restore runbook, monitoring and alerts, administrator and user guides.

Done when: the Part 23 checklists for reporting, audit and access pass; the migration dry run reconciles counts and amounts per status; the performance targets are met or the gaps are documented; and a pilot-readiness checklist is delivered.
```

### 24.8 Phase 2 modules (one prompt per module)

Phase 2 starts only after Phase 1E is accepted. For each module I will send a short specification addendum together with this prompt:

```
PHASE 2 MODULE: <name>
Read first: the relevant parts of the master prompt and my addendum below.
Produce the design note first and wait for approval. The module must plug into the existing delegation matrix and approval engine. It must not create a second routing system. Apply the Standing Preamble rules.
```

Candidate modules: Workflow Designer (visual editor of the delegation matrix), Rule Builder, Delegation and proxy approval, Escalations and reminders, Integrations (ERP, HRMS, webhooks), Access Policies and MFA enforcement, Tenant administration, Drafts, scheduled currency rates, and the unbudgeted limit once its behaviour is specified.

### 24.9 Utility prompts

- **Design note review:** "I have read your design note. Approved with these changes: <list>. Before you build, update the note with the changes, confirm any impact on other modules, and then start."
- **Answers to open questions:** "Here are my answers to the open questions: <item numbers and answers>. First update the Master Build Prompt (Part 18 and any affected rules), show me the changed sections, then re-plan the current phase and continue."
- **Change request:** "A business rule changes: <describe>. Do not change code yet. First update the affected sections of the Master Build Prompt and show me the diff. Then give an impact analysis covering data model, API, screens, tests and existing data. After I approve, implement it and update the tests."
- **Bug report:** "Bug: <what happened, steps, expected result>. First write a failing automated test that reproduces it. Then fix it. Check whether the same flaw exists in the OPEX or CAPEX counterpart, and report what you found and changed."
- **Phase report request:** "Give me the Phase Report: what was built, the tests run and results, coverage numbers, deviations from the master prompt, open questions, risks, and your recommended next step. Include the results of each Part 23 checklist item as pass or fail."
