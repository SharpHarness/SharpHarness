# SharpHarness documentation index

**Architecture Specification v0.1 | 2026-10-21 | M0 CLOSED: formally accepted by product owner**

本目录的六项 ADR 与 C01–C07 行为契约已获批准，M0-B01 文档一致性复核通过，且产品负责人已于 **2026-10-21 正式签署关闭 M0**。这不是已实现的 Runtime，也不代表具体 C# 公共 API 已冻结。产品负责人已确认“通用 Core + Coding Agent”、“精简内核 + 标准运行服务”、ADR-0002 单 Session Actor 并发模型，以及 ADR-0003 Command/Event 协议（持久事实、实时通知、多订阅者、命令去重、事件重放），以及 ADR-0004 强类型分阶段扩展管线，以及 ADR-0005 以 SQLite 承载 Agent Journal + Optional Snapshots，以及 ADR-0006 统一工具授权、安全检查点与明确的无沙箱限制；所有六项 ADR 已获产品负责人批准。除已被明确批准的设计文档外，Coding Agent 不得直接开始实现。

## 阅读顺序

1. [总体设计](architecture/overview.md)：目标、边界、MVP、依赖约束。
2. [模块划分](architecture/module-boundaries.md)：程序集与组合规则。
3. [Session/Turn/协议](architecture/runtime-protocol.md)：Actor、状态、命令和事件。
4. [模型与上下文](architecture/model-and-context.md)：Provider 语义、上下文构造。
5. [工具与安全](architecture/tools-and-security.md)：验证、审批、执行。
6. [持久化与恢复](architecture/persistence-and-recovery.md)：事件日志、副作用。
7. [M0-B01 契约评审](specifications/m0-contract-decisions-v0.1.md)：C01–C07 已批准；M0-B01 总验收 PASS，M0 总体已正式验收关闭；[现有契约类型目录](specifications/contracts-v0.1.md) 仍为 Draft。
8. [测试策略](testing/test-strategy.md)、[Review 清单](review/review-checklist.md)。
9. [实施任务 T001](tasks/T001-bootstrap.md)：**BLOCKED，待单独审批**；[T001 实施准备评审](review/t001-readiness-review.md) 仅提供范围、交付物和验收建议。

## 架构决策记录

- [ADR-0001 Thin Kernel](adr/0001-thin-kernel.md)（Accepted：产品负责人已选 B）
- [ADR-0002 Single-writer Session](adr/0002-session-actor.md)（Accepted：单活动 Turn，Busy 拒绝新 StartTurn）
- [ADR-0003 Commands/Events](adr/0003-command-event-protocol.md)（Accepted：Command + Event/Notification，支持去重及持久事件重放）
- [ADR-0004 Typed extension pipeline](adr/0004-extension-pipelines.md)（Accepted：Core 固定阶段顺序及检查点，通过显式契约和 DI 扩展）
- [ADR-0005 Journal/Snapshot](adr/0005-persistence.md)（Accepted：SQLite + WAL/FULL、ISessionStore、Unknown 禁止自动重试）
- [ADR-0006 Mandatory tool policy](adr/0006-tool-authorization.md)（Accepted：验证 → Policy → 审批 → SQLite 意图 → Executor；首版无完整 OS Sandbox）

## 待产品负责人批准的问题

- **M0 已正式 CLOSED / Accepted（2026-10-21）**：[六项 ADR](adr/0001-thin-kernel.md)、[C01–C07](specifications/m0-contract-decisions-v0.1.md) 和 [M0 Final Review](review/m0-consistency-review.md) 全部签署完成；此决策**不等于批准 T001 或合并 PR #1**。
- CLI 首版是否默认开启权限审批及本地持久化？建议是。
- T001 实施范围、交付物及验收条件仍需独立审查和授权；[查看 T001 准备评审](review/t001-readiness-review.md)。
- 开源许可证尚未确定；公开仓库不等于已授权他人使用/再分发。

六项 ADR、七项行为契约及 M0 文档复核均已正式通过，M0 已关闭。**T001 仍 BLOCKED，未授权任何 Coding Agent 实现；PR #1 继续 Open，不自动合并到 main。**
