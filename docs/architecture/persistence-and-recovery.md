# Session Persistence and Recovery v0.1

**Status:** Draft | Related: ADR-0005.

## 1. 原则

Session 持久化不仅保存聊天记录，还需要保存能重建执行状态的**事实事件**。推荐 Append-only Journal + Optional Snapshot：快照含最后应用的 Sequence；恢复时先加载快照，再重放之后的 Journal。

存储实现属于 Infrastructure，Core 只依赖 `ISessionStore` 一类契约。初期可用 JSONL（日志事实）+ JSON（快照），未来可升级 SQLite，但应保持相同事实语义。

## 2. 原子性与顺序

- 同 Session 单写入者，事件序号单调递增。追加存储需要约束预期序号/版本，实现 CAS 或等价并发防护。
- 数据损坏、重复序号、快照与 Journal 不一致须显式报告；不能静默重放或补造缺失事实。
- 关键状态转换与副作用调用之间确立持久化栅栏。若写入意图失败，不得执行副作用。
- 日志持久化失败不得假装任务成功；普通遥测失败可以隔离处理。
- SchemaVersion 随每条记录保存；未知版本采用 fail-closed 或显式迁移，不得随意反序列化成默认值。

## 3. 外部副作用恢复

在首次执行修改类工具前先写入 `ToolInvocationPrepared/Started`。完成后写 `ToolInvocationCompleted`。崩溃可能发生于真实修改成功之后、终态写入之前，此时恢复状态是 **Unknown**，不能自动再执行一次。

恢复策略：
1. 读取 Journal 重建执行事实。
2. 对正在等待审批的请求重新核实其上下文和有效期；失效则重新申请。
3. 对未结束的只读工具可在策略允许时重新执行；不可以仅凭工具自报 `ReadOnly` 判定安全。
4. 对未结束的副作用工具标记 Unknown，尽可能采用工具专用 Reconcile；无法判定则通知用户处理。
5. 对中断中的模型请求可重新构建下一步模型调用，但须保证消息和工具结果关联完整。

## 4. 隐私与数据治理

绝不记录凭据、Authorization Header 或未脱敏环境变量；对路径、工作区内容、工具输出提供本地可配置留存策略。原始模型内部推理不是默认持久化对象。所有 Snapshot/Journal 应记录协议版本和迁移要求。

## 5. 验收测试

测试尾行截断、磁盘写入失败、事件序号冲突、快照落后、重复投递、Kill 后恢复、工具已修改文件但 Completed 丢失等场景。对普通文件写入和 Shell 命令，不应宣称 Exactly Once；保证恢复过程“不盲目重复执行副作用”。
