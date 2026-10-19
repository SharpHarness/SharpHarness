# SharpHarness documentation index

**Architecture Specification v0.1 | 2026-10-19 | Status: DRAFT FOR OWNER REVIEW**

本目录目前是**待确认的设计基线**，并非稳定 API 或代码实现。产品负责人已确认“通用 Core + Coding Agent”、“精简内核 + 标准运行服务”、ADR-0002 单 Session Actor 并发模型，以及 ADR-0003 Command/Event 协议（持久事实、实时通知、多订阅者、命令去重、事件重放），以及 ADR-0004 强类型分阶段扩展管线；其余 Proposed ADR 仍需审阅。除已被明确批准的设计文档外，Coding Agent 不得直接开始实现。

## 阅读顺序

1. [总体设计](architecture/overview.md)：目标、边界、MVP、依赖约束。
2. [模块划分](architecture/module-boundaries.md)：程序集与组合规则。
3. [Session/Turn/协议](architecture/runtime-protocol.md)：Actor、状态、命令和事件。
4. [模型与上下文](architecture/model-and-context.md)：Provider 语义、上下文构造。
5. [工具与安全](architecture/tools-and-security.md)：验证、审批、执行。
6. [持久化与恢复](architecture/persistence-and-recovery.md)：事件日志、副作用。
7. [契约草案](specifications/contracts-v0.1.md)：公共概念和行为语义。
8. [测试策略](testing/test-strategy.md)、[Review 清单](review/review-checklist.md)。
9. [实施任务 T001](tasks/T001-bootstrap.md)：**待批准**，用于交给其他 Coding Agent。

## 架构决策记录

- [ADR-0001 Thin Kernel](adr/0001-thin-kernel.md)（Accepted：产品负责人已选 B）
- [ADR-0002 Single-writer Session](adr/0002-session-actor.md)（Accepted：单活动 Turn，Busy 拒绝新 StartTurn）
- [ADR-0003 Commands/Events](adr/0003-command-event-protocol.md)（Accepted：Command + Event/Notification，支持去重及持久事件重放）
- [ADR-0004 Typed extension pipeline](adr/0004-extension-pipelines.md)（Accepted：Core 固定阶段顺序及检查点，通过显式契约和 DI 扩展）
- [ADR-0005 Journal/Snapshot](adr/0005-persistence.md)（Proposed）
- [ADR-0006 Mandatory tool policy](adr/0006-tool-authorization.md)（Proposed）

## 待产品负责人批准的问题

- 剩余 ADR-0005～0006 是否批准成为 v0.1 基线？
- CLI 首版是否默认开启权限审批及本地持久化？建议是。
- 第一次开发任务是否只允许工程骨架+测试基础设施？建议是。
- 开源许可证尚未确定；公开仓库不等于已授权他人使用/再分发。

通过 Review 后：更新各 ADR 状态，并由产品负责人批准首个任务。文档修订使用 PR 保留审查历史。
