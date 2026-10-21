# T001 Implementation Readiness Review · v0.1

- **Status:** **REVIEW COMPLETE; T001 APPROVED FOR IMPLEMENTATION BY PRODUCT OWNER (2026-10-21)**. Work/results remain unverified and require independent acceptance.
- **Review date:** 2026-10-21
- **Task:** [T001 · Initialize the .NET solution](../tasks/T001-bootstrap.md)
- **Authority:** M0 architecture v0.1 formally Closed/Accepted by product owner on 2026-10-21; T001 separately approved for strictly scoped implementation on 2026-10-21, **not yet accepted as completed work**.
- **Scope of this review:** proposed implementation scope, allowed files, deliverables, dependency restrictions, CI and acceptance criteria only. **No code, package installation, workflow or implementation PR is created by this review.**

## 1. Reviewer verdict and rationale

**Product-owner task approval received (2026-10-21)** for the scoped T001 skeleton and accompanying implementation constraints below. The original T001 is appropriately restricted to .NET 10 project scaffolding and CI, compatible with accepted ADR-0001..0006 and C01..C07. It must not turn into a premature implementation of the Agent Core or SQLite.

A green M0 design report is not a green runtime or CI. T001 acceptance remains **Not Verified** until a Coding Agent implements an *approved* T001, produces its own PR, and shows actual passing three-OS CI and reviewer-checked project boundaries.

## 2. Proposed T001 implementation scope

**Allowed once and only once T001 is separately approved:**

- `.NET 10` solution `SharpHarness.slnx`, seven minimal SDK-style project files under `src/`: Abstractions, Core, Models, Tools, Infrastructure, Coding, Cli.
- Minimal test project(s) under `tests/`, using a documented test framework and test adapter/runtime packages only as needed to run deterministic local tests.
- Minimal repository-wide build/format settings: `global.json` when exact SDK feature-band/version and roll-forward policy have been decided; `Directory.Build.props`, optional `Directory.Packages.props`, `.editorconfig`, `.gitignore` if necessary.
- `.github/workflows/ci.yml` with Linux, Windows, macOS jobs; restore/build/test and uploaded/presented run evidence.
- Only a short build/test instruction amendment to README, plus PR handoff text describing branch/base, dependency graph and test proof. No unrelated documentation rewrite.

**Explicit exclusions:** public `IAgentRuntime`/`IAgentSession` or fake API implementations, Agent Loop, Session Actor, Command/Event runtime, policy and approval logic, real tool executor, model adapters, provider HTTP/credentials, SQLite DDL / `Microsoft.Data.Sqlite`, MCP, coding workspace tools, TUI framework, NuGet publish, native AOT, network service, OS sandbox, generated business stubs. Even a "not implemented" class mimicking a public API is not necessary for a project-only build.

**CLI project clarification:** It may be a compilable project placeholder without a functional executable entrypoint in T001. If a no-op CLI executable is desired, it must be explicitly authorized as a bounded exception; never represent it as a working Agent.

## 3. Proposed dependency and build acceptance

| Project | Allowed minimal project-reference direction |
| --- | --- |
| `SharpHarness.Abstractions` | No references to other SharpHarness projects |
| `SharpHarness.Core` | May reference `Abstractions` only |
| `SharpHarness.Models`, `SharpHarness.Tools`, `SharpHarness.Infrastructure` | May reference `Abstractions`; no `Core` business implementation coupling |
| `SharpHarness.Coding` | May depend inward on `Abstractions`/`Core`/`Tools` only when useful; no provider/storage implementation required by empty scaffold |
| `SharpHarness.Cli` | Composition root may ultimately depend on other projects; **no needless references needed to make empty projects compile** |
| `tests/*` | Depend only on projects under test and selected test framework |

The exact reference graph may be a minimal subset of allowed edges: **don't add a dependency just to demonstrate a possible future connection**. Reject any cycles, `Core → Coding/Models/Infrastructure/Cli`, provider/SQLite/native runtime packages, or functionality not required by T001.

Proposed minimal target: `TargetFramework=net10.0` on all platform-neutral projects; consistent SDK selection and `Nullable`/language settings as justified. Do not introduce Windows-only target frameworks that make the three-OS skeleton unbuildable.

## 4. Three implementation clarifications included in approved T001 handoff

**T001-D01 · Final branch/base strategy after PR #1 merge (approved 2026-10-21)**

The product owner separately authorized [merging architecture PR #1](https://github.com/SharpHarness/SharpHarness/pull/1), which is now **Merged** into `main` at `7534fb60536b3b0e07e0b9ffeae06c68a63843b9`. The earlier recommendation to build atop a not-yet-merged architecture branch has been superseded. Use the existing `feat/t001-dotnet10-bootstrap` implementation branch, which has been fast-forwarded to the merged `main` without force or lost commits. Coding Agent works only there and opens a **separate T001 PR against `main`**. Never commit implementation straight to `main` or merge the new implementation PR without its own T001-A01..A08 review and owner authorization. The approved M0 specification is now available directly on `main`.

**T001-D02 · .NET 10 SDK/test-framework selection (delegated bounded implementation choice)**

Pin an explicit supported .NET 10 SDK release/feature band **at task execution** using `global.json` where applicable, and document `rollForward`. Avoid speculative version numbers in this architecture report. Pick one mainstream deterministic C# test framework (e.g. MSTest or xUnit) with compatible versions; no runtime Agent dependency. If the owner delegates exact patch/test-framework selection, the Coding Agent must record the choice, references and evidence in the T001 PR. Do not silently add unrelated analyzers, provider SDKs or storage packages.

**T001-D03 · Measurable three-platform acceptance (mandatory)**

Acceptance requires a single workflow commit/job matrix with `ubuntu-latest`, `windows-latest`, `macos-latest`; actual successful restore/build/test; a non-vacuous assertion (prefer a structural boundary rule or build artifact check, not just `Assert.True(true)`); test discovery visibly identifies the test; explicit SDK version/log and dependency graph summary. Use least-privilege `GITHUB_TOKEN` permissions for CI. Stable/pinned GitHub actions versions are required; avoid unreviewed script downloads or external credentials. A matrix entry with a skipped/failed test is **not** a PASS.

## 5. Proposed deliverable and acceptance matrix

| ID | Deliverable / acceptance check | Evidence required | Reviewer rule |
| --- | --- | --- | --- |
| T001-A01 | `SharpHarness.slnx` includes seven intended SDK-style projects and test project(s) | Solution/project list and `dotnet build` logs | Reject missing/extra business projects or invalid project files |
| T001-A02 | No forbidden dependency edges/cycles | Documented direct ProjectReference graph; inspection of all `.csproj` | Reject Core dependencies pointing outward |
| T001-A03 | No behavior or public API implementation leaked into skeleton | Git diff; source/package inventories | Reject Agent Loop, SQLite/Tool logic or fake public runtime stubs |
| T001-A04 | Real, deterministic test discovery and assertion | Test source, `dotnet test` output showing discovered and passed tests | Reject empty project/zero-test green CI |
| T001-A05 | Windows, Linux, macOS CI matrix executes and succeeds | Three platform GitHub Actions job links with identical commit SHA | Mark any absent platform Not Verified; **do not accept** until all three pass |
| T001-A06 | Reproducible SDK/build settings and readable instructions | Selected SDK and roll-forward rule, build/test commands, minimal README instructions | Reject unexplained version drift or unsupported target |
| T001-A07 | Scoped implementation PR | PR title/body references T001 and architecture sign-off, permitted file list, rollback, assumptions and CI proof | Reject unrelated refactoring or implicit merge of PR #1 |
| T001-A08 | Safety and secrets | Inspect CI permissions, scripts and lock/ignore files, ensure no secrets committed | Reject unnecessary privileged CI or new external credentials |

### Reviewer execution when an implementation PR exists

Review the diff against the T001 allowlist, project dependency graph and full CI run, then reproduce `dotnet restore`, `dotnet build`, and `dotnet test` as feasible. **PASS only** after all required observed proof. Otherwise choose `Changes Required` or `Not Verified` with the exact missing evidence.

## 6. Scope-change discipline

- Tasks beyond scaffold must use their own design approval, implementation task ID and PR; no tacit advancement to M2.
- The developer can propose changes, but only the owner may approve widening T001, merging the architecture PR, or launching implementation.
- Branch protection/access policies and hosted CI availability may affect the branch strategy. If the preferred target branch cannot be used, stop and request an explicit alternate plan rather than targeting `main` silently.
- A CI matrix tests OS images/runners, not every CPU architecture; `macos-latest` alone does not prove both Intel and Apple Silicon.
- README can be edited minimally for build instructions **only when T001 is actually authorized**. No user-facing claim that SharpHarness SDK is functional at M1.

## 7. Owner implementation authorization (RECORDED 2026-10-21)

**Authorized scope for Coding Agent handoff:**

> Approve T001 to create only the .NET 10 solution skeleton, seven minimal project files, deterministic test infrastructure and three-OS GitHub Actions CI, using the merged M0 baseline on `main` as the base for `feat/t001-dotnet10-bootstrap`, with a separate T001 PR targeting `main`. Permit an explicitly documented SDK/test-framework selection and minimal build README update. Forbid implementation of public Agent runtime APIs, SQLite and Coding tools. Require T001-A01..A08 acceptance evidence and independent review; PR #1 was separately approved and merged; do not merge any implementation PR without separate review and authorization.

**Current state:** T001 is **APPROVED FOR IMPLEMENTATION** only. No code/test/CI is claimed to have been completed or accepted by the task approval; separate PR, code review and T001-A01..A08 verification remain required. PR #1 has been merged into `main`; T001 implementation itself remains unmerged and unverified.
