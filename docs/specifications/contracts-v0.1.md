# Public Contract Specification v0.1 (Proposal)

**Status:** Draft; conceptual contracts, not generated source, and **not yet stable public API**. API naming/signatures must be reviewed before M2 implementation.

## 1. Type catalog

| Contract | Required semantics | Notes |
| --- | --- | --- |
| `IAgentRuntime` | Create/open Session, inject service set, honor lifecycle | Does not dictate UI or provider |
| `IAgentSession` | Stable `SessionId`; submit command; subscribe; dispose | One active Turn, single state writer |
| `AgentCommand` | Version, `CommandId`, `SessionId`, payload | `StartTurn`, `SteerTurn`, `InterruptTurn`, `ResolveApproval` |
| `CommandReceipt` | Accepted/rejected, status, correlation/Turn ID if available | No implication of Turn completion |
| `AgentNotification` | Typed durable event or ephemeral stream item | Do not confuse with journal fact |
| `AgentEventEnvelope` | SchemaVersion, SessionId, TurnId?, Sequence, EventId, CorrelationId?, timestamp, event | Sequence monotonic per Session |
| `IAgentModelClient` | Stream normalized model responses; cancellation; declared capabilities | Model provider implementation injected |
| `ModelRequest` | Immutable messages, tools, provider options, budgets | Preserve multimodal and provider extensions |
| `ModelEvent` | Text/ToolCall fragments, completion, usage/error | Incomplete ToolCall never runnable |
| `IAgentTool` | Definition + execute request → typed result | Does **not** authorize itself |
| `ToolDefinition` | Stable tool name, schema, capabilities, version | Metadata is not proof of safety |
| `ToolInvocation` | Invocation ID, ToolCall ID, CallIndex, validated args, scope, origin | Idempotency key is stable across recovery |
| `ToolResult` | Status, normalized content, error, truncation, side-effect state | Status may be Unknown |
| `IPolicyEvaluator` | Policy verdict Allow/Deny/Ask + reason | Cannot be omitted from path |
| `IContextContributor` | Contribute prioritized context parts into builder | No mutation of Session history |
| `ISessionStore` | Append/read durable facts, expected Sequence and snapshot | Append atomicity to be explicitly tested |

## 2. Required result and state semantics

- `TurnStatus`: Queued, Running, AwaitingApproval, Completed, Failed, Interrupted; statuses terminal after Completed/Failed/Interrupted.
- `StepKind`: ContextBuild, ModelInvocation, ToolInvocation, ContextCompaction, Verification.
- `ToolResult.Status`: Succeeded, Failed, Rejected, Cancelled, Unknown. Unknown = possible side effect not reconciled.
- Model errors classified as Authentication, RateLimit, Transient, Protocol, UnsupportedCapability, Cancelled, Unknown.
- Completion reasons must distinguish natural stop, budget exceeded, user interrupted, fatal infrastructure failure.

## 3. Cancellation and threading

`SubmitAsync` cancellation only cancels the acceptance wait. `InterruptTurn` cancels running Turn. `SubscribeAsync` cancellation stops that subscriber, not the Session. `DisposeAsync` must document Drain/Cancel/Detach choice; **proposal**: default cancel active Turn with bounded cleanup and return when ownership is relinquished, not silently keep mutating tools running.

Actor owns mutable Session state; asynchronous model/tool tasks return internal messages. Client listeners cannot mutate internal state by accessing Event objects. Payloads treated as immutable snapshots.

## 4. Serialization and compatibility

- Use explicit discriminator and `SchemaVersion` for commands, facts and notifications.
- Unknown command types should fail clearly; unknown optional payload fields should be preserved or rejected by specified forward-compat policy, never silently alter security meaning.
- Time is `DateTimeOffset` UTC for machine records; public IDs opaque, not derived from paths.
- Tool name and JSON Schema are stable per invocation; argument JSON validation happens before Policy.
- Secret fields, private prompts and internal reasoning must not leak into logs by default.

## 5. Contract review gates before coding

Decide final DTO types, JSON serializer strategy/AOT constraints, persistence consistency guarantees, event history replay API, `CommandReceipt` error taxonomy, extension metadata policy, and API versioning. Every decision must be documented before public NuGet publication.