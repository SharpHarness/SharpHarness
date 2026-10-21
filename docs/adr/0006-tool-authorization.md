# ADR-0006: Mandatory Tool Authorization and Explicit Execution Boundaries

- **Status:** Accepted
- **Date:** 2026-10-20
- **Decision authority:** Product owner, accepted in M0 architecture review
- **Related:** ADR-0002 Session Actor, ADR-0003 Command/Event, ADR-0004 Stage-Specific Extensions, ADR-0005 SQLite Journal

## Context

LLM output, repository files, tool descriptions and external/MCP responses are untrusted. A Coding Agent can read sensitive files, modify workspaces, execute commands or transmit data. User-friendly confirmation prompts alone do not provide a security boundary; authorizations must be verified by the runtime independently of the model.

## Decision

1. **Single governed invocation path:** every tool invocation executed **through SharpHarness's managed tool runtime**, whether built-in, Coding Profile, adapter or MCP, MUST pass: resolve identity/registration → validate schema/arguments and derive actual execution scope → Policy (Allow / Deny / Ask) → obtain/verify precise approval when Ask → durably commit an execution intent before side effects → controlled Executor → normalize outcome and persist required facts. Extensions cannot reorder or omit mandatory gates.
2. **Fail closed:** unknown tool, invalid arguments, unresolvable scope, Policy evaluation error, missing/invalid/expired approval or unresolved security facts cannot result in execution. Denied/failed calls emit a structured, observable result, not a silently successful action.
3. **Approval binding:** approval is correlated to Session, Turn, ToolInvocationId, stable tool identity/version, canonicalized arguments or digest, resolved workspace/resource scopes, capabilities and validity interval. Mutation of approved inputs or expanded effective scope requires fresh validation and approval. A duplicate/late/stale decision cannot unlock a different invocation. Decisions enter via ADR-0003's `ResolveApproval` command and are serialized by the Session Actor.
4. **Durable intent barrier:** ADR-0005 SQLite Agent Journal is the default production store (`journal_mode=WAL`, `synchronous=FULL`). Before a tool capable of effects is dispatched, a persisted intent/started fact MUST have committed successfully. Failure or unknown commit result fails closed. Crash with possible effects and missing trusted terminal fact restores invocation as `Unknown` and MUST NOT automatically rerun it.
5. **Controlled Executor:** enforce request cancellation, execution time budget, bounded outputs, controlled working directory/environment, explicit process-tree termination best-effort guarantees and structured tool outcomes. Classify risky capabilities (workspace read/write, outside-workspace read/write, shell, network, credentials, Git destructive operations) independently of untrusted tool metadata.
6. **Sandbox is separate:** Policy and approval are decisions made by SharpHarness, **not** OS process isolation. A shell launched with user privileges may escape a workspace working-directory restriction. First version does **not** require a full native OS sandbox and MUST disclose actual permissions and limitations; platform-specific sandbox work is a separate milestone.
7. **In-process trust limitation:** a third-party .NET plugin loaded into the same privileged process can call OS APIs directly outside the managed Tool Pipeline. The single path governs **calls mediated by the SharpHarness runtime**, and is not a security boundary against arbitrary malicious in-process code. Untrusted extensions require process/OS isolation or must not be loaded; no misleading "plugin-proof sandbox" claims.
8. **Default policy vs mechanism:** this ADR approves mandatory authorization mechanism, not the CLI's complete default Allow/Ask/Deny matrix. Default grants, task-level approvals, trust classifications and security prompts must be specified and reviewed before Coding Agent features ship.

## Consequences and rejected alternatives

Benefits: stable security checkpoints, auditability, explicit approval semantics and recoverable execution state. Costs: more controlled execution orchestration and review burden. Reject "the LLM decides whether to request permission" and "each tool implements its own authorization" because both allow inconsistent or bypassed checks. Reject a universal middleware or unrestricted hook that could skip safety gates.

## Required acceptance tests

- Policy Allow / Deny / Ask; Deny or Policy failure does not invoke Executor.
- Approval rejection, duplicate, replay, expiry, mismatched scope or args, and approvals that arrive after cancellation do not start the invocation.
- SQLite intent commit barrier: failure/unknown commit prevents mutating dispatch; crash after possible effect yields Unknown without automatic retry.
- Untrusted Tool/MCP/repository output cannot directly alter Policy or grant itself capabilities through runtime APIs.
- Path traversal/symlink escape, workspace scope, secret access, subprocess timeouts and bounded output.
- Explicitly verify actual security claims. Without an OS sandbox, do not assert a malicious subprocess or malicious in-process plugin can be confined by Policy alone.

**Remaining detailed contracts:** threat model and trusted adapter registration, scope canonicalization/TOCTOU strategy, durable approval schema and expiry, risk categories and CLI defaults, executor process isolation configuration, cancellation grace periods, output redaction and failure taxonomy must be reviewed before corresponding feature implementation.
