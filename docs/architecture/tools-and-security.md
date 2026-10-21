# Tool Runtime and Security Boundaries v0.1

**Status:** Draft (implementation details) | ADR-0006 Accepted: mandatory governed tool path and explicit no-sandbox limitation.

## 1. 职责

Tool Registry 管理工具 ID、JSON Schema、描述和能力元数据；Tool Coordinator 管理调度；Policy Engine 评估授权；Executor 负责执行；Approval Coordinator 和 Session Actor 协调用户批准。业务工具属于 Coding Profile 或其他业务模块，不能作为 Core 内置业务依赖。

## 2. 唯一执行路径

```text
Model ToolCall
 → Lookup tool and binding
 → Validate arguments / size / version
 → Determine actual execution scope
 → Policy evaluate (allow / deny / ask)
 → Durable approval request + await decision (if ask)
 → Commit durable invocation intent in SQLite (before side-effect)
 → Controlled executor with timeout / cancellation / output limits
 → Durable terminal result (or Unknown)
 → Normalized tool response for model
```

**ADR-0006 已批准**：所有通过 SharpHarness 托管执行入口调用的内置工具、Coding 工具、MCP 适配工具和未来子 Agent 工具都必须走此路径；Hook 无权跨越或重排授权节点。Unknown Tool、参数无效、作用域无法确定、Policy 失败或拒绝、审批无效均须 fail closed，**不能启动 Executor**。**C05 已批准：**模型返回的 `ToolCallId` 仅负责关联模型调用，Runtime 为每个逻辑工具执行分配**可跨恢复保持稳定**的 `InvocationId`，`ApprovalId` 关联该次审批请求。审批绑定 Session/Turn、InvocationId、精确工具身份/版本、规范化参数指纹、解析后的有效资源范围、能力/权限与有效期；改变任一获批执行属性必须重新校验及授权，不能用新 InvocationId 绕过 Unknown 状态。`ResolveApproval` 经 Session Actor 串行处理，重复相同决定可按 C02 原回执处理，但重复、过期、取消后到达、参数/范围不匹配或冲突的审批绝不能授权错误执行。审批通过后仍须按 C04 对当前 Turn/Invocation/授权状态复核，且 SQLite 事务明确确认工具执行意图提交成功，才可派发潜在副作用。

## 3. 可信边界

- LLM、工具 Schema 描述、插件元数据、MCP 输出和 Shell 输出均视为不可信。
- `ReadOnly` 元数据不是沙箱证明；默认禁止依靠第三方声明开启并行写入或网络访问。
- Policy 判断用户是否授权，**不等于** OS Sandbox 的权限隔离；没有沙箱时不得宣传“已隔离”。
- Shell 命令不得通过未经审查的字符串拼接引入额外命令；执行策略必须约束 cwd、环境变量、进程树、输出大小和取消语义。
- 工作目录外读写、网络访问、子进程执行等应有独立政策能力，默认高风险行为需审批。
- **进程内受信任扩展限制：**任意第三方 .NET 代码如果被加载到宿主的同一高权限进程，可以绕开 SharpHarness 的托管工具调用入口直接调用 OS API。授权管线仅强制约束**由 Runtime 托管的 ToolInvocation**，不是任意恶意扩展的沙箱。未知/不可信代码必须另用进程级/OS 级隔离或不加载。首版没有完整 OS Sandbox，CLI 必须如实展示实际权限边界。

## 3.1 执行权限的评审边界

- **已批准机制：**Policy 必须返回 Allow / Deny / Ask；Ask 必须获得精确、未过期的 Approval 才能继续；否则 fail closed。
- **尚待另行批准（M3）：**CLI 默认授予哪些文件读取/写入、Shell、Git 和网络权限，任务级信任/自动批准、授权策略配置，以及路径/符号链接 TOCTOU 防护细节。不能由 Coding Agent 私自决定宽松默认值。单次审批的实际有效期必须按 C05 验证，默认时长仍未规定。
- 不能仅依据 `cwd`、`UseShellExecute=false`、插件 `ReadOnly` 标签或提示词规则承诺真正的资源隔离。Native Sandbox 是后续独立里程碑。

## 4. ToolResult 与故障

调用结果携带 `InvocationId, ToolCallId, Status, StructuredContent, DisplayText?, Duration?, Truncated, SideEffectState`。**C05 已批准：**状态语义为 `Succeeded`、`Failed`、`Rejected`、`Cancelled`、`Unknown`；`Cancelled` 不等于保证没有副作用，必须另行检查 SideEffectState；无法确认是否产生外部效果时归入 `Unknown`，不能冒充已确认失败。截断后的长输出必须明确标记。

对已经可能产生外部副作用的操作，取消、崩溃、网络中断不可直接归为 Failed；只要无法确认提交结果，就设为 Unknown。未知状态不触发自动重试。使用稳定 Invocation ID，允许适配幂等工具。

## 5. 首版 Coding Tools（Profile 层）

`read_file`, `list_files`, `search`, `apply_patch`, `run_command`, `git_status`。首次实现前分别定义路径解析、符号链接、编码、安全检查、变更预期、二进制处理和平台差异。默认不允许未经明确审批的工作目录外修改。

## 6. 安全验收用例

未知工具、非法 Schema、拒绝授权、重复/过期审批、审批后参数或作用域篡改、SQLite 意图提交失败、恶意工具输出试图改变授权判断、cwd 越界、符号链接逃逸、命令超时/子进程未终止、海量输出、MCP 返回恶意内容、事件日志泄密。验收应验证不可越过 Runtime 的受控授权入口，而不是声称模型对所有 Prompt Injection 免疫。对不具备真实 OS 沙箱的第一版，应将该缺口显式列为已知限制。
