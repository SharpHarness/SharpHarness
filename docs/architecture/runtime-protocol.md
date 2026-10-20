# Session, Turn and Execution Protocol v0.1

**Status:** Draft (protocol details) | ADR-0002 and ADR-0003 Accepted; public API shapes remain proposals.

## 1. 生命周期定义

- **Runtime**：装配 Provider/Tools/Services，创建或恢复 Session。
- **Session**：多 Turn 的独立对话和配置边界，拥有命令 Mailbox、状态版本和事件游标。
- **Turn**：一次用户任务的完整执行，从请求排队到完成/失败/中断。
- **Step**：ContextBuild、ModelInvocation、ToolInvocation、Compaction 或 Verification 的独立执行单元。

Session 不能等同一个 `ChatAsync` 请求。**C01 已批准**：同一 Runtime 内每个 SessionId 只允许一个控制句柄/一个逻辑 Actor；该 Session 已被控制时第二个 `OpenSession` 必须返回 `SessionAlreadyOpen`，不可隐式共享控制权；多个独立只读事件订阅者可以观察同一个 Session，不获得提交命令或 Dispose 控制句柄的权利。释放控制权后可以按恢复协议重新打开，具体关闭语义待 C06 确认。此处不承诺跨进程或分布式所有权协调。默认每个 Session 同时最多一个活动 Turn；跨 Session 并发运行。**ADR-0002 已批准**：活动 Turn 存在时新的 `StartTurn` 直接返回明确的 `Busy` 拒绝结果，不会创建第二个 Turn，不会隐式排队，也不会自动视为 `SteerTurn`；原活动 Turn 不受影响。

## 2. Actor / Mailbox

单 Session 一个逻辑 Actor 管理可变状态。通过有界 Channel 输入命令；不能在 Actor 的主处理循环中直接等待完整 AgentLoop 完成，否则 `Interrupt`、`Steer`、`Approval` 无法及时处理。

TurnExecutor 在独立异步执行任务中运行，其步骤结果发回 Actor 进行状态转换和事件编号。只有 Actor 决定 Session/Turn 状态；取消由 Coordinator 触发对应 CTS，同时记入中断命令以确保事实一致性。内部完成通知也进入同一串行状态通道。

**C04 已批准：**Actor 负责唯一逻辑状态写入、事件编号及待持久化转移的决策。SQLite I/O 在独立执行上下文处理，由带有关联 ID、Session/Turn 代次的内部结果通知返回 Actor；Storage Worker 不是第二个状态决策者。邮箱不得同步等待耗时磁盘操作；提交结果仍需由 Actor 串行处理，并维持每 Session 的事务顺序及 expected Version/Sequence 冲突规则。对可能产生副作用的工具，先确认预执行意图成功提交，再由 Actor 重新核实当前 Turn 未终止/取消、审批仍有效及 Invocation 代次匹配，方可调度 Executor。提交失败/不确定禁止派发；若提交成功但中途已经处理 Interrupt，未派发的工具也不能启动。迟到或重复的完成回调不得改写终态或启动已作废工具。即使意图已持久化，也不等于工具已经实际执行；崩溃恢复需按可信执行事实分类处理。

## 3. Command 语义（候选）

| Command | 前置条件 | 行为与保证 |
| --- | --- | --- |
| `StartTurn` | Session idle | 接收并开始一个新 Turn；接收回执不是完成信号。若已有活动 Turn，返回 `Busy`，不排队、不改变当前 Turn（ADR-0002） |
| `SteerTurn` | Turn active | 入队补充用户约束，在下一个安全模型请求检查点生效 |
| `InterruptTurn` | Turn active | 触发取消，尽力终止子调用，并产生终态 |
| `ResolveApproval` | 有匹配的待审批 ID | C05 Accepted：审批绑定 Session/Turn/稳定 InvocationId、工具身份/版本、规范化参数、有效资源范围、授权能力及有效期；重复同决策按 C02 幂等返回，冲突、过期、取消后或不匹配时绝不派发 Executor |

**ADR-0003、C02 已批准**：每条命令必须有稳定的 `CommandId`，以 `(SessionId, CommandId)` 去重。具有可合法标识的命令首次确定接受或拒绝（包括 `Busy`）时，必须将规范化载荷指纹、原始回执及相关持久事实原子记录；同 ID 同载荷重试返回原回执、不重新执行，即使 Session 后来空闲也不得将原 `Busy` 变为 `Accepted`。同 ID 不同载荷返回 `DuplicateCommandConflict`，不改变原结果；可靠存储重启后仍需保留已存回执直到 Session 的明确保留策略允许清理。存储失败或提交结果不确定时，不能声称原回执已可靠提交，也不能授权副作用；使用原 CommandId 重试并先核对持久记录。身份无效而无法安全去重的拒绝不承诺持久化。`SubmitAsync` 的 CancellationToken 仅取消“等待提交/回执”，不能隐式取消已经接受的 Turn；仅通过 `InterruptTurn` 请求终止。

**C05 已批准：**Runtime 使用稳定 `InvocationId` 标识一次逻辑工具执行，不得重启后换身份来绕开已有 Unknown/拒绝状态。模型的 `ToolCallId` 与审批的 `ApprovalId` 是独立关联 ID。审批通过不代表允许立刻派发，仍必须经过 C04 的存储提交确认及当前状态复核。ToolResult 必须区分 `Succeeded`/`Failed`/`Rejected`/`Cancelled`/`Unknown`，副作用状态未确定不得被降格为普通失败，也不得自动重试。具体审批参数序列化与 CLI 默认权限配置留待对应任务合同。

## 4. Turn 状态与错误

`Queued → Running ↔ AwaitingApproval → Completed | Failed | Interrupted`；已终止状态不可逆。Turn 状态表示可见总体进度；并行 Tool Step 由各自状态表述。每个终态必须携带 `CompletionReason`、错误/预算限制详情（适用时）。

- 模型请求异常：由 Provider 分类 transient/permanent/protocol，重试受预算约束。
- 工具业务失败：结构化 `ToolResult` 反馈模型，非必然使 Turn 失败。
- 授权拒绝：不执行工具，将拒绝事实/结果返回模型。
- 用户中断：发送取消，等待有界清理，超限明确记录无法确认的执行状态。
- 预算触顶：有界结束，显式提示限制原因；禁止死循环。

## 5. 并发与确定性

- 同一 Turn 的多个模型工具调用保留请求中的 `CallIndex`，按该索引稳定组装工具结果。
- 默认工具串行。可信任内置只读工具可通过明确的冲突/资源规则并行；不以模型声明的 `readOnly` 作为信任依据。
- 追加用户要求不会修改已发往模型的不可变请求，只在安全检查点作用于后续上下文。
- 不提供含糊的“完全同时修改 Session”语义。将来 Subagents 使用独立执行作用域。

## 6. Event 与实时输出

**ADR-0003 已批准**：对外输出区分 **Durable facts**（TurnStarted、ToolInvocationPrepared、ApprovalResolved、ToolCompleted、TurnCompleted 等）和 **Ephemeral notifications**（TextDelta、工具进度、可选推理摘要）。持久事实在配置可靠 Session Store 时必须支持按事件游标重放；实时增量默认不保证持久化或重放。

**C03 已批准：**持久事实信封包含 `SchemaVersion, SessionId, TurnId?, Sequence, EventId, CorrelationId?, Timestamp, Payload`。每个 Session 的持久 Sequence 单调递增且唯一，只有真正提交的事实才能作为 Durable Event 对外发布。订阅起点包含 `FromBeginning`、**排他**的 `AfterSequence(n)`（只返回 `Sequence > n`）以及从当前已提交高水位原子注册的 `LiveOnly`。保留范围内的历史回放到实时流的衔接不得遗漏已提交事实，断线重连可以至少一次重复投递，客户端按 `(SessionId, Sequence)` 或 `EventId` 去重；游标历史已清理或不可用时必须显式报告，不能悄悄跳过。

多个订阅者是**各自独立的广播消费者**，不能竞争同一个 `ChannelReader`；每个订阅者使用有界缓冲且其取消/故障不影响其他订阅者或阻塞 Session Actor。对于跟不上的 Durable Event 订阅者，必须可见地报告溢出并断开，客户端以**自己已经实际处理并保存**的最后一个持久 Sequence 重新订阅。TextDelta、工具进度等 Ephemeral Notification 不拥有持久 Sequence，不保证重放，允许合并/丢弃但应通知依赖完整增量的消费者发生了缺口。具体 C# 订阅方法及缓冲配置仍待接口实现合同确定；缺乏可靠存储时不得承诺断线后可重放。

可靠执行事实需满足“持久追加成功后可发布/可依赖”；日志不可用时不能继续启动可能产生副作用的新工具。
