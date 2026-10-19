# ADR-0006: Mandatory Authorization Before Tool Execution

- **Status:** Proposed
- **Date:** 2026-10-19

## Context

Model output and plugins are untrusted. Tool description metadata cannot ensure a subprocess does not mutate disk or send network traffic.

## Proposal

Require every call to pass lookup/schema validation, policy evaluation, approval if required, and controlled execution. Approval tied to exact invocation ID, arguments/scope and expiration. No generic Hook can bypass policy. OS sandboxing is independent of policy and must not be claimed until implemented and tested.

## Consequences / tests

Safer defaults and explicit trust boundary, more complexity in scheduling and user approval. Verify refusal does not call Executor, late/duplicate approval cannot authorize modified calls, symlink/path traversal and malicious tool outputs do not grant permission.

## Alternatives

LLM decides when to ask or each tool implements its own authorization: rejected due to inconsistent, bypassable security.
