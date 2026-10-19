# Task T001: Initialize the .NET solution (Coding Agent handoff)

**Status: BLOCKED pending approval of Architecture v0.1 and this task.**  
**Owner:** Product owner (approval) / Architecture-review agent (specification, test and acceptance) / Coding Agent (implementation).

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
- Public packaging/NuGet publishing, Native AOT, TUI framework, model SDK choice and external cloud credentials are out of scope.
- No automatic code formatting or sweeping README/architecture rewrites beyond task scope.
- Never create a PR containing unreviewed generated runtime stubs that pretend to implement real behavior.

## Deliverables

1. Buildable solution and project graph with a short README explaining how to build/test.
2. At least one real test demonstrating test discovery and execution.
3. CI matrix for `ubuntu-latest`, `windows-latest`, `macos-latest`, with pinned/stable actions versions and explicit .NET SDK selection.
4. PR description with command outputs, platform CI links and architecture boundary self-check.

## Acceptance / rejection

- Accept only when CI is green on 3 OS and reviewer confirms a correct dependency graph and no extra business implementation.
- Request changes if any project graph is cyclic, `Core` depends on `Coding`, test suite not discovered, or CI lacks evidence.
- If build/CI cannot run, mark the missing platform **Not Verified** and hold acceptance.

## Implementation communication

If repository already contains overlapping files, inspect and preserve them before editing. If SDK version or package decisions conflict with docs, file a question, do not invent answers. The developer may propose changes, but approval stays with owner/reviewer.