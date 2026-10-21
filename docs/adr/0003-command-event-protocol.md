# ADR-0003: Command Input and Event/Notification Output

- **Status:** Accepted
- **Date:** 2026-10-19
- **Decision authority:** Product owner, approved in architecture review on 2026-10-19

## Context

A simple `RunAsync(prompt)` call does not sufficiently support CLI, TUI, future RPC, mid-turn steering, approval, streaming and recovery.

## Decision

Expose **Command input and Event/Notification output**: `Submit(Command) → Receipt` and `Subscribe(cursor) → Notifications`. Commands are typed, versioned, carry stable `CommandId` values, and require duplicate-command detection. A receipt confirms acceptance or explicit rejection, **not** Turn completion. Per ADR-0002, a new `StartTurn` while a Turn is active is rejected with `Busy` and does not affect that Turn.

Separate output into **Durable facts** (e.g. `TurnStarted`, `ToolInvocationPrepared`, `ApprovalResolved`, `ToolCompleted`, `TurnCompleted`) and **Ephemeral notifications** (e.g. text deltas, progress). Durable facts have a monotonically increasing per-Session `Sequence`, support persistence and replay by cursor when a durable store is configured, and are not silently dropped. Ephemeral notifications are streamed without a guarantee of persistence or replay and must not masquerade as sequenced journal facts.

Provide **broadcast subscriptions** with independent subscriber delivery, not multiple consumers competing for one queue. Each subscriber has its own cancellation and backpressure behavior; a slow observer must not consume another observer's messages. `SubmitAsync` cancellation only cancels submission/acceptance waiting, and does **not** implicitly interrupt an accepted Turn; use `InterruptTurn` to cancel execution. An explicit acknowledgement that the command was accepted is distinct from final execution completion.

**Contract decisions approved:** C02 specifies CommandId deduplication, retained Accepted/deterministic Rejected receipts, payload conflicts and crash-safe retry boundary; C03 specifies durable cursors, replay/broadcast/backpressure; C04 specifies durable write acknowledgement and the Actor's commit barrier; C07 specifies version/error behavior. **Remaining implementation contracts:** exact public DTO signatures, fingerprint/serialization algorithm, buffer size/configuration, SQLite DDL/locking and detailed service lifetimes require separate review before their M2/M3 implementation; Coding Agents must not invent them.

## Consequences / tests

Supports UI decoupling and replayable history. Requires explicit consistency, backpressure and subscription cursor semantics. Test duplicate commands (including conflicting payloads), correct receipt semantics, per-Session event ordering, multiple subscribers, slow consumers, cancellation of one subscriber and reconnect/replay. Integration with journal durability is reviewed under ADR-0005.

## Alternatives

Callback-only UI, a single unversioned JSON stream, publishing each token as a journal event; rejected for long-term evolvability or cost.
