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

## C01 · API surface and ownership — ACCEPTED (product owner, 2026-10-20)

**Public semantic root:** a host uses `IAgentRuntime` to open an independent `IAgentSession` by explicit SessionId/create request. `IAgentSession` exposes: submit a versioned `AgentCommand`, subscribe to typed `AgentNotification` using a durable cursor option, inspect minimally required identity/lifetime data, and asynchronously dispose of its session handle. These are **conceptual members**, not final compile-ready method signatures.

**Invariants**
- Stable opaque SessionId; TurnId unique within Session; CommandId stable per command; InvocationId stable for one logical tool action. IDs are not derived from user file paths.
- One logical Actor writes each Session's mutable state; at most one active Turn. A different `StartTurn` in a busy Session MUST return `Busy`; it MUST NOT queue or become `SteerTurn`.
- Different Sessions may operate concurrently; Core MUST NOT reference concrete models, tooling, UI, SQLite or Coding projects.
- Host DI composition root creates runtime and explicit per-Session service scope; resources owned by a Session scope are disposed only when no active controlled operations rely on them. Avoid a global mutable service locator and unaudited arbitrary Hooks.
- **Approved scope: exclusive control per Runtime.** While one `IAgentSession` controls a SessionId within the same Runtime, another `OpenSession` for that ID MUST reject with `SessionAlreadyOpen`; it MUST NOT reuse/create a second Actor or silently transfer ownership. Multiple independent read-only event subscribers remain allowed through an observation/subscription entry point which does not take ownership or allow command submission. Multiple different SessionIds may operate concurrently.
- Opening an already persisted but **not currently controlled** Session may restore and take ownership. After a controlled Session is properly released, reopening it may restore under the recovery protocol; C06 will finalize release/disposal completion semantics. The exact subscriber API and handle types remain proposals until the API signatures are reviewed.
- **Boundary:** exclusive control is guaranteed within one Runtime instance. v0.1 makes **no** claim of cross-process, cross-host or distributed exclusivity merely from this API contract; a production SQLite adapter must prevent unsafe conflicting writers through its own separately reviewed ownership mechanism before such access is enabled.

**Approved resolution C01-A:** second `OpenSession` on the same Runtime while the Session is controlled returns `SessionAlreadyOpen`, with no second Actor and no implicit shared control handle. Independent observation subscriptions are allowed, but do not confer control. No second approval is needed for this decision.

## C02 · Command acceptance and idempotency — ACCEPTED (product owner, 2026-10-20)

**Command envelope minimum:** `ProtocolVersion`, `SessionId`, `CommandId`, typed payload (`StartTurn`, `SteerTurn`, `InterruptTurn`, `ResolveApproval`), and correlation when applicable. An accepted `StartTurn` receipt provides the assigned TurnId; the receipt says the command was admitted, **not** that the Turn completed.

**Candidate `CommandReceipt` outcome names (C02 behavioral contract approved, exact C# type names not frozen):** `Accepted`, `Busy`, `InvalidState`, `InvalidCommand`, `NotFound`, `DuplicateCommandConflict`, `StorageUnavailable` and `OwnershipConflict`. Permission denials should be represented by the corresponding tool/approval or command rejection as appropriate. The exact C# discriminated-union/enum/error struct design is deferred to type-level review.

**Replay and conflict**
1. **Approved:** `(SessionId, CommandId)` is the deduplication key. For an adequately identified, validated command, durably record its identity, canonical typed payload fingerprint, deterministic Accepted or Rejected receipt (including `Busy`) and any associated durable state/facts under an atomic transition. A persistable rejection MUST NOT change into acceptance when the Session state later changes. Exact fingerprint serialization/cryptographic format is subject to implementation contract review, but must include security-relevant typed payload semantics and protocol version.
2. **Approved:** A repeated command with the **same canonical payload** returns the *original, durably recorded receipt*, with no re-execution or additional state transition, even if Session state has changed. For example a previously `Busy`-rejected `StartTurn` MUST remain `Busy` when the same CommandId is retried after the active Turn ends; a new intent requires a new CommandId.
3. **Approved:** Reuse of a CommandId with a **different canonical payload** MUST produce `DuplicateCommandConflict` without altering the originally recorded command or performing any action.
4. **Approved:** On production SQLite stores, original receipts for both Accepted commands and **deterministically Rejected** commands MUST survive process restart while the relevant Session remains retained. No silent TTL pruning of required deduplication records. A fake/volatile M2 store only guarantees deduplication for its own lifetime and MUST NOT be described as crash-safe.
5. **Approved failure boundary:** If transport cancellation, store I/O failure, or *uncertain commit status* prevents a trustworthy receipt, the Runtime MUST NOT claim a durable success/rejection or fabricate the original result. Return an explicit storage/uncertainty error if feasible, otherwise the caller treats outcome as unknown and retries/queries with the **same** CommandId after recovery. The store reconciles whether an original receipt exists before any new evaluation; an ambiguous original transition MUST NOT authorize a side effect. Commands with malformed or missing identity which cannot be safely keyed, and storage failures without durable results, are not asserted to have persisted rejection receipts.
6. `SubmitAsync` cancellation stops waiting for an acceptance result; it cannot cancel an already accepted Turn. Use `InterruptTurn` to request a Turn interruption.

**Approved resolution C02-A:** Persist and deduplicate both Accepted commands and deterministically Rejected commands with valid identities, including `Busy`. The original reply is immutable for a given `(SessionId, CommandId, canonical payload)`. Different payloads conflict. Storage failure or uncertain commit never counts as a confirmed durable receipt. Detailed transaction boundary/canonicalization API belongs to C04 and future C# signature review.

## C03 · Durable Events, subscription cursor, notifications — ACCEPTED (product owner, 2026-10-20)

**Approved decision:** Use three subscription starts, broadcast to independent subscribers, preserve gap-free committed durable replay/live transition, and isolate slow subscribers with bounded buffering and explicit overflow/reconnect. Ephemeral deltas are neither durable journal facts nor guaranteed replay data. This decision specifies observable behavior, not final C# signatures.

### Durable facts and cursor

- A durable event envelope contains `SchemaVersion`, `EventId`, `SessionId`, optional `TurnId`, monotonic per-Session `Sequence`, timestamp and immutable typed payload. A durable event becomes externally visible as *committed* only after the corresponding persistence transaction has successfully committed.
- `FromBeginning`: replay the Session's retained durable history after the initial cursor (conceptually Sequence 0), then continue live.
- `AfterSequence(n)`: replay durable events with `Sequence > n`, then continue live. It is an **exclusive** cursor: event `n` itself is not repeated merely because the requested cursor equals `n`.
- `LiveOnly`: atomically establish the subscription from the current committed high-water mark, without delivering earlier durable history. Establishment and live subscription registration must not introduce a race that silently skips a newly committed fact.
- Cursor validity is tied to `SessionId` and the journal's retained range; if requested history is unavailable because of future approved retention/compaction, report an explicit **cursor/history unavailable** error. Never silently claim gap-free replay for missing history.
- Across a reconnect, delivery may be **at least once**, not exactly once. Subscribers deduplicate durable facts using `(SessionId, Sequence)` or `EventId`; sequence order must be preserved per Session.

### Broadcast and backpressure

- Subscribers receive **independent broadcast delivery**, not competing reads from a common `ChannelReader`. One listener canceling, failing, or consuming slowly MUST NOT consume another listener's notifications or block the Session Actor.
- Each subscriber has a **bounded** buffer. If a subscriber cannot keep pace with **durable facts**, the broker MUST emit an observable overflow/disconnect outcome. The client resumes with `AfterSequence(lastProcessedSequence)`, where `lastProcessedSequence` is **the last durable event it actually processed and retained**, not the highest event published by the server. Do not rely on unimplemented server acknowledgements.
- `Ephemeral Notification` (text deltas, UI progress, optional summaries) has no journal Sequence and does not automatically replay. It MAY be coalesced, throttled or dropped; a subscriber that depends on a faithful incremental view MUST be told that deltas were lost, so it can refresh from durable facts or another supported snapshot. Ephemeral notifications cannot be reclassified as committed events to hide loss.
- Resource limits, exact API shape, error DTO, whether overflow is carried as a terminal stream result or a separate signal, and ephemeral refresh behavior remain detailed contract questions. Their implementations must preserve the accepted observable semantics.

**Approved resolution C03-A:** `FromBeginning`, exclusive `AfterSequence(n)`, and `LiveOnly`; committed durable replay-to-live MUST be gap-free within the retained journal, with independent multi-subscriber broadcast and per-subscriber bounded buffering. Overflow of a slow durable subscriber causes **explicit** disconnect and cursor-based recovery, never unbounded growth, actor blockage or silent durable loss. Ephemeral loss is allowed only with appropriate observable loss signaling.

## C04 · Actor and durable-store coordination — ACCEPTED (product owner, 2026-10-20)

**Approved resolution C04-A:** the Session Actor is the **sole authority for mutable Session/Turn state and durable event sequencing**. SQLite I/O runs outside the Actor mailbox on a separately scheduled storage execution path. Storage results are returned as correlated internal messages and processed by the Actor. Successful durable commit is a **mandatory execution barrier**, but is **not itself permission to dispatch** a side-effect Tool; the Actor must revalidate current Turn/Invocation status immediately before releasing an operation. Failure or uncertainty fails closed, and a late completion must never mutate a terminated/superseded Turn.

### Ordering and responsibility

1. The Actor selects the logical transition, expected Session Version/Sequence and the immutable facts/index updates to submit. It remains the only logical state decision-maker; a Storage Executor performs I/O, version checks and atomic writes but cannot invent new Session transitions or dispatch tools.
2. SQLite Journal facts, next Sequence/version, command admission/receipt and related derived indexes belonging to a given logical transaction MUST commit atomically. A stale expected version/sequence is a visible conflict, not an occasion for the Storage Executor to silently renumber or overwrite an event.
3. SQLite I/O and lock waits MUST NOT block the mailbox's ability to process incoming `InterruptTurn` or `ResolveApproval`. Offloading I/O MUST preserve per-Session durable operation ordering; overlapping requests must not publish inconsistent transitions.
4. A storage completion notification MUST be correlated with the pending operation and **Turn/Session generation**, so an old or duplicate callback cannot change a terminated, canceled or superseded Turn. All such notifications go through Actor-controlled serialization, not a direct callback mutating shared state.
5. For a side-effect-capable ToolInvocation, the approved Policy/Approval and prepared invocation identity and parameters are fixed before intent persistence. The storage call commits a `ToolInvocationPrepared/Started` or equivalent durable pre-dispatch fact. The **corresponding commit acknowledgement MUST be confirmed** before any Executor dispatch, and the Actor MUST recheck the live Turn state, cancellation status, relevant authorization validity and invocation generation immediately before dispatch.
6. If a user interrupt/cancellation is serialized while persistence is in flight, then even a later **successful** commit cannot start a tool that has not been dispatched. The Actor records a consistent abandoned/canceled intent when feasible. Once a tool may have started, cancellation must follow the controlled Executor protocol, and uncertain external results remain `Unknown` on recovery.
7. An explicitly failed commit or uncertain commit/acknowledgement MUST NOT release a possible side effect. The Runtime reports a structured error/uncertainty and reconciles persistent state before resuming; it MUST NOT invent a successful receipt or fact. Conversely, a committed fact cannot be published to subscribers before transaction success (C03).
8. SQLite transactions MUST be short-lived; never hold an open write transaction during network/model/Shell/tool execution. Multiple Sessions may run concurrently even though SQLite serializes writers. `Microsoft.Data.Sqlite`'s asynchronous ADO.NET method names do not guarantee nonblocking disk I/O, so use a genuine isolated storage execution context as needed.

### Crash interpretation and deferred detail

Committed *intent* does not prove actual dispatch or completion. If the journal has enough trusted evidence that dispatch never happened, recovery may record an abandoned intent; where a side effect **might** have happened and no trusted terminal result exists, return `Unknown` and **never** blindly retry. The detailed pre-dispatch/dispatch event names, scheduling mechanism (dedicated worker, Channel, dispatcher), SQLite DDL, busy timeout, exact DTOs and terminal-state reconciliation API remain subject to later implementation contract review. No exactly-once guarantee for arbitrary external operations is asserted.

**Accepted invariants to test:** SQLite callback arriving after Interrupt/Dispose must not restart tools or mutate terminal state; storage failure/uncertain commit prevents dispatch; successful commit followed by cancellation before dispatch prevents execution; interleaved per-Session requests preserve committed ordering while independent Sessions remain responsive.

## C05 · Approval, Tool and failure authority — ACCEPTED (product owner, 2026-10-20)

**Approved resolution C05-A:** a runtime-mediated ToolInvocation has a **stable InvocationId** for its logical action across event correlation and recovery. A one-time Approval is bound to that specific invocation, including Session, Turn, tool identity/version, canonical arguments, resolved effective scope, requested permissions and expiration. It cannot authorize a different call or an expanded scope. After approval the Actor MUST revalidate current Turn/invocation/authorization, then apply the C04 durable-intent Commit Barrier before Executor dispatch. Duplicate, mismatched, expired or post-cancellation approvals MUST NOT start a Tool. ToolResult distinctly represents `Succeeded`, `Failed`, `Rejected`, `Cancelled` and `Unknown`. An `Unknown` external side effect is **never** blindly retried.

### Tool and approval identity

1. Distinguish provider-supplied `ToolCallId` (model response correlation), runtime-assigned stable `InvocationId` (one logical execution, including recovery), and `ApprovalId` (specific request for authorization). Do not generate a new InvocationId to circumvent an existing uncertain or rejected state.
2. Before Policy, resolve a registered tool identity and version, validate arguments against schema, and derive actual effective resource scope and requested capabilities. Plugin-supplied descriptive metadata or LLM declarations are not proof of harmlessness.
3. If Policy returns `Deny` or errors, Executor is not called. `Ask` creates a durable pending approval; only an authenticated `ResolveApproval` command correlated with that pending request can produce a decision. Explicit denial and expiration fail closed.
4. The authorization snapshot binds `SessionId`, `TurnId`, `InvocationId`, stable tool ID/version, canonical argument fingerprint, resolved resource scope, capability/permission set and expiry. **ApprovalId** correlates a particular authorization request and its decision. If any bound execution attribute changes, the approval is invalid for that invocation and fresh evaluation/approval is required.
5. A duplicate exact resolution follows C02 command idempotency; a conflicting, late, expired, cancelled or already-finalized approval can neither authorize an alternative invocation nor duplicate dispatch. Approval status transitions are serialized by the Session Actor and recorded as applicable durable facts.
6. Immediately before dispatch, recheck live Turn state, invocation generation, bound argument/scope and policy/approval validity, and require an acknowledged successful SQLite intent commit per C04. A persisted approval **alone** never authorizes immediate execution.
7. Status semantics: `Succeeded` = trustworthy completion; `Failed` = confirmed failed operation; `Rejected` = refused before dispatch; `Cancelled` = cancelled with sufficiently known effect state; `Unknown` = possible external effect without trustworthy completion/effect evidence. Process timeout, crash, disconnect or cancellation must not be mapped to `Failed` or `Cancelled` when effects remain uncertain. Recovery uses reconciliation or user decision, never blind retry.
8. **Security boundary:** mandatory checks govern SharpHarness-managed ToolInvocations. No assertion is made that arbitrary malicious same-process .NET code or unsandboxed Shell commands can be confined by the managed Policy. Default CLI Allow/Ask/Deny matrix, task-level grants, canonicalization/TOCTOU strategies, process isolation and workspace trust profiles remain **M3-specific reviews**, not silently approved by C05.

### Verification obligations

Must test distinct ID correlation; altered arguments/tool version/effective scope invalidating approval; expiry, duplicate or late `ResolveApproval`; interruption between approval, commit and dispatch; stable InvocationId after restart; and `Unknown` effects never automatically replay. Exact C# DTO names, tool fingerprint encoding, time source, effective-scope canonicalization and default permission rules are deliberately deferred to implementation-contract review.

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
| C01 | Public root & exclusive Session ownership | exclusive control per Runtime; second Open rejects SessionAlreadyOpen; independent event subscribers permitted | **Accepted (2026-10-20)** |
| C02 | Commands, receipts, persistent idempotence | stable `(SessionId, CommandId)` key, immutable Accepted/deterministic Rejected receipt incl. Busy; conflict on payload; uncertain store outcome never faked | **Accepted (2026-10-20)** |
| C03 | Durable cursor and multi-subscriber backpressure | FromBeginning / exclusive AfterSequence(n) / LiveOnly; committed facts gap-free; bounded independent broadcast; explicit overflow/reconnect | **Accepted (2026-10-20)** |
| C04 | Actor ↔ store commit ordering | Actor sole state writer; independently scheduled storage I/O; correlated commit result, revalidate live Turn before dispatch, fail-closed late callbacks | **Accepted (2026-10-20)** |
| C05 | Approval/tool authorization & failure | stable InvocationId, exact bounded Approval, Actor/C04 revalidation and distinct Succeeded/Failed/Rejected/Cancelled/Unknown results | **Accepted (2026-10-20)** |
| C06 | Session disposal behavior | Cancel + bounded cleanup; no implicit detach | **Proposed** |
| C07 | Versions & errors | structured versioned contracts; fail closed | **Proposed** |

## 2. Acceptance matrix for contract decisions (design requirements, not run tests)

| Test ID | Scenario | Expected evidence when implemented |
| --- | --- | --- |
| CT-001 | Second OpenSession same SessionId, with multiple event subscribers | returns SessionAlreadyOpen within same Runtime; no duplicate Actor, independent read-only subscriptions succeed |
| CT-002 | Duplicate Busy StartTurn after active Turn completes | original persisted Busy receipt returned on same CommandId; no Turn created |
| CT-003 | Same CommandId with different payload | DuplicateCommandConflict; original receipt unchanged, no new action |
| CT-004 | Submit acceptance response lost and caller retries | same CommandId retrieves original durable receipt after restart, exactly one command admission/Turn, without reexecution |
| CT-005 | Reconnect from AfterSequence(n) during simultaneous publish | no committed durable event gap or reordering |
| CT-006 | Slow subscriber overflows | bounded durable queue signals disconnect; client resumes from last actually processed Sequence; other subscribers/Actor unaffected |
| CT-007 | Commit delayed/failed while interrupt arrives | Actor responsive; no unauthorized side effect; coherent state |
| CT-008 | Approval replay with altered args/scope or after interrupt | Executor not started |
| CT-009 | Persist intent, tool mutates, process crashes before terminal | Unknown; no automatic repeat |
| CT-010 | Dispose during running Turn or subprocess | bounded cleanup; no false claim of stopped effects |
| CT-011 | Unsupported protocol/command version | explicit rejection; fail closed |
| CT-012 | Fake Store vs SQLite production authorization | Fake Store cannot enable production mutating tool dispatch |
| CT-013 | Deterministically rejected Busy retried across restart | original Busy receipt preserved after state changes; new intent requires new CommandId |
| CT-014 | Storage commit fails or result uncertain | no fabricated Accepted/rejected receipt; same-ID retry reconciles existing record before new decision, no side-effect dispatch on uncertain intent |
| CT-015 | Subscription modes and live boundary | FromBeginning / AfterSequence(n) / LiveOnly deliver correct ranges; concurrent commit at transition causes no gap |
| CT-016 | Unavailable retained event history | out-of-range/compacted cursor fails explicitly, no silent omission |
| CT-017 | Ephemeral delta coalescing and overflow | loss is signaled; deltas cannot masquerade as durable facts |
| CT-018 | Cancel after durable intent commit before dispatch | even when intent commit succeeds, Actor rechecks canceled Turn and Executor is never invoked |
| CT-019 | Late or duplicate storage completion | callback for terminated/superseded Turn is ignored for illegal state transitions and cannot restart a tool |
| CT-020 | SQLite conflict and slow storage interleaving | atomic Journal+Sequence+receipt/index writes or explicit conflict; Actor remains able to process interrupt/approval while storage runs |
| CT-021 | ToolCallId/InvocationId/ApprovalId correlation | stable one-logical-action InvocationId survives replay and is distinct from provider ToolCallId and specific ApprovalId |
| CT-022 | Tool identity/args/scope/expiry changed after approval | revalidation fails closed; executor cannot start with stale or mismatched approval |
| CT-023 | Tool outcome uncertain after interruption/crash | Unknown preserves possible side effects and cannot trigger automatic retry; Failed/Cancelled not substituted for uncertain effects |

**Review evidence:** deterministic fake components for M2; SQLite, subprocess and crash-injection evidence in M3; no tests have run while documents-only.

## 3. Explicitly deferred documents (do not let Coding Agent guess)

1. Final public C# API signatures, DTO placement, binary/source versioning, DI service lifetimes and serializers before corresponding M2 API implementation.
2. SQLite table/index DDL, migrations, busy timeouts, backup, local locking and file path rules before M3 persistence adapter.
3. CLI permissions/grants, workspace canonicalization, shell/subprocess and network controls before M3 Coding Tool rollout.
4. Provider wire formats, token/usage semantics, concrete UI/TUI display and native OS sandbox in their respective tasks.

## 4. Review workflow

The product owner decides remaining Proposed C06–C07 or requests changes. C01–C05 were explicitly Accepted on 2026-10-20 and synchronized with the public-contract catalog, Tool/Runtime architecture and test strategy. When all C01–C07 have been approved, synchronize the remaining documents, revise the M0 consistency report, then conduct a second static consistency audit. M0 can be signed off **only after that review**. M1/T001 implementation still needs an explicit separate owner authorization.
