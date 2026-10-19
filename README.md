# SharpHarness

**A pure C# cross-platform AI Agent Harness for .NET.**

SharpHarness aims to deliver an embeddable, model-agnostic Agent Runtime and a separately composed Coding Agent. The project adopts a **thin kernel + replaceable standard runtime services** architecture. The repository is currently in the **architecture specification phase**: no runtime implementation is approved yet.

> 当前阶段：架构设计与评审。本文档集是实现工作的输入，不代表已有功能；在方案评审通过前，不得将设计草案视为稳定的公共 API。

## Project scope / 项目范围

- Core SDK: Agent session/turn lifecycle, command handling, event publication and model/tool loop.
- Standard services: model protocol adapters, context construction, policies, persistence and tool execution.
- Coding Agent: workspace tools, project guidance, build/test verification and a CLI/TUI shell.
- Platforms: Windows, Linux, macOS. The primary implementation language is C#; calling OS utilities or external developer tools is allowed.

## Design documents / 设计文档

Start at [docs/README.md](docs/README.md). Architecture proposals, ADRs, public contracts, an implementation brief and acceptance criteria are maintained there.

**Governance:** [AGENTS.md](AGENTS.md) specifies the role split and delivery gates: architecture/review/acceptance are separated from code implementation.

## Current status

- [x] Product direction: General Agent Core + Coding Agent.
- [x] Architecture style: Thin kernel + composable standard services.
- [ ] Review and accept architecture v0.1.
- [ ] Approve bootstrap implementation task.
- [ ] Implement, test and accept the first code change.

**License:** not yet selected. No license grant should be inferred from public visibility.