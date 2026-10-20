# Session Persistence and Recovery v0.1

**Status:** Draft (implementation contracts) | ADR-0005 Accepted: SQLite-backed Journal + optional Snapshots.

## 1. 原则

Session 持久化不仅保存聊天记录，还需要保存能重建执行状态的**事实事件**。推荐 Append-only Journal + Optional Snapshot：快照含最后应用的 Sequence；恢复时先加载快照，再重放之后的 Journal。

**ADR-0005 已批准：**默认生产级本地存储实现为 SQLite（`Microsoft.Data.Sqlite`，原生 SQLite 作为部署依赖）；数据内容为 Append-only Agent Journal 与 Optional Snapshot。存储 Adapter 属于 Infrastructure，Core 只依赖 `ISessionStore` 一类抽象，不得引用数据库驱动。SQLite 的 WAL 是引擎内部写前日志，**不是** Agent 事件 Journal。首版无需第二套 JSONL 实时存储引擎；JSONL 导入/导出可作为未来诊断能力。

## 2. 原子性与顺序

- 同 Session 单逻辑状态写入者，事件序号单调递增。SQLite 事务须协调 Journal 追加、Session Sequence/Version、CommandId 去重及派生状态索引，采用预期版本冲突检测或等价防护。多 Session 可并发处理，但 SQLite 同时只允许一个写事务。
- 数据库损坏、重复序号、快照与 Journal 不一致须显式报告；不能静默重放或补造缺失事实。Snapshot 必须附带与已提交 Journal 匹配的 Sequence 和 SchemaVersion。
- **强制持久化栅栏：**在可产生外部副作用的工具实际启动前，必须提交并确认 Invocation 意图/开始事实。写入失败或提交结果不确定时不允许触发新的副作用。数据库事务不能覆盖真实文件、Git 或 Shell 操作。
- 日志持久化失败不得假装任务成功；普通遥测失败可以隔离处理。
- SchemaVersion 随每条记录保存；未知版本采用 fail-closed 或显式迁移，不得随意反序列化成默认值。

## 2.1 SQLite 本地适配器配置（已批准方向）

生产级默认连接须验证 `PRAGMA journal_mode=WAL`，并对每个写连接确保 `PRAGMA synchronous=FULL`；存在外键时建议使用 `PRAGMA foreign_keys=ON` 并验证所需连接配置。禁止将 WAL+NORMAL 当作满足本 ADR 的默认实现；底层存储设备不正确履行同步保证时不承诺绝对断电持久性。

连接/事务应短小、有界；数据库锁竞争超时、写失败和 I/O 阻塞必须可观察。`Microsoft.Data.Sqlite` 的 ADO.NET 异步方法并不代表 SQLite 提供非阻塞磁盘 I/O，不能在 Session Actor 的命令邮箱循环中执行长耗时存储操作。

**C04 已批准：**Actor 是唯一 Session 状态决策者，Storage Executor 仅在独立执行上下文中提交由 Actor 准备的不可变事务请求，返回具有关联 ID 和 Turn/Session 代次的提交结果；逻辑状态转换在 Actor 串行处理。单事务涉及的 Journal/Sequence/Version、命令回执、派生状态索引保持原子一致并执行 expected-version/sequence 防护。执行潜在副作用前必须获得可信 Commit 成功结果，且 Actor 随后立即重新检查 Turn 未取消/终止、Invocation 代次和有效授权。SQLite 提交成功而用户已中断时不能派发未开始的工具；对延迟完成回调不允许修改终态或重新调度。提交失败/状态不确定时一律禁止派发；意图已提交不代表工具一定启动。事务绝不跨越实际外部工具执行。

默认 WAL 数据库在本地使用，不支持将其作为多主机通过网络文件系统共享的数据库。跨平台发布必须验证 Native SQLite 加载；Native AOT 留待单独里程碑。

## 3. 外部副作用恢复

**ADR-0006 已批准**：托管 Tool Pipeline 的实际执行前，先验证参数与作用域并通过 Policy/必要审批；对可能产生副作用的工具，必须在 SQLite 成功提交 `ToolInvocationPrepared/Started` 或等价的执行意图事实后才启动 Executor。完成后写 `ToolInvocationCompleted`。崩溃可能发生于真实修改成功之后、终态写入之前，此时恢复状态是 **Unknown**，不能自动再执行一次。

恢复策略：
1. 读取 Journal 重建执行事实。
2. 对正在等待审批的请求重新核实其上下文和有效期；失效则重新申请。
3. 对未结束的只读工具可在策略允许时重新执行；不可以仅凭工具自报 `ReadOnly` 判定安全。
4. 对未结束的副作用工具标记 Unknown，尽可能采用工具专用 Reconcile；无法判定则通知用户处理。
5. 对中断中的模型请求可重新构建下一步模型调用，但须保证消息和工具结果关联完整。

## 3.1 恢复时的事务边界

- 先验证数据库结构/SchemaVersion，再读取 Snapshot 和比其序号更新的 Journal 事实；不可凭快照覆盖更近期的提交。
- SQLite 事务可原子提交 Journal、Sequence、去重结果和派生索引，但不能对外部进程操作提供 Exactly Once。
- 如果工具已经可能执行而缺少可信终态，即使 SQLite 事务完好也要恢复为 Unknown，不能推断一定失败。
- 命令重放和 Session 恢复需要联动检查 CommandId，不能因为 CLI 断线/重新投递而重复执行工具。
- 迁移、WAL Checkpoint、在线备份与一致性还原必须遵守 SQLite 的一致性语义；不能在数据库正在使用时只随意复制 `.db` 文件并宣称完成可靠备份。

## 4. 隐私与数据治理

绝不记录凭据、Authorization Header 或未脱敏环境变量；对路径、工作区内容、工具输出提供本地可配置留存策略。原始模型内部推理不是默认持久化对象。所有 Snapshot/Journal 应记录协议版本和迁移要求。

## 5. 验收测试

验证事务中断与提交结果不确定、数据库损坏、磁盘满、SQLite 锁竞争、Journal 顺序/版本冲突、Snapshot 落后、重复投递、Kill 后恢复、工具已修改文件但 Completed 丢失、跨平台 Native 库加载、WAL/FULL 实际配置、备份恢复等场景。对于真实断电故障，必须有专门环境才可声明通过。对普通文件写入和 Shell 命令，不应宣称 Exactly Once；保证恢复过程“不盲目重复执行副作用”。
