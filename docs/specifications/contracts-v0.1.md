# Public Contract Specification v0.1 (Proposal)

**Status:** Draft; conceptual contracts, not generated source, and **not yet stable public API**. API naming/signatures must be reviewed before M2 implementation.

## 1. Type catalog

| Contract | Required semantics | Notes |
| --- | --- | --- |
| `IAgentRuntime` | Create/open Session, inject service set, honor lifecycle | C01 Accepted: exclusive controller per SessionId within one Runtime; second OpenSession returns `SessionAlreadyOpen` |
| `IAgentSession` | Stable `SessionId`; submit command; subscribe; dispose | C01 Accepted: sole control handle within Runtime; independent subscribers have no command/control ownership; one active Turn, single writer |
| `AgentCommand` | Version, stable `CommandId`, `SessionId`, typed payload | C02 Accepted: `(SessionId, CommandId)` is the dedup key; same canonical payload replays original durable receipt, different payload conflicts |
| `CommandReceipt` | Accepted/rejected, status, correlation/Turn ID if available | C02 Accepted: persist original Accepted or deterministic Rejected outcome incl. Busy; same-ID retry never reevaluates; commit uncertainty not a confirmed receipt |
| `AgentNotification` | Typed durable fact delivery or ephemeral stream item, per-subscription | C03 Accepted: independent broadcast, per-subscriber bounded buffer, explicit overflow; ephemeral deltas may coalesce/drop with loss signaling and have no durable Sequence |
| `AgentEventEnvelope` | SchemaVersion, SessionId, TurnId?, Sequence, EventId, CorrelationId?, timestamp, event | C03 Accepted: publish only committed facts; monotonic per-Session Sequence; retained journal supports gap-free replay-to-live |
| `IAgentModelClient` | Stream normalized model responses; cancellation; declared capabilities | Model provider implementation injected |
| `ModelRequest` | Immutable messages, tools, provider options, budgets | Preserve multimodal and provider extensions |
| `ModelEvent` | Text/ToolCall fragments, completion, usage/error | Incomplete ToolCall never runnable |
| `IAgentTool` | Definition + execute request → typed result | Does **not** authorize itself; runtime-mediated invocations always traverse ADR-0006 mandatory policy/approval gates |
| `ToolDefinition` | Stable tool name, schema, capabilities, version | Metadata is not proof of safety |
| `ToolInvocation` | Invocation ID, ToolCall ID, CallIndex, validated args, scope, origin | Idempotency key is stable across recovery |
| `ToolResult` | Status, normalized content, error, truncation, side-effect state | Status may be Unknown |
| `IPolicyEvaluator` | Mandatory Policy verdict Allow/Deny/Ask with explicit reason | ADR-0006 Accepted: fail closed on unknown scope, denied/failed policy or invalid approval; cannot be skipped/reordered |
| `IContextContributor` | Contribute prioritized context parts into builder | ADR-0004: typed stage-specific contract, no mutation of Session history; ordering/error policy TBD |
| `ISessionStore` | Append/replay durable facts, optimistic expected Sequence, command deduplication and optional versioned snapshots | ADR-0005: semantics storage-agnostic; default local SQLite adapter, transactional consistency required |

**ADR-0004 approved architecture constraints:** Core fixes execution phase boundaries, lifecycle and mandatory checkpoints; standard Context, Model, Tool and Event/Observability components are registered by explicit DI contracts. No universal `IMiddleware` or all-powerful Hook may wrap the Agent Loop. Telemetry observers may fail independently without hiding critical Journal failures. Public interfaces, DI lifetimes, ordering and error policies remain unapproved implementation details.

**ADR-0005 accepted storage constraints:** `SharpHarness.Core` must remain SQLite-independent. Default first production-grade local adapter is `Microsoft.Data.Sqlite` using SQLite `journal_mode=WAL` and `synchronous=FULL`. Journal is append-only durable facts; optional Snapshots are coherent derived state with applied Sequence. Journal, version counter, command deduplication and related state/index updates must have documented atomicity. External tool actions are never part of an SQLite transaction; missing trustworthy terminal facts after possible execution imply `Unknown` and no blind retry. Table structure, connection policy, busy timeout, event-retention/compaction and precise transactional API remain pending detailed contract review.

**ADR-0006 accepted security constraints:** Every runtime-mediated ToolInvocation requires registered tool resolution, argument/schema validation, effective resource scope, Policy Allow/Deny/Ask, bound Approval for Ask, successful SQLite journal intent commit before possible side effects, and controlled execution. Deny/invalid/unknown/missing approvals fail closed without Executor dispatch. Approval correlates exact Session, Turn, InvocationId, tool identity/version, canonical arguments, resolved scope, capability class and expiry; exact DTOs and scope canonicalization remain to be designed. Policy/Approval are **not** an OS Sandbox; malicious in-process code can bypass the managed runtime entirely and is outside the claimed protection boundary. CLI default permission matrix is not yet approved.

**C03 Accepted (2026-10-20):** subscribers select `FromBeginning`, exclusive `AfterSequence(n)` or `LiveOnly`. Replay-to-live delivery of retained, committed durable facts MUST be gap-free per Session; reconnect may cause duplicate delivery and clients deduplicate by `(SessionId, Sequence)` or `EventId`. Each observer receives independent broadcast with bounded buffering. On durable overrun, explicitly terminate/disconnect that subscription so the client can resubscribe using its *last actually processed* Sequence; never block the Actor or silently drop durable facts. Ephemeral deltas have no durable Sequence and may be combined/dropped with observable loss signaling. Unavailable/compacted history must yield an explicit cursor/history error, not a false replay guarantee. Specific API method signatures and error types remain Draft.

**C02 Accepted (2026-10-20):** production-capable stores MUST atomically retain a command's valid `(SessionId, CommandId)` identity, canonical typed-payload fingerprint, original Accepted or deterministically Rejected receipt (including Busy), and associated state/facts. Same ID/payload returns the original receipt without rerunning; same ID/different payload yields `DuplicateCommandConflict` without modifying original decision. Records survive restarts for the retained Session; no silent TTL-based expiry. If store failure or uncertain commit prevents confirmation, the Runtime MUST NOT fabricate a successful receipt or claim reliable rejection; retry with the same CommandId reconciles the committed result before further action. Invalid/missing command identity and unavailable storage are not assumed to produce persisted rejection records. Exact serialization, fingerprint algorithm, transaction API, and error type names remain for signature/implementation review.

**C01 Accepted (2026-10-20):** one Runtime MAY own many Sessions concurrently, but at most one controlling `IAgentSession` handle/Actor per SessionId. Another same-Runtime `OpenSession` MUST return `SessionAlreadyOpen` rather than reuse or create a competing actor. Multiple independent read-only event subscriptions are allowed and do not acquire Session control ownership. Reopening after ownership is properly released is permitted subject to C06 final disposal rules. This local-Runtime guarantee does NOT imply a working cross-process/distributed lock. Public signatures and exact observation handle types remain Draft.

## 2. Required result and state semantics

- `TurnStatus`: Queued, Running, AwaitingApproval, Completed, Failed, Interrupted; statuses terminal after Completed/Failed/Interrupted.
- `StepKind`: ContextBuild, ModelInvocation, ToolInvocation, ContextCompaction, Verification.
- `ToolResult.Status`: Succeeded, Failed, Rejected, Cancelled, Unknown. Unknown = possible side effect not reconciled.
- Model errors classified as Authentication, RateLimit, Transient, Protocol, UnsupportedCapability, Cancelled, Unknown.
- Completion reasons must distinguish natural stop, budget exceeded, user interrupted, fatal infrastructure failure.

## 3. Cancellation and threading

`SubmitAsync` cancellation only cancels the acceptance wait; after acceptance the Turn continues until completed or explicitly interrupted. `InterruptTurn` cancels running Turn. `SubscribeAsync` cancellation stops that subscriber only, not the Session or other subscribers. Durable facts are replayable when a durable store is enabled; ephemeral deltas may be lost across disconnect. `DisposeAsync` must document Drain/Cancel/Detach choice; **proposal**: default cancel active Turn with bounded cleanup and return when ownership is relinquished, not silently keep mutating tools running.

Actor owns mutable Session state; asynchronous model/tool tasks return internal messages. Each Session allows only one active Turn; a second `StartTurn` returns `Busy` rather than queueing or mutating the running Turn (ADR-0002). Client listeners cannot mutate internal state by accessing Event objects. Payloads treated as immutable snapshots.

## 4. Serialization and compatibility

- Use explicit discriminator and `SchemaVersion` for commands, facts and notifications.
- Unknown command types should fail clearly; unknown optional payload fields should be preserved or rejected by specified forward-compat policy, never silently alter security meaning.
- Time is `DateTimeOffset` UTC for machine records; public IDs opaque, not derived from paths.
- Tool name and JSON Schema are stable per invocation; argument JSON validation and effective scope resolution happen before Policy. Failed Policy or stale/invalid Approval cannot start Executor; an uncertain durable pre-execution commit also prevents tool dispatch.
- Secret fields, private prompts and internal reasoning must not leak into logs by default.

## 5. Contract review gates before coding

Decide final DTO types, JSON serializer strategy/AOT constraints, SQLite transaction boundaries/schema/constraints and verified WAL/FULL configuration, persistence consistency guarantees, event history replay API method signatures, cursor/history-unavailable error type and buffer-limit configuration (C03 semantics approved), command fingerprint serialization and storage-reconciliation algorithm (C02 behavior approved), notification backpressure, native runtime packaging, busy/backup/migration policy, precise `CommandReceipt` DTO/error taxonomy, extension metadata policy, DI registration/lifetimes, contributor ordering and stage-specific failure policies, threat model/approval binding, default CLI permission matrix, Sandbox boundary disclosure, and API versioning. Every decision must be documented before public NuGet publication.