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

TurnExecutor 在独立异步执行任务中运行，其步骤结果发回 Actor 进行状态转换和事件编号。只有 Actor 写入 Session/Turn 状态；取消由 Coordinator 触发对应 CTS，同时记入中断命令以确保事实一致性。内部完成通知也进入同一串行状态通道。

## 3. Command 语义（候选）

| Command | 前置条件 | 行为与保证 |
| --- | --- | --- |
| `StartTurn` | Session idle | 接收并开始一个新 Turn；接收回执不是完成信号。若已有活动 Turn，返回 `Busy`，不排队、不改变当前 Turn（ADR-0002） |
| `SteerTurn` | Turn active | 入队补充用户约束，在下一个安全模型请求检查点生效 |
| `InterruptTurn` | Turn active | 触发取消，尽力终止子调用，并产生终态 |
| `ResolveApproval` | 有匹配的待审批 ID | ADR-0006：匹配 Session/Turn/Invocation、工具身份/版本、参数与解析后的作用域、有效期；重复、过期、被取消或载荷不匹配必须安全拒绝或幂等处理，不启动错误的工具 |

**ADR-0003、C02 已批准**：每条命令必须有稳定的 `CommandId`，以 `(SessionId, CommandId)` 去重。具有可合法标识的命令首次确定接受或拒绝（包括 `Busy`）时，必须将规范化载荷指纹、原始回执及相关持久事实原子记录；同 ID 同载荷重试返回原回执、不重新执行，即使 Session 后来空闲也不得将原 `Busy` 变为 `Accepted`。同 ID 不同载荷返回 `DuplicateCommandConflict`，不改变原结果；可靠存储重启后仍需保留已存回执直到 Session 的明确保留策略允许清理。存储失败或提交结果不确定时，不能声称原回执已可靠提交，也不能授权副作用；使用原 CommandId 重试并先核对持久记录。身份无效而无法安全去重的拒绝不承诺持久化。`SubmitAsync` 的 CancellationToken 仅取消“等待提交/回执”，不能隐式取消已经接受的 Turn；仅通过 `InterruptTurn` 请求终止。

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

持久事实信封包含 `SchemaVersion, SessionId, TurnId?, Sequence, EventId, CorrelationId?, Timestamp, Payload`。每 Session Sequence 从同一状态写入者产生，递增且唯一。实时 TokenDelta 可以携带非持久游标，不能冒充持久事件序号。多个订阅者必须是广播而非竞争一个 `ChannelReader`；订阅者的取消、缓冲与背压处理相互隔离，慢客户端有独立背压/断线补读政策。事件重放游标只引用持久事件序号；缺乏可靠存储时不得承诺断线后可重放。

可靠执行事实需满足“持久追加成功后可发布/可依赖”；日志不可用时不能继续启动可能产生副作用的新工具。
