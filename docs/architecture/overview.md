# SharpHarness Architecture Overview v0.1

**Status:** Draft | **Owner decisions accepted:** General Agent Core + Coding Agent; Thin Kernel + Standard Runtime Services; ADR-0002 Session Actor; ADR-0003 Command/Event; ADR-0004 typed stage-specific extensibility; ADR-0005 SQLite-backed Journal + Optional Snapshots; ADR-0006 mandatory Tool Policy/Approval and explicit no-sandbox boundary.

## 1. 产品目标

SharpHarness 是纯 C#、跨平台、模型无关、可嵌入的 AI Agent Runtime。它不是聊天客户端，也不是强制 DAG 的通用工作流平台。第一款基于 SDK 的应用是 Coding Agent，包括 CLI/TUI，但 SDK 必须能用于运维、自动化和其他 Agent Profile。

“纯 C#”指官方核心和运行组件使用 C#/.NET 开发，不要求自行实现 git、Shell 或编译器；外部进程按受控工具调用处理。目标平台 Windows、Linux、macOS；首版目标 .NET 10，Native AOT 为后续独立验证目标，不作为 v0.1 验收前提。

## 2. 目标与非目标

### 目标

- 稳定的 Session/Turn 生命周期、模型工具循环、取消与用户审批；异步流式事件。
- 可替换的模型 Provider、上下文、策略、工具、持久化服务。
- 模型协议能力与多模态内容不被错误降级；错误结果可观测。
- 安全控制、长任务可靠性、恢复语义有明确边界。
- 可通过 NuGet SDK 嵌入；Coding Agent 不污染 Core。

### v0.1 非目标

- 多 Agent 编排、分布式调度、复杂 DAG Workflow。
- 首版完整 OS 沙箱、原生 AOT 动态插件兼容、完整 RAG。
- 上线运营所需 Web UI、远程执行、完整 MCP 插件生态。
- 从第一天就划分数十个 NuGet 包。

## 3. 逻辑结构

```text
Hosts: CLI/TUI | Embedded .NET SDK | Future RPC/IDE
                       |
              Coding Agent Profile
                       |
                  Agent Core
  Session actor | Turn loop | events | execution coordination
          /             |                \
    Model adapters   Tool executors   Standard services
    and capability    and policy      context / store / telemetry
                       |
         Platform adapters: files/process/OS sandbox
```

**Core owns when and in which order a capability is called, not how that capability is implemented.** Under accepted ADR-0004, extension contracts are typed per Context, Model, Tool, and Event/Observability stage rather than a universal `IMiddleware`. Mandatory checkpoints cannot be skipped by hooks. Runtime providers/implementations are injected through Abstractions. Composition happens at the CLI/host entry point.

## 4. 强制不变量

1. 一个 Session 中只有一个逻辑状态写入者，同时最多一个活动 Turn。
2. 一次经 Runtime 托管的工具调用路径固定为 `resolve → schema/scope validation → Policy Allow/Deny/Ask → bound Approval (if required) → durable SQLite intent commit (before effects) → controlled execution → result`（ADR-0006）。
3. 模型输出、工具参数、工具元数据、MCP 返回值均不可信；经 Runtime 管理的 ToolInvocation 不得绕过审批。此保证不等于任意受同进程权限的恶意插件或任意 Shell 命令已被 OS 沙箱隔离。
4. 副作用工具在可信终态事实缺失时恢复为 Unknown，不可默认重放；默认本地存储采用 SQLite 的 Agent Journal + Optional Snapshot，工具启动前需确认前置事实已成功提交（ADR-0005）。
5. 持久事实事件具有会话范围内单调序号、配置持久存储时可游标重放；Token Delta 作为非持久通知，不承诺重放。订阅者必须独立接收广播而非竞争同一队列（ADR-0003）。
6. 每次模型请求具有可追踪的不可变上下文快照；压缩不能吞掉活跃约束。
7. 任何任务都可以受取消与预算上限约束；终止状态必须明确可观察。
8. 扩展接口遵循阶段边界，Core 固定执行次序与强制检查点；可替换服务通过明确接口和 DI 装配，不能使用万能 Middleware（ADR-0004）。

## 5. v0.1 阶段里程碑

- **M0 Documents**：架构、ADR、契约、验收标准获得产品负责人通过。
- **M1 Bootstrap**：多项目解决方案、编译/测试、跨平台 CI 基础设施，不实现 Agent 行为。
- **M2 Minimal Agent**：Fake Model 驱动一个完整 Turn 和工具循环，事件/取消/预算测试通过。
- **M3 Coding Agent Alpha**：工作区、工具/策略、默认 SQLite SessionStore（WAL/FULL）及恢复、CLI，真实项目任务回归测试。M1 仅工程骨架，不要求生产级 SQLite；M2 可使用 Fake Store 测试 Runtime。

每项里程碑单独批准开发任务，测试证据须可复现；模型真实调用仅作为集成验收补充。

## 6. 非功能性要求

- cancellation 及时传播；日志不含密钥和私密推理原文。
- 设计中显式记录兼容性、资源释放、错误分类、重试边界。
- 不断言 OS Sandbox 已存在；仅完成 Policy 不等于安全沙箱；未知第三方 .NET 扩展作为同进程可信代码会具有宿主权限，首版不能宣称能拦截其直接 OS 调用。
- 对外公共 API 在发布前需审查版本化策略。
- SQLite 原生引擎通过跨平台 NuGet 运行时依赖发布，官方应用和适配器逻辑仍以 C# 实现；Native AOT 不是首版验收门槛。