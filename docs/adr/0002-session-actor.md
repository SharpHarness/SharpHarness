# ADR-0002: Single-Writer Session Actor

- **Status:** Accepted
- **Date:** 2026-10-19
- **Decision authority:** Product owner, approved in architecture review on 2026-10-19

## Context

Turn execution involves long model calls, tool execution, user interrupt, approval and steering. Shared mutable state with uncontrolled callbacks risks races and deadlock.

## Decision

Each Session has one logical serial writer (not an OS thread). Mailbox processes external commands and internal completion messages. AgentLoop runs as a separate asynchronous task so approvals/interrupts remain responsive. **At most one active Turn per Session.** While it is active, a different `StartTurn` is rejected immediately with an explicit `Busy` receipt; it is **not** queued or converted into `SteerTurn`. Independent Sessions can run concurrently. The existing active Turn stays unaffected by the rejected command.

The Actor owns and serializes Session/Turn state transitions. `InterruptTurn`, `SteerTurn`, and `ResolveApproval` remain processable during execution. The exact public C# type signatures remain subject to contract review.

## Consequences / tests

Deterministic state transitions and event ordering; extra coordination needed between actor and tasks. Must test: (a) second `StartTurn` returns `Busy` without creating another Turn; (b) rejected `StartTurn` does not cancel/steer the current Turn; (c) `SteerTurn` and approval remain responsive; (d) interleaved interrupt/approval and late completions after cancellation; (e) duplicate commands and parallel Sessions. The mailbox must not await the entire AgentLoop synchronously.

## Alternatives

Lock-based mutable Session, direct await of AgentLoop in actor, multi-actor distributed coordinator. Deferred until requirements justify complexity.
