# Open Questions for the Product Owner (Phase 0 batch)

**Addressed to:** Sardar Rafhy, Warmbytes (Pvt) Ltd
**Source:** Master Prompt Part 18, plus gaps and conflicts found while writing the Phase 0 plan and the Phase 1A data-model note.
**Rule:** Every item below is marked **Not Yet Verified** in code and docs until answered. The default shown is what the agent uses until the answer arrives. Nothing here is guessed silently.

Reply with the item number and your answer, for example: `1: level numbers carry no meaning. 2: default. 6: separate limits.` Replying "default" accepts the default.

---

## A. Needed before Phase 1A (configuration)

| # | Question | Default used until answered |
|---|---|---|
| 1 | **Level naming.** The notes describe Level 1 both as the highest amount and as 10K to 20K. Confirm that level numbers carry no meaning and the amount range decides. | Numbers carry no meaning. Range decides. |
| 2 | **Rule lookup.** Confirm: subcategory rule first, then category-wide rule. | As stated in 4.2. |
| 3 | **Standalone and recommending authority.** V3 required an RA on every rule. Confirm that standalone rules have no RA. | No RA on standalone rules. |
| 4 | **Combined routes.** V3 text says EC levels and/or CEO, CFO, Head of Admin. Confirm that a rule is either EC or standalone. | A rule is either EC or standalone. |
| 5 | **Several RAs on one rule.** Confirm that any one RA may act and the first decision completes the stage. | Any one may act. |
| 6 | **Department-head limit per nature.** Separate OPEX and CAPEX limits? | Separate limits. |
| 7 | **No limit configured.** Confirm the seeded 20,000 PKR `DefaultDeptHeadLimitPkr` setting. | 20,000 PKR. |

### New questions from Phase 1A design (1A-1 to 1A-7)

| # | Question | Default used until answered |
|---|---|---|
| 1A-1 | **Conflicting zero limit (Part 5.5 vs 4.2).** 5.5 says the limit "starts from 0" and must be greater than zero. 4.2 says a 20,000 default applies when no active limit exists. Which applies to a new tenant before the administrator sets a limit? | Seeded `DefaultDeptHeadLimitPkr` = 20,000 applies until an administrator creates an active limit. A saved limit must be greater than zero. |
| 1A-2 | **Limit of exactly zero.** Is a zero limit meaningful (send every amount to the matrix)? | Not allowed. A limit must be greater than zero. |
| 1A-3 | **Maximum members per EC level.** Part 5.3 says "up to 10 on creation". Is 10 a hard cap on the number of members, or only on the creation screen? | Hard cap of 10 members per level. |
| 1A-4 | **Single active CEO, CFO and Head of Admin & GST.** Confirm this applies to the pool as a whole (one active holder per designation) and that a level can still include the CEO or CFO as a member. | As written in 5.3. |
| 1A-5 | **Recommending authority duplicate rule.** Part 5.4 says "duplicate combinations are rejected" without naming the combination. Proposed: the same department and user cannot be active twice. Is the title part of the key? | Department plus user is unique among active authorities. |
| 1A-6 | **Publish semantics.** Part 21.2 lists `POST /delegation-rules/{id}/publish`. Given maker-checker, does "publish" mean the maker submits the change (creates a pending change request), or the checker approves it? | Maker submits. Checker approves. The endpoint name is kept for the submit step. |
| 1A-7 | **Apply-on-approval.** Maker-checker stages the change and applies it only when a checker approves (ADR-0010). Do you agree, and may a checker edit before approving? | Stage and apply on approval. No editing by the checker, only approve or reject with remarks. |

---

### Permission conflicts found in Part 20 (Phase 0)

| # | Question | Default used until answered |
|---|---|---|
| 1A-8 | **Organisation and Users permissions.** Part 20.2 gives no role `org.department.manage`, `org.branch.manage`, `org.hierarchy.manage` or `org.user.view`, although Departments, Branches, Hierarchy and Users are P1 screens. Who maintains organisation data? | System Administrator holds the four permissions (PROPOSED, marked in `web/src/session/roles.ts` and `docs/permission-matrix.md`). EDM Administrator does not see these screens, as Part 20.2 says. |
| 1A-9 | **Organisation view for approvers.** A Department Head needs to see their department's users and hierarchy to resolve approvers. Which permission grants this read-only? | Department Head uses hierarchy-derived read access. No new permission is added until confirmed. |

## B. Needed before the routing engine (Phase 1B)

| # | Question | Default used until answered |
|---|---|---|
| 8 | **Revert recipient and selective revert.** What exactly happens when a user chooses a previous approver? Which V3 procedure is the reference? | Part 4.5. |
| 9 | **EC votes on resume after a revert.** Are votes cleared and logged? | Cleared and logged. |
| 10 | **Standalone revert target.** Where does a standalone approver's revert go? | Last Phase-1 approver. |
| 11 | **Forward recipient eligibility.** Who may be a forward recipient? | Any active user in an eligible set that the administrator defines. |
| 12 | **Self-approval.** When routing resolves the requester as approver, does the step move to the next head up the hierarchy? | Next head up the hierarchy. |
| 22 | **Already Approved as a hot field.** Changing this flag during a revert forces a fresh cycle? | Yes. |
| 23 | **PKR rounding.** Two decimals, half away from zero? | Yes. |
| 1B-1 | **CEO action tags at the EC stage.** Part 4.4 says CEO is configurable, and V3 behaviour is approve and revert only. Which default applies to a new CEO assignment? | Approve, reject and revert (configurable per assignment). |
| 1B-2 | **Department Head with no hierarchy record.** If `department_heads` has no current row for a department, should the request be blocked with EDM-ROUTE-003? | Yes, blocked with EDM-ROUTE-003. |

---

## C. Needed before OPEX (Phase 1C)

| # | Question | Default used until answered |
|---|---|---|
| 13 | **Others category.** How does V3 route an Others request? | Needs a rule like any other category. |
| 14 | **Other currency field.** Is it still needed? | Removed, unless a currency named Other is configured. |
| 15 | **OPEX charge total.** Must the branch and Head Office charges total the approval amount? | No check until confirmed. |
| 16 | **Lookup values and formats.** Request types, Term of Payment, Nature of Purchase, Budget Comments, checklist items per request type, vendor field formats (CNIC, NTN, STRN), and the reference number format including the V3 format. | Placeholder lists marked Not Yet Verified. Reference format per ADR-0011. |
| 19 | **YTD and MTD.** Source for automatic calculation, and the purpose of `tbl_MonthlyExpenseHistory`. | Calculated from the expense register, read-only. |
| 21 | **Rejected requests.** Can they be copied into a new request? | Closed. Copy allowed as a new request. |
| 1C-1 | **Drafts.** Confirm Draft is a Phase 2 item in navigation (14.1) but a Proposed Enhancement in 6.1. Build it or not in Phase 1C? | Not in Phase 1C. Phase 2. |

---

## D. Needed before CAPEX (Phase 1D) and reporting

| # | Question | Default used until answered |
|---|---|---|
| 17 | **CAPEX finance fields.** Who fills Finance Date, Finance Comment and Budget Comments? | Requester fills them. Marked Not Yet Verified. |
| 18 | **Classification values.** Must they add up to the approval amount? | No check until confirmed. |
| 20 | **Download formats.** Beyond PDF, which formats are needed (Excel, CSV)? | PDF only. |
| 1D-1 | **CAPEX Budget Status values.** Confirm the list: Budgeted, Un-budgeted, Over-budget. | As listed in 7.5, marked Not Yet Verified. |
| 1D-2 | **Eligibility on resubmission.** Confirm: eligibility is enforced for new CAPEX requests only. Returned requests stay editable by their creator or revert recipient. | As written in 7.1 and 24.6. |

---

## E. Environment and platform (blocks Phase 0 backend)

| # | Question or request | Default used until answered |
|---|---|---|
| E-1 | **Backend build environment.** The sandbox cannot reach `api.nuget.org` or the .NET SDK download host, and Docker and PostgreSQL are not installed. Choose one: (a) allow outbound access to `api.nuget.org`, `builds.dotnet.microsoft.com` and a container registry for the PostgreSQL image; or (b) provide a build runner that has .NET 10, Docker and NuGet access. | The web shell is built and tested here. Backend code is not committed until it can be compiled. |
| E-2 | **Hosting model** for the first deployment (Part 24.2). | Docker Compose on-premise. IIS option in Phase 1E. |
| E-3 | **Unbudgeted limit and exceeding limit data.** Part 17 says migrate `tbl_ExceedingApprovalLimit` data and build no behaviour. Confirm this migration is needed in the first release, or can wait for Phase 2. | Migration deferred to Phase 1E. |

---

## F. Phase 0 and platform decisions

| # | Question | Default used until answered |
|---|---|---|
| 25 | **First deployment.** Target bank, hosting model and SSO provider (Entra ID, ADFS, Okta, other, and whether AD/LDAP bind is needed). | OIDC and SAML both supported. Provider chosen at deployment. |
| 24 | **Retention periods** for requests, attachments and audit data. | 10 years proposed for requests and audit, to be confirmed with the bank's policy. |
| F-1 | **Component library.** Approve ADR-0003 (Fluent UI React v9) or choose shadcn/ui. | Fluent UI React v9. |
| F-2 | **Policy-version and maker-checker design.** Approve ADR-0010 (stage and apply on approval). | Proposed. Phase 1A is blocked on this. |
| F-3 | **Digit grouping default.** International (1,000,000) or lakh and crore (10,00,000) for the default display? | International. User-selectable as specified. |
| F-4 | **SAML library.** Shortlist and licence approval for the SAML library (ADR-0006). | Evaluated during Phase 0. No commercial-only licence without approval. |

---

## G. Compliance and ownership

| # | Question | Default used until answered |
|---|---|---|
| 26 | **Ownership and licensing** of EDM (intellectual property) confirmed in writing before commercial rollout. | Not an agent task. Flagged for the owner. |
| G-1 | **Applicable regulations.** SBP technology governance, outsourcing and internal-control expectations (Part 19 says Not Yet Verified). Which controls must be recorded against a control list? | Control list started from ASVS L2 and Part 10 and Part 11. Regulations to be confirmed. |
| G-2 | **Performance targets.** Confirm the Part 19 targets (routing under 100 ms, reads under 300 ms at the 95th percentile) with the first bank. | Design to these targets. |
