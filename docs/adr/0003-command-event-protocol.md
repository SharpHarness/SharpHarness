# ADR-0003: Command Input and Event/Notification Output

- **Status:** Proposed
- **Date:** 2026-10-19

## Context

A simple `RunAsync(prompt)` call does not sufficiently support CLI, TUI, future RPC, mid-turn steering, approval, streaming and recovery.

## Proposal

Expose `Submit(Command) → Receipt` and `Subscribe(cursor) → Notifications`. Commands are typed/versioned and idempotently correlated. Persist important event facts with per-Session Sequence; emit high-frequency text/progress deltas as independent ephemeral notifications. Streaming subscribers receive broadcast, not shared work-queue semantics. `SubmitAsync` cancellation affects only acceptance waiting, not already accepted Turn.

## Consequences / tests

Supports UI decoupling and replayable history. Requires explicit consistency, backpressure and subscription cursor semantics. Test duplicate commands, event ordering, multiple subscribers, slow consumers and reconnect.

## Alternatives

Callback-only UI, a single unversioned JSON stream, publishing each token as a journal event; rejected for long-term evolvability or cost.
