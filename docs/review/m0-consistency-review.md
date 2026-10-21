# M0 Architecture Consistency Review · Final v0.1

- **Review disposition:** **PASS (architecture documentation / behavioral-contract scope)**
- **M0-B01:** **RESOLVED**; owner-approved C01–C07 supply the M0-level observable protocol and safety semantics.
- **Overall M0:** **CLOSED / FORMALLY ACCEPTED BY PRODUCT OWNER (2026-10-21)**. The owner explicitly accepted all six ADRs, C01–C07 and the final M0 PASS report. This is an architecture/documentation sign-off ONLY, not approval to merge PR #1 or launch T001.
- **Review date:** 2026-10-21
- **Repository / PR:** [SharpHarness/SharpHarness PR #1](https://github.com/SharpHarness/SharpHarness/pull/1), branch `docs/architecture-v0.1` (OPEN, not merged at review).
- **Scope limitation:** documentation design audit only. There is no production implementation or executed cross-platform/build/runtime/crash/security test evidence to pass.

## 1. Evidence and approach

- Inspected the GitHub review-branch tree of **21 Markdown documents**: root governance/README, documentation index, all six ADRs, five architecture-area documents, both contract specifications, review checklist, this review report, T001 task brief and test strategy.
- Re-read all six ADRs and seven contract decision statuses. All **ADR-0001..0006 = Accepted** and **C01..C07 = Accepted** by the product owner.
- Statically validated **28 relative Markdown file references** across the documentation corpus against branch file paths: **0 missing file targets**.
- Verified unique, increasing test-plan identifiers **RT-001..RT-057 (57 requirements)** and **CT-001..CT-032 (32 contract acceptance scenarios)**. These are *documented requirements*, **not executed or passing unit/integration tests**.
- Cross-checked observable behaviors, actor/storage ordering, approved tool security gates, compatibility and stage boundaries against the accepted decision records and T001 scope.

## 2. Consistency matrix

| Decision | Observed aligned contracts/architecture | M0 review |
| --- | --- | --- |
| ADR-0001 Thin Kernel | Core delegates interchangeable services to interfaces/DI; no Coding/SQLite dependency in Core | PASS |
| ADR-0002 Session Actor | One logical Session state writer, one active Turn, Busy rejects a second StartTurn; interrupt/approval remain responsive | PASS |
| ADR-0003 Command/Event | Typed versioned commands and receipts; durable sequenced facts vs ephemeral deltas; independent broadcast subscriptions | PASS |
| ADR-0004 Stage Extensions | Typed Context/Model/Tool/Event stages; Core fixes mandatory checkpoints; telemetry distinct from critical Journal | PASS |
| ADR-0005 Persistence | `ISessionStore` abstraction; first local production store SQLite / Microsoft.Data.Sqlite, WAL + FULL, Journal + optional Snapshots, Unknown side effects | PASS |
| ADR-0006 Authorization | Runtime-managed resolve/schema/scope → Policy Allow/Deny/Ask → bound Approval → committed intent → controlled Executor; no unsupported OS-sandbox claim | PASS |
| C01 Session ownership | Exclusive controlling handle per Runtime; concurrent independent read-only subscribers; cross-process ownership deferred | PASS |
| C02 Command idempotency | Persist Accepted and deterministic Rejected (Busy), immutable same-ID receipt; payload conflict and uncertain commit handled without fake success | PASS |
| C03 Event replay | FromBeginning / exclusive AfterSequence / LiveOnly, gap-free committed-fact replay/live transition within retention; bounded independent buffers and visible overflow | PASS |
| C04 Actor/Store | Actor solely decides state, independent storage I/O, acknowledged commit barrier plus live Turn/generation recheck, no late-callback dispatch | PASS |
| C05 Approval/Tool | Stable InvocationId, scope/argument/version-bound Approval, Succeeded/Failed/Rejected/Cancelled/Unknown distinct, no blind side-effect replay | PASS |
| C06 Dispose | Cancel + bounded cleanup, exclusive ownership release only when safe, ShutdownIncomplete quarantined, historical Unknown distinguishable from active execution | PASS |
| C07 Compatibility/Error | Independent ProtocolVersion/SchemaVersion/SDK version; explicit Major/Minor policy, unknown security/event semantics fail closed, structured error and safe retry boundaries | PASS |

No normative contradiction requiring reopening any ADR or owner-approved C01–C07 was identified in this static review.

## 3. M0-B01 resolution and the critical distinction

**Previous blocker:** the conceptual type catalog `docs/specifications/contracts-v0.1.md` is explicitly *Draft*, so it did not establish enough behavioral constraints for Coding Agents.

**Now resolved at the M0 behavioral level:** owner-accepted `docs/specifications/m0-contract-decisions-v0.1.md` states observable Session lifetime, Command receipt and duplicate semantics, durable Event cursors/backpressure, Actor/Store commit synchronization, precise tool/approval authority, shutdown/quarantine and version/error policies, with CT acceptance cases.

**Still Draft, by design:** concrete compile-ready `IAgentRuntime`/`IAgentSession` signatures, exact DTOs/enums/serialization API, DI lifetimes, SQLite table schemas/migrations/locking, CLI default grants and native execution/sandbox details are **not approved implementation specifications**. These are required before corresponding M2/M3 tasks, but are **not** M0 documentation blockers and must not be invented by a Coding Agent or implemented in T001.

## 4. Remaining risks, with explicit phase gates (nonblocking for M0)

| Item | Owner/implementation gate |
| --- | --- |
| T001 .NET 10 skeleton | **BLOCKED** until separate product-owner approval of T001 and implementation handoff. T001 may create only solution/projects/CI/minimal tests; no Agent Loop, public runtime, SQLite code or mutating tools. |
| Compile-ready public C# APIs and DTOs | Design review before relevant **M2** public contract implementation and NuGet publication. |
| SQLite schema, atomic transition API, locking, backup, migrations, crash tests and native packaging | Design + implementation acceptance during **M3**; tests not yet executed. |
| CLI permission defaults, filesystem TOCTOU/path scope, Shell/process tree, trust, task grants | Explicit policy/security contract before **M3** tools ship. |
| OS sandbox / hostile in-process plugin confinement | Not v0.1 capability; never advertise Policy or a working directory as isolation. |
| Provider details, source-generated serializers / Native AOT, RPC/distributed ownership | Later scoped tasks; do not silently assert compatibility. |
| Repository open-source License | Product owner selects and adds a license before distribution/third-party reuse, not a requirement for this architecture-only static audit. |

## 5. Tests vs evidence

- **Passed now:** owner decision-status audit, document link validation, consistency review of approved behavioral contracts, test ID continuity, stage/task boundary inspection.
- **NOT VERIFIED:** `dotnet restore/build/test`, Windows/Linux/macOS CI, SQLite WAL/FULL runtime, crash/Unknown behavior, actual process-tree cleanup, sandbox guarantees, binary public API compatibility, Native AOT.
- Test cases `RT-001..RT-057` and `CT-001..CT-032` are **acceptance plans** for later implementation stages. A passing static document audit is not evidence that any listed behavior has been implemented.
- T001 remains marked BLOCKED in `docs/tasks/T001-bootstrap.md` and `AGENTS.md` still forbids development without approved tasks.

## 6. Final reviewer determination and product-owner sign-off

**M0-B01: PASS / RESOLVED.**

**M0 Architecture v0.1: PASS and FORMALLY CLOSED by the product owner's explicit sign-off on 2026-10-21.** No remaining M0-level normative contradictions or incomplete owner decisions were found. This does **not** freeze unreviewed C# API signatures or imply that tests and implementation are passing.

**Owner-approved M0 scope boundary (as signed off):** PR #1 remains **OPEN**, never auto-merge into `main`; no implementation without separate task-specific authorization. **Post-sign-off update, 2026-10-21:** the owner has since separately **approved T001 v0.1 for M1 scaffold implementation**, with constraints and acceptance defined in [T001 review](t001-readiness-review.md) and [task brief](../tasks/T001-bootstrap.md). This later task authorization is not T001 completion/acceptance and does not authorize merging PR #1, runtime features or other implementation tasks.
