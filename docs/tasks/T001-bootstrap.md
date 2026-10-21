# Task T001: Initialize the .NET solution (Coding Agent handoff)

**Status: APPROVED FOR IMPLEMENTATION (2026-10-21, product owner).** M0 architecture v0.1 was formally accepted and closed on 2026-10-21. T001 is **authorized as a strictly scoped M1 .NET 10 scaffold task only**; coding is **not yet complete or accepted**. See [T001 readiness review](../review/t001-readiness-review.md).  
**Owner:** Product owner (approval) / Architecture-review agent (specification, test and acceptance) / Coding Agent (implementation).

## Approved implementation handoff constraints (2026-10-21)

- **Task version:** T001 v0.1, approved by product owner. Authority extends **only** to the allowed files/scaffold scope and acceptance checks in this brief and the [readiness review](../review/t001-readiness-review.md).
- **Branching (T001-D01):** Create/use a separate T001 implementation branch based on the signed-off `docs/architecture-v0.1` branch. Implement via its own T001 PR targeting the architecture branch while PR #1 remains Open. Do **not** implement directly on `main`, merge PR #1, or change the M0 baseline as part of T001.
- **SDK/test framework (T001-D02):** Coding Agent may choose a supported .NET 10 SDK feature band/patch plus documented `global.json` rollForward and one mainstream compatible C# unit test framework. Record exact versions and rationale in the T001 PR; avoid unrelated dependencies.
- **CI acceptance (T001-D03):** Execute restore/build/test on `ubuntu-latest`, `windows-latest` and `macos-latest` at the same commit. Show genuine discovered-and-passing deterministic test(s), not zero-tests success. Provide complete CI job links.
- **Review checklist:** satisfy T001-A01 through T001-A08 in the readiness review. This approval authorizes **coding work only**, not passing acceptance or merging any PR.

## Objective

Create a clean .NET 10 Monorepo that builds and tests on Windows, Linux and macOS. **No Agent Loop, model calls, tool executor, business code or public contracts beyond what is necessary to compile the skeleton.**

## Allowed changes

- `SharpHarness.slnx`, `global.json` (if SDK pin agreed), `Directory.Build.props`, `Directory.Packages.props` (only if needed), `.editorconfig`, `.gitignore`.
- `src/SharpHarness.Abstractions`, `src/SharpHarness.Core`, `src/SharpHarness.Models`, `src/SharpHarness.Tools`, `src/SharpHarness.Infrastructure`, `src/SharpHarness.Coding`, `src/SharpHarness.Cli` with minimal SDK-style project files and no business behavior.
- `tests/*` with minimal deterministic test project(s).
- `.github/workflows/ci.yml` with OS matrix and restore/build/test.

## Constraints

- Use C# and .NET 10, central package/version management only when demonstrably useful.
- Dependency rules per `docs/architecture/module-boundaries.md`. Core must **not** reference concrete implementation projects.
- Public packaging/NuGet publishing, Native AOT, TUI framework, model SDK choice, SQLite connection/schema implementation and external cloud credentials are out of scope. The approved SQLite persistence engine belongs to a later task; **do not add `Microsoft.Data.Sqlite` or native packages solely for the skeleton** unless separately approved.
- No automatic code formatting or sweeping README/architecture rewrites beyond task scope.
- Never create a PR containing unreviewed generated runtime stubs that pretend to implement real behavior. ADR-0005 requires `ISessionStore` to remain a Core-facing abstraction; T001 must not smuggle storage implementation into Core.

## Deliverables

1. Buildable solution and project graph with a short README explaining how to build/test.
2. At least one real test demonstrating test discovery and execution.
3. CI matrix for `ubuntu-latest`, `windows-latest`, `macos-latest`, with pinned/stable actions versions and explicit .NET SDK selection.
4. PR description with command outputs, platform CI links and architecture boundary self-check.

## Acceptance / rejection

The independent reviewer MUST validate all eight T001-A01–T001-A08 checks. Local `dotnet restore` / `dotnet build` / `dotnet test` results and all three OS CI jobs are **Not Verified until actually run**. An approved task is not a passed implementation.

- Accept only when CI is green on 3 OS and reviewer confirms a correct dependency graph and no extra business implementation.
- Request changes if any project graph is cyclic, `Core` depends on `Coding`, test suite not discovered, or CI lacks evidence.
- If build/CI cannot run, mark the missing platform **Not Verified** and hold acceptance.

## Implementation communication

If repository already contains overlapping files, inspect and preserve them before editing. If SDK version or package decisions conflict with docs, file a question, do not invent answers. The developer may propose changes, but approval stays with owner/reviewer.