# SharpHarness

**A pure C# cross-platform AI Agent Harness for .NET.**

SharpHarness aims to deliver an embeddable, model-agnostic Agent Runtime and a separately composed Coding Agent. The project adopts a **thin kernel + replaceable standard runtime services** architecture. The **M0 architecture specification is formally accepted and closed** (2026-10-21). Task T001 (M1 solution skeleton/CI only) is separately approved for implementation, but no runtime feature implementation is authorized or accepted.

> 当前阶段：**M0 已关闭，T001 .NET 10 工程骨架已批准实施**。尚无通过验收的运行功能；M2/M3 未批准，具体 C# API 签名仍待后续专项评审。

## Project scope / 项目范围

- Core SDK: Agent session/turn lifecycle, command handling, event publication and model/tool loop.
- Standard services: model protocol adapters, context construction, policies, persistence and tool execution.
- Coding Agent: workspace tools, project guidance, build/test verification and a CLI/TUI shell.
- Platforms: Windows, Linux, macOS. The primary implementation language is C#; calling OS utilities or external developer tools is allowed.

## Design documents / 设计文档

Start at [docs/README.md](docs/README.md). Architecture proposals, ADRs, public contracts, an implementation brief and acceptance criteria are maintained there.

**Governance:** [AGENTS.md](AGENTS.md) specifies the role split and delivery gates: architecture/review/acceptance are separated from code implementation.

## Build and test

T001 is a .NET 10 solution skeleton. It does not implement an Agent runtime, model providers, tools, or SQLite.

Install the .NET SDK **10.0.402** feature band (`10.0.4xx`). [`global.json`](global.json) selects `10.0.402` and sets `rollForward` to `latestPatch`: a newer patch in the `10.0.4xx` band is accepted, and a different feature band is not. Prerelease SDKs are not selected.

```bash
dotnet restore SharpHarness.slnx
dotnet build SharpHarness.slnx
dotnet test SharpHarness.slnx
```

`SharpHarness.Cli` is a class-library placeholder in this task. It has no executable entrypoint. Shared `net10.0` settings live in [`Directory.Build.props`](Directory.Build.props). Source projects do not reference each other yet; allowed directions are checked by `SharpHarness.Scaffold.Tests`.

## Current status

- [x] Product direction: General Agent Core + Coding Agent.
- [x] Architecture style: Thin kernel + composable standard services.
- [x] Complete independent M0-B01 architectural documentation review (PASS).
- [x] Obtain explicit product-owner final sign-off and formally close M0 architecture v0.1 (2026-10-21).
- [x] Separately approve bootstrap implementation task T001, limited to M1 scaffold and CI (2026-10-21).
- [ ] Implement, independently test, and accept T001's first code change (not yet completed).

**License:** not yet selected. No license grant should be inferred from public visibility.