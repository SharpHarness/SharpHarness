# M0 Architecture Consistency Review · v0.1

- **Status:** Changes Required (M0 exit gate not yet approved)
- **Review date:** 2026-10-20
- **Repository / PR:** [SharpHarness/SharpHarness PR #1](https://github.com/SharpHarness/SharpHarness/pull/1)
- **Scope:** Architecture and agent-governance documents only; no production code, builds or executable runtime tests have been reviewed.
- **Governance:** Product owner has independently approved ADR-0001..0006. Architecture reviewer checks consistency and recommends status; implementation and task launch still require separate authorization.

## 1. Evidence and audit method

Read all 19 initial Markdown files from branch `docs/architecture-v0.1` and reviewed the accepted ADRs, architecture overview and subsystem boundaries, public conceptual contracts, test matrix, `AGENTS.md`, and `T001-bootstrap.md`. Confirmed all six ADRs contain Accepted status, with ADR-0001 accepted earlier as product direction; ADR-0002..0006 accepted explicitly in review. Verified the test matrix RT-001..RT-036 is unique and continuous, and all Markdown relative file references resolve after adding this report.

This is a **documentation-level consistency review**. It does not constitute actual execution of tests, a penetration test, a Windows/Linux/macOS build, a SQLite crash test or a validation of unimplemented contracts.

## 2. Findings already consistent with approved ADRs

| Area | Evidence | Reviewer finding |
| --- | --- | --- |
| Product scope | ADR-0001, overview, module-boundaries | General SDK + Coding Profile and thin Core are compatible |
| Session concurrency | ADR-0002, runtime-protocol, contracts | One logical Session writer / one active Turn, second StartTurn rejected Busy |
| Commands and events | ADR-0003, runtime-protocol, contracts | Accepted receipt distinct from Turn completion; durable sequence vs ephemeral deltas, multiple subscribers and deduplication |
| Typed extension stages | ADR-0004, module-boundaries, model-and-context | Core controls checkpoints, standard services composed with DI, no universal middleware |
| Local persistence | ADR-0005, persistence-and-recovery, contracts | SQLite via Microsoft.Data.Sqlite, WAL + FULL; append-only Agent Journal, optional coherent snapshot, storage-independent Core |
| Tool security | ADR-0006, tools-and-security, contracts | Resolve/validate scope → Policy Allow/Deny/Ask → bound Approval → durable SQLite intent → Executor |
| Trust boundary | ADR-0006, tools-and-security | No complete OS sandbox claimed; runtime-mediated gate does not confine arbitrary same-process .NET code |
| Source control / tasks | AGENTS.md, T001 | Coding agents do not implement without task approval; reviewer only designs/reviews/tests/accepts |

No contradiction was found that would require reopening an approved ADR. Explicit and limited trust claims are essential: Policy controls **runtime-mediated** tool calls, not arbitrary OS access from plugins.

## 3. Open blockers for the M0 **document/sign-off gate**

### M0-B01 · Approved and sufficiently precise interface contract is missing (blocking M0 close)

**Evidence:** `docs/specifications/contracts-v0.1.md` explicitly says *Draft* and *not yet stable public API*. Important behavioral details remain unresolved, including:

- Final lifecycle, command/receipt/error model, CommandId dedup/restart/payload conflict, subscription cursor, slow-client semantics and Dispose cancellation behavior.
- Single-writer Actor ↔ storage executor handoff, durable facts ordering, failure/acknowledgement semantics and time-of-commit barriers.
- Tool invocation ID, approval binding, resolved-scope canonicalization, operation classification, timeout/failure/Unknown semantics.
- Public type ownership and DI registration/lifetimes for Abstractions vs standard services; no premature freeze of unnecessary NuGet surface.

**Action:** Architecture reviewer prepares a versioned *contract decisions* supplement identifying what is normative for M1/M2 and what is explicitly deferred to M3, with testable acceptance cases. Product owner approves that scoped specification. In particular, no Coding Agent invents contract behavior from a conceptual type catalog.

**Important:** T001 scaffold explicitly prohibits implementation of Agent Loop, tool execution, SQLite schemas and broad public APIs, so this blocker **does not indicate T001 design is incorrect**. It blocks calling the whole M0 documentation phase completed under the current M0 exit definition.

## 4. Items required later, but **not blockers to a bare .NET scaffold**

- **M1 task authorization:** `T001-bootstrap.md` is marked BLOCKED. Even after M0 approval it must be explicitly approved by the product owner before any Coding Agent implementation starts.
- **CLI default policy and permission matrix:** accepted ADR-0006 specifies **mechanism**, not default file/Shell/network grants. Must be decided before enabling production Coding tools (M3). Never silently ship permissive defaults.
- **SQLite detailed design:** transaction/DDL/locking/migrations/backup/retention/recovery and explicit native asset matrix to be approved before the production storage task (M3).
- **Native sandbox:** separate workstream. Runtime must disclose that shell and in-process plugins can use user/process privileges; never claim sandbox coverage without evidence.
- **License:** repository currently has no chosen license. Product owner must select one before distributing or soliciting external contributions; public GitHub visibility does not imply an open-source grant.
- **Public versioning and provider contract detail:** complete before implementation/public release of corresponding API and model adapter tasks.

## 5. Correct test-stage boundary

M1 T001 is **skeleton-only**: build, project graph and minimal tests on Windows/Linux/macOS. It MUST NOT pretend SQLite or Tool/Policy runtime is implemented.

M2 permits deterministic Fake Model, Fake Tool and **Fake SessionStore** for test of control flow and policy/approval semantics. Production mutating commands must **not** become enabled on a fake/in-memory store without the accepted ADR-0005 durable pre-execution guarantee. M3 implements and verifies SQLite plus side-effect execution, recovery and safety checks.

Documentation RT-001..RT-045 is a **requirements/test-plan inventory**, not evidence that tests pass.

## 6. Recommendations and acceptance

- **ADR decision consistency:** Pass at the conceptual architecture level; six ADRs accepted, no contradiction requiring ADR rollback found.
- **Markdown links, ADR state, test ID uniqueness:** Pass by structured static review of the committed documents; report link is satisfied by this document.
- **Implementation and runtime behavior:** Not Verified (no code exists in reviewed scope).
- **M0 overall:** **Changes Required**, pending an approved scoped contract supplement (M0-B01) and final owner sign-off. Do not merge PR #1 as a fully accepted M0 baseline solely because six ADRs are Accepted.
- **T001 readiness:** Scope appears appropriately limited; still BLOCKED until task-specific owner approval, and separation from M2/M3 confirmed.

### Next architecture-only task

A [proposed C01–C07 contract-decision supplement](../specifications/m0-contract-decisions-v0.1.md) has been drafted for owner review. **The M0-B01 blocker remains OPEN**; this proposal does not constitute contract approval. C01–C04 are Accepted; C05–C07 remain Proposed. The next step is to review and explicitly accept or revise the remaining decisions. Limit decisions to the observable SDK protocol, session concurrency, Tool/Policy authority, and safe-storage ordering; leave provider wire formats, SQLite DDL and TUI rendering to later tasked contracts. Then rerun M0 documentation checks, present remaining product choices, obtain explicit owner sign-off and only then authorize T001.

**Reviewer limitation:** No build, unit test, crash recovery test or cross-platform runtime test can be marked Pass because this branch currently contains design documents only.
