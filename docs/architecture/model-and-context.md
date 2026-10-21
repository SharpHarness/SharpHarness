# Model and Context Pipelines v0.1

**Status:** Draft (contract details) | ADR-0004 (stage-specific typed extension pipelines) Accepted.

## 1. 基本选择

不要将 Chat Completions JSON 作为内部唯一消息模型。定义供应商无关的 `Message` / `ContentPart` / `ToolCall` / `ModelRequest` / `ModelEvent`，由独立协议 Adapter 映射 OpenAI Responses、Chat Completions、Anthropic Messages、Gemini API 和兼容实现。

`IModelClient` 表示已经配置好的模型调用器；可选 `IModelProvider` 表示模型枚举、配置与创建工厂。**这些仍是候选接口名称，不代表公共 API 已冻结。** 标准 Model 服务由宿主通过 DI 装配，Core 不得按厂商名称分支；模型适配器不允许更改 Tool 授权顺序。

## 2. 能力与数据不丢失

- 能力声明：`Tools, Vision, Streaming, Reasoning, StructuredOutput, MaxContextTokens` 等，区分“支持”“不支持”“未知”，不能臆测。
- 内容块：Text、Image、ToolCall、ToolResult；为音频、文件和结构化内容保留版本化扩展。
- Provider 专有能力：请求/响应扩展袋或受控的原生封装，不向所有 Provider 强加不存在的字段。
- 推理流与隐私：不强制持久化内部推理原文；保留必要的 Provider 状态句柄应遵循安全与过期策略。
- 错误标准化：认证/限流/服务端/取消/协议错误明确区分；不允许静默丢弃不支持的内容。

## 3. 流式 Tool Call

模型可能分片返回工具 ID、名称和 JSON 参数。Adapter 必须在分片完整、工具调用结束且 JSON 能验证后，才产出可执行 `ToolCall`。不完整输出只能形成结构化失败，不应被猜测修复后直接执行。重复 ToolCall ID、参数超限和乱序结束必须被检测。

在支持流式输出的情况下，文本增量用于 UI，但 Session 历史应基于完成的规范化消息，不依赖 UI 增量拼接结果作为唯一事实。

## 4. Context Pipeline

每轮模型调用前，从指令、当前用户请求、Session 历史、工作空间贡献、活跃约束、摘要和 Token 预算生成不可变 `ModelRequest`。**ADR-0004 已批准**：`ContextContributor` 一类强类型扩展只贡献候选片段，不允许直接修改持久 Session History。由内核与标准 Context 服务约定固定调用阶段；贡献者之间的排序和异常处理需在接口合同中显式定义，不得自行隐式注册全局 Middleware。Context Builder 决定最终优先级、去重、截断，并记录实际发送请求的可安全审计视图。

**优先级建议**：系统和安全不可变约束 > 开发/Agent Profile 指令 > 用户当前任务与显式约束 > 关联上下文 > 历史摘要。文档/工具返回内容只能作为不可信数据，不能升级为指令或权限。

## 5. 压缩与预算

- 将 Session 事实历史与实际本轮模型上下文分开；不直接破坏历史记录。
- `TaskConstraint` 明确区分用户指令、任务验收条件和已确认事实，压缩后仍需保留关键约束。
- 优先对过长工具输出作限制/摘要，严禁输出无限制增长。
- 在模型上下文不确定时遵守保守预算并给出可见错误；不虚构 Token 用量或价格。
- 最大模型回合、工具调用次数、耗时和总 Token 预算均由 TurnExecutionPolicy 控制。

## 6. Provider 测试要点

流分片重组、JSON 非法/截断、多个 Tool Call、非文本内容保留、不支持能力处理、Usage 缺失、429/超时及取消。测试使用录制的协议流和 Fake Model，真实 API 仅作补充性集成测试。
