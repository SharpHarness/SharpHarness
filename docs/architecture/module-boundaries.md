# Module Boundaries v0.1

**Status:** Draft. 名称表示逻辑职责；不要求第一轮就全部拆成独立可发布 NuGet 包。

## 项目与职责

| Project | 包含 | 禁止包含 |
| --- | --- | --- |
| `SharpHarness.Abstractions` | 公共模型、接口、版本化协议定义 | 具体 HTTP、文件系统、Shell、业务 Profile |
| `SharpHarness.Core` | Session/Turn 协调器、Agent Loop、命令队列、事件生命周期 | Anthropic/OpenAI SDK、Git、TUI、数据库实现 |
| `SharpHarness.Models` | 模型客户端工厂、协议 Adapter、能力发现与差异处理 | Agent 决策、授权策略 |
| `SharpHarness.Tools` | 通用 Tool Registry、执行编排标准实现 | 具体 Coding Agent 业务规则 |
| `SharpHarness.Infrastructure` | 默认 SQLite SessionStore（`Microsoft.Data.Sqlite`）、Agent Journal/Snapshot、文件系统、进程及平台适配 | 模型 Loop、业务决策；将 SQLite 依赖泄漏到 Core |
| `SharpHarness.Coding` | 编码 Profile、项目上下文、Git/Shell/File 工具、验证服务 | 模型协议和通用 Session Engine |
| `SharpHarness.Cli` | composition root、命令行及初版 TUI | 自行实现 Agent Loop/Model Adapter |
| `SharpHarness.Mcp` (future) | MCP SDK 的适配 | 重复实现 MCP 协议 |

## 依赖规则

- `Abstractions` 仅依赖必要的 .NET 基础库，禁止引用其他 SharpHarness 工程。
- `Core` 依赖 `Abstractions`，调用通过 DI 注入的接口；不得引用 `Models`、`Coding`、`Cli`、`Infrastructure`、`Microsoft.Data.Sqlite` 或任何 Native SQLite 实现。
- `Models`、`Tools`、`Infrastructure` 依赖 `Abstractions`；确实需要共同编排的能力由组合层连接，不得产生循环依赖。
- `Coding` 可以使用 Core 和工具机制及其他适配实现，但不反向成为底层依赖。
- `Cli` 是组装入口，可以引用具体实现；UI 不得修改 Core 的私有状态。
- 示例、测试应用通过公开 API 访问核心，不依赖私有实现以规避验证。

**ADR-0005 已批准：**初版生产级本地持久化 Adapter 采用 SQLite + WAL/FULL；SqlitePCLRaw/原生 SQLite 资源可作为 Infrastructure 的 NuGet 运行时依赖，CLI Composition Root 负责注册 Store。Core 的协议、事件及 Session 逻辑保持存储技术无关。Native AOT 在后续专门验证。

## 包边界判断原则

先保证概念清晰、依赖无环；NuGet 的拆分和语义版本策略在 API 签名稳定后制定。不要为了“可扩展”创建空接口、万能 BaseAgent 或大型 ServiceLocator。

## 扩展方式

- 进程内托管组件：普通 .NET 模式，可直接 DI。
- 进程外组件：MCP/RPC，隔离和进程生命周期单独治理。
- Native AOT 不支持运行时任意加载普通托管程序集，未来需预编译注册或进程外扩展。
- **ADR-0004 已批准**：使用按阶段划分的强类型接口和宿主 DI 组合，不采用万能 `IMiddleware` 或随意包裹整个 Agent Loop 的通用 Hook。
- Core 控制阶段次序、运行生命周期及必须执行的检查点；具体 Context/Model/Tool/Event 服务可替换，但不得反向改变 Core 状态或绕过安全关卡。
- 事件观察者是只读订阅者，尽力而为的日志与指标允许故障隔离；持久化 Journal 是关键可靠性服务，不能作为可忽略的日志观察者处理。
- **ADR-0006 已批准：**托管 Tool Pipeline 的验证、Policy、精确审批、SQLite 执行意图、Executor 由 Core 编排并保留固定检查点；Tool 适配器不能单独向 Core 暴露绕过 Policy 的可执行快捷路径。
- 进程内动态扩展仅在被视为受信任代码的前提下装配；不可信代码必须获得真正的进程/OS 隔离才能形成强制安全边界，首版不承诺具备该能力。
- 进程内扩展的生命周期、优先级及异常策略属于待评审 API Contract，不允许 Coding Agent 自行发明公共语义。

## 代码 Review 准则

出现任意一条就必须请求修改：`Core` 引用 Coding/CLI；模型响应结构强行绑定 Chat Completions；Session 状态多个写入者；工具绕开 Policy；所有模块以全局静态可变状态交换数据。
