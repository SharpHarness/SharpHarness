# ADR-0005: Append-Only Journal and Optional Snapshots

- **Status:** Proposed
- **Date:** 2026-10-19

## Context

A chat transcript alone cannot recover ToolStarted/Approval/Unknown states after a crash. Database-specific APIs in Core undermine embedding and portability.

## Proposal

Persist key execution facts in an ordered per-Session journal; use versioned snapshots for fast loading. Start with a file-based implementation if it meets atomicity requirements; reserve SQLite as an adapter. Any side-effect tool requires a persisted intent before execution. After interruption with ambiguous outcome, state is Unknown and must be reconciled, not blindly replayed.

## Consequences / tests

More storage discipline, but deterministic recovery semantics. Test torn writes, cursor conflicts, snapshot mismatch, process kills between side effect and completed event. No exactly-once claim for arbitrary Shell actions.

## Alternatives

Only append messages, or full event-sourcing platform/database dependency; neither is necessary for M2.
