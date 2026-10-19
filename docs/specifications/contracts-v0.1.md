# Public Contract Specification v0.1 (Proposal)

**Status:** Draft; conceptual contracts, not generated source, and **not yet stable public API**. API naming/signatures must be reviewed before M2 implementation.

## 1. Type catalog

| Contract | Required semantics | Notes |
| --- | --- | --- |
| `IAgentRuntime` | Create/open Session, inject service set, honor lifecycle | Does not dictate UI or provider |
| `IAgentSession` | Stable `SessionId`; submit command; subscribe; dispose | One active Turn, single state writer |
| `AgentCommand` | Version, stable `CommandId`, `SessionId`, payload | Must detect duplicate submissions (ADR-0003); exact retention and conflict policy pending |
| `CommandReceipt` | Accepted/rejected, status, correlation/Turn ID if available | Acknowledges submission, NOT completion; `StartTurn` while active MUST reject with `Busy` (ADR-0002/0003) |
| `AgentNotification` | Typed durable fact delivery or ephemeral stream item, per-subscription | Broadcast semantics, no shared competing readers; ephemeral replay not guaranteed (ADR-0003) |
| `AgentEventEnvelope` | SchemaVersion, SessionId, TurnId?, Sequence, EventId, CorrelationId?, timestamp, event | Durable fact with monotonic per-Session Sequence, supports cursor replay with durable store (ADR-0003) |
| `IAgentModelClient` | Stream normalized model responses; cancellation; declared capabilities | Model provider implementation injected |
| `ModelRequest` | Immutable messages, tools, provider options, budgets | Preserve multimodal and provider extensions |
| `ModelEvent` | Text/ToolCall fragments, completion, usage/error | Incomplete ToolCall never runnable |
| `IAgentTool` | Definition + execute request → typed result | Does **not** authorize itself |
| `ToolDefinition` | Stable tool name, schema, capabilities, version | Metadata is not proof of safety |
| `ToolInvocation` | Invocation ID, ToolCall ID, CallIndex, validated args, scope, origin | Idempotency key is stable across recovery |
| `ToolResult` | Status, normalized content, error, truncation, side-effect state | Status may be Unknown |
| `IPolicyEvaluator` | Policy verdict Allow/Deny/Ask + reason | ADR-0004: mandatory Tool-stage checkpoint, cannot be omitted/reordered; precise authorization remains ADR-0006 |
| `IContextContributor` | Contribute prioritized context parts into builder | ADR-0004: typed stage-specific contract, no mutation of Session history; ordering/error policy TBD |
| `ISessionStore` | Append/replay durable facts, optimistic expected Sequence, command deduplication and optional versioned snapshots | ADR-0005: semantics storage-agnostic; default local SQLite adapter, transactional consistency required |

**ADR-0004 approved architecture constraints:** Core fixes execution phase boundaries, lifecycle and mandatory checkpoints; standard Context, Model, Tool and Event/Observability components are registered by explicit DI contracts. No universal `IMiddleware` or all-powerful Hook may wrap the Agent Loop. Telemetry observers may fail independently without hiding critical Journal failures. Public interfaces, DI lifetimes, ordering and error policies remain unapproved implementation details.

**ADR-0005 accepted storage constraints:** `SharpHarness.Core` must remain SQLite-independent. Default first production-grade local adapter is `Microsoft.Data.Sqlite` using SQLite `journal_mode=WAL` and `synchronous=FULL`. Journal is append-only durable facts; optional Snapshots are coherent derived state with applied Sequence. Journal, version counter, command deduplication and related state/index updates must have documented atomicity. External tool actions are never part of an SQLite transaction; missing trustworthy terminal facts after possible execution imply `Unknown` and no blind retry. Table structure, connection policy, busy timeout, event-retention/compaction and precise transactional API remain pending detailed contract review.

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
- Tool name and JSON Schema are stable per invocation; argument JSON validation happens before Policy.
- Secret fields, private prompts and internal reasoning must not leak into logs by default.

## 5. Contract review gates before coding

Decide final DTO types, JSON serializer strategy/AOT constraints, SQLite transaction boundaries/schema/constraints and verified WAL/FULL configuration, persistence consistency guarantees, event history replay API/cursor shape, duplicate CommandId retention and payload-conflict behavior, notification backpressure, native runtime packaging, busy/backup/migration policy, `CommandReceipt` error taxonomy, extension metadata policy, DI registration/lifetimes, contributor ordering and stage-specific failure policies, and API versioning. Every decision must be documented before public NuGet publication.