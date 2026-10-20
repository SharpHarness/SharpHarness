# M0-B01: Runtime Public-Contract Decisions v0.1

- **Status:** Proposed; owner review required (NOT Accepted)
- **Created:** 2026-10-20
- **Scope:** observable runtime protocol and guarantees required for the M0 architecture sign-off; not production C# source or a frozen NuGet API.
- **Authority:** ADR-0001..ADR-0006 are Accepted. This document proposes narrower contract decisions beneath them; rejected/modified choices must be reflected here before the M0-B01 blocker can close.
- **Reference:** [M0 review](../review/m0-consistency-review.md), [existing type catalog](contracts-v0.1.md), [runtime protocol](../architecture/runtime-protocol.md), [recovery](../architecture/persistence-and-recovery.md), [security](../architecture/tools-and-security.md).

## 0. Normative levels and phase boundaries

Each decision below is **Proposed** until explicitly approved; use **MUST**, **MUST NOT** and **SHOULD** to express recommended requirements, not to claim existing behavior. The Accepted ADR principles remain binding regardless of approval of this supplement.

- **M1 / T001:** empty multi-project .NET 10 skeleton, test discovery, CI on three OS; no public API, Agent Loop, SQLite schema or Tool Executor implementation required. All contract examples here are **documentation**, not implicit authorization to code.
- **M2:** design and implement behaviorally approved Core/Abstractions contracts using fake model/tool/store; deterministic tests of concurrency/receipts/streams, not deployment of mutating tool operations using volatile stores.
- **M3:** approved production SQLite store and controlled Coding Tool/Policy execution. Any action capable of real-world side effects requires successful pre-execution durable intent, or must be disabled until reliable storage is available.
- **Later:** actual RPC protocol, distributed leases, Native AOT, third-party untrusted in-process plugin isolation, OS sandbox and full provider wire-level contract.

## C01 · API surface and ownership — RECOMMEND APPROVE

**Public semantic root:** a host uses `IAgentRuntime` to open an independent `IAgentSession` by explicit SessionId/create request. `IAgentSession` exposes: submit a versioned `AgentCommand`, subscribe to typed `AgentNotification` using a durable cursor option, inspect minimally required identity/lifetime data, and asynchronously dispose of its session handle. These are **conceptual members**, not final compile-ready method signatures.

**Invariants**
- Stable opaque SessionId; TurnId unique within Session; CommandId stable per command; InvocationId stable for one logical tool action. IDs are not derived from user file paths.
- One logical Actor writes each Session's mutable state; at most one active Turn. A different `StartTurn` in a busy Session MUST return `Busy`; it MUST NOT queue or become `SteerTurn`.
- Different Sessions may operate concurrently; Core MUST NOT reference concrete models, tooling, UI, SQLite or Coding projects.
- Host DI composition root creates runtime and explicit per-Session service scope; resources owned by a Session scope are disposed only when no active controlled operations rely on them. Avoid a global mutable service locator and unaudited arbitrary Hooks.
- Opening the same persisted Session concurrently from two live owners MUST fail with an explicit ownership conflict; v0.1 makes **no** claim of cross-host/distributed ownership. Precise local-process ownership/lease mechanics belong to the persistence adapter contract.

**Review question C01-A:** Should a second `OpenSession` in the same host reuse the Actor or reject with `SessionAlreadyOpen`? **Recommendation:** reject with `SessionAlreadyOpen` to keep ownership and disposal unambiguous. Future multi-handle attachment can occur through subscriptions instead.

## C02 · Command acceptance and idempotency — RECOMMEND APPROVE

**Command envelope minimum:** `ProtocolVersion`, `SessionId`, `CommandId`, typed payload (`StartTurn`, `SteerTurn`, `InterruptTurn`, `ResolveApproval`), and correlation when applicable. An accepted `StartTurn` receipt provides the assigned TurnId; the receipt says the command was admitted, **not** that the Turn completed.

**Proposed `CommandReceipt` outcomes:** `Accepted`, `Busy`, `InvalidState`, `InvalidCommand`, `NotFound`, `DuplicateCommandConflict`, `StorageUnavailable` and `OwnershipConflict`. Permission denials should be represented by the corresponding tool/approval or command rejection as appropriate. The exact C# discriminated-union/enum/error struct design is deferred to type-level review.

**Replay and conflict**
1. `(SessionId, CommandId)` is the deduplication key. On the first submission, record command identity, a canonical payload fingerprint, its acceptance/rejection receipt and necessary durable facts before claiming durable acceptance.
2. A repeated **identical** command returns the original receipt without reapplying its side effects, even if the Session state has since changed. Example: a previously `Busy` rejected `StartTurn` does not become accepted by retrying its original CommandId after the active Turn ends.
3. Reuse of the same CommandId with **different canonical payload** returns `DuplicateCommandConflict` and performs no action.
4. For production SQLite storage, command deduplication records MUST survive process restart as long as the relevant Session is retained; no silent TTL pruning before an explicit retention policy. For fake/volatile M2 stores, dedup scope is only the fake Session's lifetime and is **not** evidence of crash safety.
5. If cancellation or transport failure occurs before receiving `CommandReceipt`, the caller cannot infer whether admission happened. Retrying with the **same** CommandId is the safe client operation.
6. `SubmitAsync` cancellation stops waiting for an acceptance result; it cannot cancel an already accepted Turn. Use `InterruptTurn` to request a Turn interruption.

**Review question C02-A:** Approve persistent deduplication of both Accepted and deterministically Rejected commands, including Busy? **Recommendation:** yes; avoids ambiguous retry outcomes.

## C03 · Durable Events, subscription cursor, notifications — RECOMMEND APPROVE

**Envelope:** `SchemaVersion`, `EventId`, `SessionId`, optional `TurnId`, strictly increasing per-Session durable `Sequence`, timestamp and immutable typed payload. Journal facts are the authoritative persisted output. Streaming text/progress deltas are ephemeral and have no durable Sequence.

**Proposed public subscription choices** (conceptual only):
- `FromBeginning`: replay facts after Sequence 0 then continue live;
- `AfterSequence(n)`: replay facts with Sequence **greater than** `n`, then continue live;
- `LiveOnly`: start from the current durable high-water mark, with no earlier facts.

When reliable storage is configured, replay and transition to live delivery MUST be gap-free for committed durable facts. At-least-once re-delivery across reconnection is acceptable; consumers deduplicate by `(SessionId, Sequence)` or `EventId`. Fact events MUST be published only after their transaction commits successfully. Subscribers are broadcast consumers, never competing readers of one work queue.

A slow or disconnected subscriber MUST NOT block the Session Actor or silently lose durable facts. When bounded subscriber buffering cannot keep up, signal explicit overflow/close; the caller resubscribes from the last durable Sequence it has actually processed and retained, not an unimplemented server acknowledgement mechanism. Ephemeral deltas may be dropped or coalesced, but any loss must be signaled to consumers expecting accurate progress; completion can be reconstructed from durable terminal events. Canceling a subscriber does not cancel the Session or another subscriber.

**Review question C03-A:** Approve cursor semantics and *fail/reconnect* on durable subscriber overflow, rather than unbounded buffering or blocking? **Recommendation:** yes.

## C04 · Actor and durable-store coordination — RECOMMEND APPROVE

- Only the Actor commits logical state transitions and assigns next durable sequence; storage can run on a separate executor/worker so long synchronous SQLite I/O does not block the mailbox.
- All durable state changes relied upon for side-effect dispatch MUST cross a **commit acknowledgement barrier**. A `Prepare/Start ToolInvocation` fact is written before beginning any mutating operation. Failed or uncertain acknowledgement MUST fail closed. After a crash, an operation that may have executed but lacks a trustworthy terminal fact is `Unknown`; never blindly retry.
- The transaction that records a command admission/receipt, corresponding durable facts, next Sequence and derived state must preserve atomic semantics as specified for that transition. Storage must reject stale expected Sequence/Version and never invent missing facts.
- An Actor may continue processing `InterruptTurn` and approval messages while an asynchronous persistence operation is outstanding, but it MUST NOT commit contradictory state or release a tool for execution before the applicable transaction completes. Internal storage completion carries a correlation/generation token so late callbacks cannot mutate a closed/superseded Turn.
- The **storage executor does not become a second Session state writer**: it performs requests prepared by the Actor and returns results. The Actor decides transitions.
- No exactly-once claim for external Shell/Git/filesystem changes. WAL + FULL is the production SQLite adapter baseline from ADR-0005, not a property of Fake Store.

**Review question C04-A:** Approve commit-acknowledged facts and actor-serialized state changes while permitting a separate storage executor? **Recommendation:** yes.

## C05 · Approval, Tool and failure authority — RECOMMEND APPROVE

- Every **runtime-mediated** invocation resolves tool identity/version; validates schema and canonical effective resource scope; receives Policy verdict `Allow / Deny / Ask`; if Ask, validates exact Approval; then persists execution intent when side effects are possible; only then dispatches controlled Executor.
- Approval binds `SessionId`, `TurnId`, `InvocationId`, tool ID/version, canonical argument fingerprint, resolved scope, permissions and expiration. The Runtime MUST revalidate an approval after resumption and before dispatch; expired, mismatched, interrupted/cancelled or otherwise stale approvals fail closed.
- `ResolveApproval` is a Command, serialized through Actor, not free-form model text. Duplicate exact decisions can return stable receipts; conflicting or late decisions cannot authorize execution.
- Deny/Ask failure does not invoke Executor. Tool result outcomes include Succeeded, Failed, Rejected, Cancelled, Unknown; Unknown specifically covers plausible external effects without a trustworthy terminal fact.
- **Security scope:** mandatory gates apply to runtime-mediated ToolInvocations. Untrusted native/process or arbitrary in-process .NET code remains outside claimed OS isolation. A working-directory limit or tool metadata such as `readOnly` does not amount to a sandbox.
- Default CLI Allow/Ask/Deny matrix, canonicalization/TOCTOU details, subprocess policy and safe workspace grants **require separate M3 contract**; they are *not* silently approved by this supplement.

**Review question C05-A:** Approve these externally observable authority and failure guarantees, deferring concrete CLI policy defaults? **Recommendation:** yes.

## C06 · Session disposal and cancellation — OWNER DECISION REQUIRED

**Recommended default:** disposing a Session requests an explicit orderly shutdown: stop admitting new commands, request cancellation of its active Turn, allow a configurable **bounded** graceful cleanup, and only then release the host's session ownership and resources as far as safe. Disposal cannot claim all external effects have stopped unless verified. If subprocesses may still be running or commit status is uncertain, report/record an observable `Unknown`/incomplete-shutdown outcome when possible and never silently mark success. Dispose of an event subscriber separately without affecting Session lifetime.

Reject the alternative of `Detach` as the implicit default because it can continue modifying files after the caller believes the session ended. Reject unbounded synchronous waiting because it can leave the CLI stuck forever. A future explicit detach/continue-running mode requires a separately governed host/runtime lifecycle and durable owner.

**Review question C06-A:** Approve **Cancel + bounded cleanup** as default, with explicit error/uncertain side-effect indication instead of silent detach? **Recommendation:** yes.

## C07 · Contract versioning and error behavior — RECOMMEND APPROVE

- Commands and durable event payloads have explicit protocol/schema versions and stable discriminators. Unknown command types and security-sensitive fields MUST fail closed, not be silently interpreted as innocuous defaults.
- Protocol invalidity, policy rejection, Busy, store unavailable and ownership conflict are reported as structured outcomes. Cancellation of the submit wait is NOT represented as a confirmed runtime rejection; caller may have lost the acknowledgement.
- Unexpected internal implementation exceptions may surface as structured runtime failures, but never disappear; telemetry failure alone must not override successful Agent execution, while durable storage failure must stop operations needing its guarantees.
- Time is UTC `DateTimeOffset`, opaque IDs must be stable, serialized records immutable/versioned. Public C# signatures, binary compatibility, source generators and package release versions must be reviewed before publishing NuGet packages.
- No volatile in-memory store may be advertised as providing production durability for side-effect execution.

## 1. Decision register for owner review

| ID | Choice | Recommendation | Status |
| --- | --- | --- | --- |
| C01 | Public root & exclusive Session ownership | explicit single owner; second Open rejects SessionAlreadyOpen | **Proposed** |
| C02 | Commands, receipts, persistent idempotence | deduplicate Accepted and Rejected by SessionId+CommandId | **Proposed** |
| C03 | Durable cursor and multi-subscriber backpressure | exclusive cursor; gap-free replay; overflow signals reconnect | **Proposed** |
| C04 | Actor ↔ store commit ordering | Actor sole state writer; async worker; required durable barrier | **Proposed** |
| C05 | Approval/tool authorization & failure | fixed gates; scope-bound approval; Unknown not retried | **Proposed** |
| C06 | Session disposal behavior | Cancel + bounded cleanup; no implicit detach | **Proposed** |
| C07 | Versions & errors | structured versioned contracts; fail closed | **Proposed** |

## 2. Acceptance matrix for contract decisions (design requirements, not run tests)

| Test ID | Scenario | Expected evidence when implemented |
| --- | --- | --- |
| CT-001 | Second OpenSession same SessionId | returns SessionAlreadyOpen; no duplicate Actor |
| CT-002 | Duplicate Busy StartTurn after active Turn completes | original Busy receipt returned; no Turn created |
| CT-003 | Same CommandId with different payload | DuplicateCommandConflict; no change |
| CT-004 | Submit acceptance response lost and caller retries | one durable command admission/Turn; original receipt retrievable |
| CT-005 | Reconnect from AfterSequence(n) during simultaneous publish | no committed durable event gap or reordering |
| CT-006 | Slow subscriber overflows | explicit cursor and reconnect outcome; other subscriber unaffected |
| CT-007 | Commit delayed/failed while interrupt arrives | Actor responsive; no unauthorized side effect; coherent state |
| CT-008 | Approval replay with altered args/scope or after interrupt | Executor not started |
| CT-009 | Persist intent, tool mutates, process crashes before terminal | Unknown; no automatic repeat |
| CT-010 | Dispose during running Turn or subprocess | bounded cleanup; no false claim of stopped effects |
| CT-011 | Unsupported protocol/command version | explicit rejection; fail closed |
| CT-012 | Fake Store vs SQLite production authorization | Fake Store cannot enable production mutating tool dispatch |

**Review evidence:** deterministic fake components for M2; SQLite, subprocess and crash-injection evidence in M3; no tests have run while documents-only.

## 3. Explicitly deferred documents (do not let Coding Agent guess)

1. Final public C# API signatures, DTO placement, binary/source versioning, DI service lifetimes and serializers before corresponding M2 API implementation.
2. SQLite table/index DDL, migrations, busy timeouts, backup, local locking and file path rules before M3 persistence adapter.
3. CLI permissions/grants, workspace canonicalization, shell/subprocess and network controls before M3 Coding Tool rollout.
4. Provider wire formats, token/usage semantics, concrete UI/TUI display and native OS sandbox in their respective tasks.

## 4. Review workflow

The product owner decides C01–C07 or requests changes. Upon approval, mark each decision Accepted, synchronize `contracts-v0.1.md` and relevant runtime/testing docs, revise the M0 consistency report, then conduct a second static consistency audit. M0 can be signed off **only after that review**. M1/T001 implementation still needs an explicit separate owner authorization.
