# ADR-0002: Single-Writer Session Actor

- **Status:** Proposed
- **Date:** 2026-10-19

## Context

Turn execution involves long model calls, tool execution, user interrupt, approval and steering. Shared mutable state with uncontrolled callbacks risks races and deadlock.

## Proposal

Each Session has one logical serial writer (not an OS thread). Mailbox processes external commands and internal completion messages. AgentLoop runs as a separate asynchronous task so approvals/interrupts remain responsive. At most one active Turn per Session; first release returns Busy for a second StartTurn, while independent Sessions can run concurrently.

## Consequences / tests

Deterministic state transitions and event ordering; extra coordination needed between actor and tasks. Must test interleaved interrupt/approval, late completions after cancellation, duplicate commands and parallel Sessions.

## Alternatives

Lock-based mutable Session, direct await of AgentLoop in actor, multi-actor distributed coordinator. Deferred until requirements justify complexity.
