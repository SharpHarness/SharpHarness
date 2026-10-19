# Tool Runtime and Security Boundaries v0.1

**Status:** Draft | Related: ADR-0006.

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
 → Durable invocation intent (before side-effect)
 → Controlled executor with timeout / cancellation / output limits
 → Durable terminal result (or Unknown)
 → Normalized tool response for model
```

所有通道（内置工具、外部 MCP、插件、未来子 Agent）必须走此路径；Hook 无权跨越或重排授权节点。拒绝或超时不得调用 Executor。Approval ID 与精确工具 ID、参数摘要、工作目录、风险能力、Turn 和有效期绑定，改变任何要素须重新评估。

## 3. 可信边界

- LLM、工具 Schema 描述、插件元数据、MCP 输出和 Shell 输出均视为不可信。
- `ReadOnly` 元数据不是沙箱证明；默认禁止依靠第三方声明开启并行写入或网络访问。
- Policy 判断用户是否授权，**不等于** OS Sandbox 的权限隔离；没有沙箱时不得宣传“已隔离”。
- Shell 命令不得通过未经审查的字符串拼接引入额外命令；执行策略必须约束 cwd、环境变量、进程树、输出大小和取消语义。
- 工作目录外读写、网络访问、子进程执行等应有独立政策能力，默认高风险行为需审批。
- 插件内托管代码本身可能任意访问进程权限；真正隔离需要进程/OS 级措施。

## 4. ToolResult 与故障

调用结果携带 `InvocationId, ToolCallId, Status, StructuredContent, DisplayText?, Duration?, Truncated, SideEffectState`。Status 应区分 Success、Rejected、Failed、Cancelled、Unknown。截断后的长输出必须明确标记。

对已经可能产生外部副作用的操作，取消、崩溃、网络中断不可直接归为 Failed；只要无法确认提交结果，就设为 Unknown。未知状态不触发自动重试。使用稳定 Invocation ID，允许适配幂等工具。

## 5. 首版 Coding Tools（Profile 层）

`read_file`, `list_files`, `search`, `apply_patch`, `run_command`, `git_status`。首次实现前分别定义路径解析、符号链接、编码、安全检查、变更预期、二进制处理和平台差异。默认不允许未经明确审批的工作目录外修改。

## 6. 安全验收用例

未知工具、非法 Schema、拒绝授权、重复审批、审批后参数篡改、恶意工具输出注入、cwd 越界、符号链接逃逸、命令超时/子进程未终止、海量输出、MCP 返回恶意内容、事件日志泄密。对不具备真实 OS 沙箱的第一版，应将该缺口显式列为已知限制。
