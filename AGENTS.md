# Instructions for Coding Agents

**Repository:** SharpHarness/SharpHarness  
**Phase:** M0 Architecture v0.1 formally closed and owner-accepted on 2026-10-21; **T001 and all implementation remain NOT approved**.  
**Authority:** product owner approves architecture and releases; architecture/review agent authors specifications, independently reviews and accepts; coding agents implement and fix code.

## Mandatory operating rules

1. **Read before editing:** read `docs/README.md`, applicable architecture/ADR/contract documents, and the assigned task brief. Quote the task ID and version in your PR.
2. **Do not start coding without an approved task.** A proposed architecture document or example signature is not permission to implement it.
3. **No invented APIs or behavior.** If a contract is missing or contradictory, ask for a design decision; do not silently fill in business semantics.
4. **Respect dependency boundaries:** Core depends only on Abstractions and base .NET facilities; concrete providers, storage, UI, Coding tools depend inward. Resolve dependencies at composition root.
5. **No unrelated refactoring, package additions, renaming or changes outside allowed task scope.** Propose an ADR change for architectural deviation.
6. **Security gates cannot be bypassed:** all **runtime-mediated** ToolInvocations must pass registered tool/argument/scope validation, Policy Allow/Deny/Ask, exact required Approval, and a confirmed **durable execution intent before possible side effects**, then controlled Executor. Tool metadata and LLM output are untrusted; Policy is not OS isolation from arbitrary same-process code.
7. **Use .NET/C# for official runtime components.** Avoid Node.js, Python or Rust runtime dependencies. External process calls to user tooling (git, compilers, shells) are acceptable when controlled and documented.
8. **Test, don't claim:** provide reproducible `dotnet build` / `dotnet test` evidence when relevant; explicitly list OS matrix and untested cases. Never claim unexecuted tests passed.
9. **Safe repository behavior:** no destructive Git commands, secret commits, force push, branch deletion, or bypassing review without explicit product-owner approval.
10. **Keep public contracts small and deliberate:** ADR-0001..0006 and M0 behavior decisions C01..C07 are accepted; their observable semantics are mandatory. Exact C# public API types/signatures, DTOs, package metadata and phase-specific implementation contracts remain unapproved until separately reviewed.

## Pull request contract

Each implementation PR must include: task ID; specification/ADR references; change summary; touched projects; test commands and results; security and compatibility impact; deviations/open questions; rollback notes. Reviewer dispositions are **Pass**, **Changes Required**, **Rejected**, or **Not Verified** for uncovered conditions.

## Required quality checks

- No reference direction violations; no coupling `Core` to `Coding`, `Cli`, concrete provider or persistence adapter.
- Session state has one logical writer; cancellation and approvals remain responsive while a turn runs.
- Mutating tool actions never auto-replay after uncertain failure/restart.
- Model/Tool content is treated as untrusted; logs and telemetry redact credentials and sensitive data.
- Deterministic fake-model tests cover success, tool loops, denial, cancellation and failure; CI later covers Windows/Linux/macOS.

**Source of truth:** accepted ADRs > approved versioned specifications > approved task acceptance criteria > implementation details. Discrepancies require reviewer escalation, not silent interpretation.