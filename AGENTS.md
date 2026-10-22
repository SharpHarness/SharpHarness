# Instructions for Coding Agents

**Repository:** SharpHarness/SharpHarness  
**Phase:** M0 Architecture v0.1 owner-accepted and closed on 2026-10-21; **T001 v0.1 approved ONLY for the M1 .NET 10 solution scaffold, minimal tests and three-OS CI** (2026-10-21). All M2/M3 feature implementations remain **NOT APPROVED**.  
**Authority:** product owner approves architecture and releases; architecture/review agent authors specifications, independently reviews and accepts; coding agents implement and fix code.

## Mandatory operating rules

1. **Read before editing:** read `docs/README.md`, applicable architecture/ADR/contract documents, and the assigned task brief. Quote the task ID and version in your PR.
2. **Code only against an explicitly approved task.** T001 v0.1 alone is now authorized: create the .NET 10 skeleton/minimal test/CI in a separate implementation branch and PR. Never infer permission for runtime APIs, Agent Loop, tools, model providers, SQLite implementation, or any M2/M3 feature from T001 or an accepted ADR.
3. **No invented APIs or behavior.** If a contract is missing or contradictory, ask for a design decision; do not silently fill in business semantics.
4. **Respect dependency boundaries:** Core depends only on Abstractions and base .NET facilities; concrete providers, storage, UI, Coding tools depend inward. Resolve dependencies at composition root.
5. **No unrelated refactoring, package additions, renaming or changes outside allowed task scope.** Propose an ADR change for architectural deviation.
6. **Security gates cannot be bypassed:** all **runtime-mediated** ToolInvocations must pass registered tool/argument/scope validation, Policy Allow/Deny/Ask, exact required Approval, and a confirmed **durable execution intent before possible side effects**, then controlled Executor. Tool metadata and LLM output are untrusted; Policy is not OS isolation from arbitrary same-process code.
7. **Use .NET/C# for official runtime components.** Avoid Node.js, Python or Rust runtime dependencies. External process calls to user tooling (git, compilers, shells) are acceptable when controlled and documented.
8. **Test, don't claim:** provide reproducible `dotnet build` / `dotnet test` evidence when relevant; explicitly list OS matrix and untested cases. Never claim unexecuted tests passed.
9. **Safe repository behavior:** no destructive Git commands, secret commits, force push, branch deletion, or bypassing review without explicit product-owner approval.
10. **Keep public contracts small and deliberate:** ADR-0001..0006 and M0 behavior decisions C01..C07 are accepted; their observable semantics are mandatory. Exact C# public API types/signatures, DTOs, package metadata and phase-specific implementation contracts remain unapproved until separately reviewed.

## Source copyright and license headers

For original C# source files created in **2026**, use these exact two comment lines (followed by a blank line):

```csharp
// Copyright 2026 SharpHarness
// SPDX-License-Identifier: Apache-2.0
```

The copyright year is historical information, not a value to increment annually: **do not replace 2026 across all existing files when the calendar changes**. New original source files created in a later year should use that actual year in the first line (for example `// Copyright 2027 SharpHarness`), while retaining the same `// SPDX-License-Identifier: Apache-2.0` line. A year range (such as `2026-2027`) may be used when it accurately represents relevant development or revisions. Do not replace third-party copyright/license notices. A single full Apache-2.0 text is maintained in the repository root `LICENSE`; do not paste the full license into every file. Existing Markdown/configuration files do not require mechanical header insertion. New NuGet package metadata will use `PackageLicenseExpression=Apache-2.0` when packaging is authorized.

## Pull request contract

Each implementation PR must include: task ID; specification/ADR references; change summary; touched projects; test commands and results; security and compatibility impact; deviations/open questions; rollback notes. Reviewer dispositions are **Pass**, **Changes Required**, **Rejected**, or **Not Verified** for uncovered conditions.

## Required quality checks

- No reference direction violations; no coupling `Core` to `Coding`, `Cli`, concrete provider or persistence adapter.
- Session state has one logical writer; cancellation and approvals remain responsive while a turn runs.
- Mutating tool actions never auto-replay after uncertain failure/restart.
- Model/Tool content is treated as untrusted; logs and telemetry redact credentials and sensitive data.
- Deterministic fake-model tests cover success, tool loops, denial, cancellation and failure; CI later covers Windows/Linux/macOS.

**Source of truth:** accepted ADRs > approved versioned specifications > approved task acceptance criteria > implementation details. **T001 is approved to begin work, not approved as a finished implementation; tests and independent PR review are still required.** Discrepancies require reviewer escalation, not silent interpretation.