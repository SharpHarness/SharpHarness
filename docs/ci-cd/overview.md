# SharpHarness CI/CD 与发布总体架构 v0.1

> **状态：Draft，待产品负责人审阅并批准合并**  
> **日期：2026-10-23**  
> **性质：工程交付与开源发布设计，不修改 M0 Runtime ADR 或 T001 已批准的实施范围**  
> **关联：** [G001 · PR Quality Gates](https://github.com/SharpHarness/SharpHarness/issues/4) · [T001](https://github.com/SharpHarness/SharpHarness/issues/2) · [T001 PR #3](https://github.com/SharpHarness/SharpHarness/pull/3)

## 1. 目标、边界与术语

SharpHarness 是 Apache-2.0 许可的纯 C# / .NET 10 跨平台 Agent Harness SDK，并将独立组合的 Coding Agent（CLI/TUI）作为首个应用。本设计统一规划 GitHub 分支协作、PR 质量验证、主干集成、版本发行、NuGet 和终端应用分发。

本文件描述**目标架构与分阶段实施顺序**，并不意味着相关 GitHub Ruleset、静态分析、nuget.org 凭据或发布 Workflow 已经配置。

- **CI（Continuous Integration）**：验证提交是否满足约定的构建、测试和质量条件。
- **Quality Gate**：GitHub 强制合并限制。Workflow 返回失败与 GitHub 真正禁止 Merge 是两回事。
- **Release**：由维护者批准的版本发布；不等于一次 PR Merge 或一次成功的构建。
- **Artifact**：构建、打包得到的候选制品；只有进入指定官方渠道后才算已发布。
- **Owner**：产品负责人，对重大治理调整、任务、正式版本和 PR 合并拥有最终授权权力。

## 2. 架构不变量和已选原则

| 编号 | 长期原则 |
| --- | --- |
| CD-01 | **Trunk-based**：\`main\` 是唯一长期稳定主干；开发分支通常是临时的。 |
| CD-02 | **PR-first**：进入 \`main\` 的代码必须通过 PR 和明确授权；不得借 CI 通过绕开独立验收。 |
| CD-03 | **Single-PR default**：日常任务由任意命名的隔离工作分支直接向 \`main\` 提 PR。复杂任务可以显式使用临时集成分支及第二级 PR，但不是默认要求。 |
| CD-04 | **Protect destination**：长期统一保护 \`main\`，不必逐一保护所有短命 \`feat/*\`；临时集成分支是否附加保护由任务风险决定，不改变 \`main\` 必需检查。 |
| CD-05 | **No release on merge**：PR 合并到 \`main\` 只触发持续集成；只有维护者批准的版本标签/发布操作才可发起正式 Release。 |
| CD-06 | **Build then promote**：按一个确定的源码提交和版本构建、验证、记录可追溯的制品，随后才允许上传官方仓库。 |
| CD-07 | **Least privilege**：PR 验证不能接触发布凭据，发布身份仅授予受控 Release Job。 |
| CD-08 | **Independent acceptance**：实现者自检、CI 绿灯与独立架构验收是不同概念；禁止自动合并未经负责人批准的 PR。 |
| CD-09 | **Incremental rollout**：先确保跨平台必需检查可靠，之后逐步增加审阅、静态分析、安全扫描和正式发布，避免空转/死锁式门禁。 |

对第三方开源贡献仍坚持同样的目标分支和 PR 检查原则。Coding Agent 的本地目录与工作分支名不受限制，仅规定实际 PR 的目标分支。

## 3. 分支模型与 PR 生命周期

### 3.1 默认：一个任务、一个到 \`main\` 的 PR

\`\`\`text
main (protected, long-lived)
 ├── feat/<task-or-feature>       ── PR ──> main
 ├── fix/<issue>                  ── PR ──> main
 ├── chore/<maintenance>         ── PR ──> main
 └── cursor/<agent-work>          ── PR ──> main
                                            |
                                    Required CI + review
                                            |
                                  owner-approved merge
                                            |
                             delete merged temporary branch
\`\`\`

- Branch 命名是协作惯例，不是技术边界；Agent 可使用自身工作分支。
- PR 必须有对应任务/授权、范围说明、测试证据、ADR 影响及回退说明。
- 合并后删除已完成、无其他依赖的临时分支；可**后续**启用仓库级“Automatically delete head branches”，当前不预设已启用。
- 不创建长期 \`develop\`，也不把每个 \`feat/*\` 默认变成需要单独管理的永久集成分支。
- 合并策略（squash/merge/rebase）在 G001 实施时再约定；保证 Issue/PR/提交可追溯即可。

### 3.2 例外：多 Agent、大功能或阶段集成

\`\`\`text
agent/task-part-A ─┐
agent/task-part-B ─┼─> integration/task-X ── final PR ──> main
agent/task-part-C ─┘
\`\`\`

- 显式创建集成分支，并在任务书中确定内部 PR 目标和最终 PR 目标。
- 集成分支合并 PR 不代表功能已进入稳定主干；**最终的 \`main\` PR 必须经过 \`main\` 的完整必需检查**。
- 对受合规/风险要求约束的集成目标，可以用**一次性范围模式 Ruleset**（例如 \`integration/*\`）统一保护，而非每个新分支重新配置；只有实际需要时才启用。
- 清理必须在最终 \`main\` PR 合并、相关引用/工作任务完成之后进行；避免过早删掉集成分支。

**T001 的已批准例外保持不变**：\`cursor/t001-dotnet10-scaffold-64b7\` → \`feat/t001-dotnet10-bootstrap\`（PR #3），然后 \`feat/t001-dotnet10-bootstrap\` → \`main\`（单独 PR）。本设计**不自动改变 T001-D01**。T001 的临时集成分支在最终合并后即可删除；不要求所有未来任务复制这个两级流程。

## 4. PR CI 与强制门禁

### 4.1 必需的三平台验证

正常代码 PR 的目标 CI 统一复用 \`.github/workflows/ci.yml\`：

| 检查名（现有 T001 PR #3） | Runner | 基本动作 |
| --- | --- | --- |
| \`restore-build-test (ubuntu-latest)\` | Ubuntu | SDK → restore → Release build → test |
| \`restore-build-test (windows-latest)\` | Windows | SDK → restore → Release build → test |
| \`restore-build-test (macos-latest)\` | macOS | SDK → restore → Release build → test |

- 运行在 **GitHub-hosted runners**，不是 Coding Agent 本身同时提供三个操作系统。
- 正常触发包括 \`pull_request\` → \`main\` 和 \`push\` → \`main\`；存在明确的临时集成目标时，Workflow 也应覆盖该 PR base。
- 三个 Job 的结果必须来自**同一被验收版本**，在 CI 记录中保留运行链接和提交 SHA。若目标分支变化、PR 内容更新或状态失效，必须按 GitHub 的合并状态重新验证。
- 有真实源码和测试项目之后，拒绝“零测试却显示成功”的误判；按任务阶段定义可证实的测试发现和断言。
- 不允许 \`continue-on-error\`、故意跳过核心测试等方式把失败变为成功；不要使用会让必需 Job 无法出现的路径/条件过滤。
- 对 PR 来自外部 Fork 的场景使用只读权限、可信依赖和最小作用域的 \`GITHUB_TOKEN\`；不要在不可信代码上下文中加载发布密钥。

### 4.2 GitHub 实际阻止合并的规则

**G001 第一阶段：最低限度的合并门禁**（单独实施任务，非本设计 PR 的改动）：

1. 给长期 \`main\` 创建并启用 GitHub Branch Ruleset，要求走 PR、禁止强制推送和受保护引用的意外删除。
2. 将已在真实 PR 上运行并验证名称/来源的三项 OS Job 设置为 **Required Status Checks**，明确预期状态源为 GitHub Actions（可选/可用时）。
3. 必需检查 **缺失、待执行或失败必须阻止合并**。执行一个故意失败/缺失检查的验证 PR，观察 GitHub UI 的 Merge 是否确实被禁止；恢复通过之后再解除阻断。
4. CI 全绿只是合并的**必要条件**，不自动合并、也不自动授予架构验收。是否另需强制审阅人、更新分支、解决全部讨论等规则，应在有实际协作角色时逐项批准，避免单维护者自我审批死锁。

**启动依赖与过渡期：**T001 PR #3 的 \`ci.yml\` 当前只在 PR 工作分支，尚未进入 \`main\`。不能在目标 PR 无法可靠产生所选 Check 的情况下提前配置 Required Check。先验证工作流对实际目标 PR 的执行，再启用相应保护；严禁通过取消保护来掩饰流水线配置问题。若 T001 第一级 PR 目标 \`feat/t001-dotnet10-bootstrap\` 仍需强制保护，必须单独决定该临时分支的规则范围并实测，不将其推广为以后每个 \`feat/*\` 都要手工配置。最终 \`main\` PR 始终满足主干门禁。

### 4.3 第二阶段的自动审阅

第一阶段稳定后，再由单独任务将以下审阅作为独立的可观察 Job 或扫描结果：

- **确定性质量检查**：\`dotnet format --verify-no-changes\`、.NET Analyzers/警告策略、项目依赖方向与架构边界检查。启用 Required 之前，先用违规样例验证会失败，并保证名称长期稳定。
- **CodeQL / 安全扫描**：在有 C# 源码之后启用，按实际告警质量确定是否/如何成为阻断门禁，不把自动扫描与人工语义审阅混淆。
- **可选 AI PR Review**：需要单独批准成本、数据访问范围、服务权限和阻断规则。AI 评论默认只是审阅输入；**机器人发布评论并不自动等于必需 Check 失败**。
- **架构验收**：由与实际实现者分离的审查者根据 ADR、契约和任务验收标准执行；在只有一位维护者时，不提前设置无法满足的“他人必需审批”规则。

## 5. 主干集成（Merge ≠ Release）

- 一旦 PR 获授权合并到 \`main\`，对主干执行 \`push\` CI，验证主干构建与测试状态。
- 可保存构建日志、测试结果或临时制品；不会因此自动 \`dotnet pack\` 并推送公开 NuGet，也不会创建正式 GitHub Release。
- 发生主干回归，应提交修复/回退 PR 或依据负责人批准的紧急处置流程处理；不要将失败主干伪装为稳定 Release。
- 保持制品、版本、源 SHA 之间可追溯；禁止未经授权直接修改 \`main\` 来规避检查。

## 6. 版本与正式发布

### 6.1 版本策略

- SDK 首期采用**统一 SemVer**：\`MAJOR.MINOR.PATCH\`，例如 \`0.1.0\`，必要时使用 \`0.1.0-rc.1\`。
- 稳定版本 Git Tag 为 \`vX.Y.Z\`；预发行使用 \`vX.Y.Z-rc.N\`。是否发布预发行 NuGet 包和 GitHub Pre-release，须在相应 Release 任务中明确。
- **不因每次 \`main\` 合并而递增或公开发布版本。** 版本由产品负责人批准，Release 使用已批准标签作为唯一输入（而不是使用会继续移动的 \`main\`）。
- 只有标签指向属于已审核 \`main\` 历史的提交且版本/产物清单匹配，才允许正式发布；不允许重写已发行标签指向不同源码。
- 可以通过受限标签触发 \`release.yml\`，必要时再加 GitHub Environment 审批。**创建发行标签属于发布授权行为**，需要清晰权限边界。
- NuGet 包版本发布后不能把“同一个版本号重新打包覆盖”作为回滚；失误时按需弃用版本并发补丁版。

### 6.2 Release 验证与产物晋级

\`\`\`text
Owner authorizes version/tag
         |
  release.yml (trusted tag)
         |
  validate version + tag SHA + ancestry
         |
  build / test / pack / native publish (as applicable)
         |
  verify package metadata, archive contents, checksums
         |
  release approval / scoped credentials
         |
  nuget.org           GitHub Releases
         |
  record published version, SHA, links, checksums
\`\`\`

发布任务必须正确报告部分失败：NuGet 已发布、GitHub Release 上传失败等情况不得显示为“整个 Release 成功”。后续重试必须识别已发布的不可覆盖版本和已存在的附件，避免产生内容不一致的重复制品。

## 7. SDK：NuGet 发布规划

- SharpHarness SDK 的官方分发渠道是 **nuget.org**。首版对外发布哪些 \`SharpHarness.*\` 包及其依赖/公共 API 边界，**须单独评审决定**；T001 中的七个项目不等于必须全部对外发七个包。
- 包配置需包括稳定 \`PackageId\`、统一版本、\`PackageLicenseExpression=Apache-2.0\`、仓库 URL、README、符号和调试体验所需信息，按发布任务约定加入 SourceLink/符号包等。
- 在首次发布前确认 NuGet 包 ID 可用、发布账户/所有者、跨包依赖版本，以及发行说明。
- **推荐 nuget.org Trusted Publishing（GitHub Actions OIDC）**：发布 Workflow 的身份在 nuget.org 上按仓库所有者、仓库、Workflow 文件名（及可选 GitHub Environment）建立信任；发布 Job 仅在需要 OIDC 时使用 \`id-token: write\`，短期凭据不向 PR 暴露。
- 发布身份和可用性需在首次发包任务中现场验证；如账户不支持 Trusted Publishing，再审议备用凭据流程，不将长期 API Key 作为默认选择。
- \`SharpHarness.Cli\` 是否作为 NuGet 包、\`.NET tool\` 或独立安装程序，需要在 CLI 形态确定后再决定。

## 8. Coding Agent / TUI：跨平台分发规划

TUI 是最终用户运行的应用，**不是将三平台 SDK 构建等同于三个安装包**。

候选初期 RID / Release Asset：

| 系统 | RID | 首选形式 |
| --- | --- | --- |
| Windows x64 | \`win-x64\` | ZIP（可执行文件及必要资源） |
| Linux x64 | \`linux-x64\` | tar.gz |
| Linux ARM64 | \`linux-arm64\` | tar.gz |
| macOS Intel | \`osx-x64\` | tar.gz |
| macOS Apple Silicon | \`osx-arm64\` | tar.gz |

- 首版倾向使用 \`.NET 10\` **Self-contained + Single-file** 发行，明确包括运行时并验证实际产物组成。ZIP/tar.gz 属于可下载发行包，**不等同于原生 MSI/PKG/DEB/RPM 安装器**。
- 通过 RID Matrix 进行 \`dotnet publish\`，附加 SHA-256 校验文件和版本清单；在适合的原生 Runner 上进行解包、启动、\`--version\` 等 Smoke Test。没有覆盖的 RID/系统应明确标注 **Not Verified**。
- macOS ARM64 和 x64、Linux 两个架构分开验证。T001 的 \`macos-latest\` CI 仅测试 macOS ARM64，不得将其宣称为 Intel macOS 已验证。
- 代码签名、公证（macOS notarization）、Windows 签名、Homebrew/WinGet/DEB/RPM 等是后续独立发布任务；先保障可运行压缩包的正确性、可信下载路径和可追溯性。
- **Native AOT、Trimming、跨平台自带原生库的兼容性**需要专项验证，不能仅为缩小二进制而提前强制启用。SDK 的动态插件/反射能力不应被 TUI 发布方式反向限制。

## 9. Workflow 拆分策略与触发条件

| 候选 Workflow | 触发条件 | 责任 | 当前状态 |
| --- | --- | --- | --- |
| \`ci.yml\` | PR 到 \`main\` / 明确集成目标；\`main\` push | 三平台 restore/build/test | T001 PR #3 中已实现并有三 OS 证据；**尚未合入** |
| \`pr-quality.yml\`（或合并至 \`ci.yml\`） | PR | 格式、Analyzer、架构边界和独立审阅 | 规划，G001 后续阶段 |
| \`release.yml\` | 受信任版本 Tag（可选受控手动入口） | 验证 Tag、构建、测试、产物验证、发行协调 | 规划，首次发行前 |
| \`publish-nuget.yml\`（可作为 Release Job） | 仅由受控 Release 调用 | 发行批准后向 nuget.org 推送 | 规划，首次 SDK 发行前 |
| \`publish-app.yml\`（可作为 Release Job） | 仅由受控 Release 调用 | TUI 按 RID 构建、冒烟测试、上传 Releases | 规划，TUI 可发布时 |
| \`nightly.yml\` | 定时（可选） | 长期/兼容性/耗时集成测试 | 仅在需要时创建 |

不要求一开始就创建六个 YAML。首次发布可以只用 \`release.yml\` 中的多个 Job，确有重复和独立维护需求时再拆成可复用 Workflow。PR 验证和正式发布必须采用隔离的权限及触发器。

## 10. 安全与供应链底线

- GitHub Action 使用经过审查的固定 SHA 或明确更新机制；第三方工具版本、NuGet SDK 版本与依赖变更应可追溯。
- PR Workflow 默认 \`permissions: contents: read\`；不使用持有高权限密钥的 \`pull_request_target\` 去直接构建不可信 PR Head。
- OIDC / Release token 只在已授权 Release Job 中申请，按需要授予 \`id-token: write\` 和必要的 Releases 写入权限，其他 Job 均不继承写权限。
- 制品关联完整版本、Git Commit SHA、构建环境、测试结果和 SHA-256 摘要。签名、SBOM、证据证明可在成熟阶段逐步增强。
- 同一版本的 NuGet 与 TUI 包须源自被批准的相同 Tag/SHA；某一渠道未完成时明确标记部分失败，不悄悄替换已经发布的产物。
- 不允许 CI、机器人、Coding Agent 自动合并实现 PR；发布不自动创造下一阶段开发授权。

## 11. 失败、回滚与紧急修复

| 情况 | 处理方向 |
| --- | --- |
| PR 必需检查失败/缺失 | GitHub 阻止 Merge；修复 PR 后重新运行检查。 |
| \`main\` 合并后发现回归 | Owner 批准修复或 Revert PR；重新走受保护主干的验证。 |
| Release 验证失败（尚未公开） | 停止发布，不创建对外发行记录；修复后从审核过的新版本候选重试。 |
| NuGet 已公开，发现严重缺陷 | 按 nuget.org 规则弃用/警告受影响版本并发布修复版；不得覆盖原版本。 |
| TUI 已发布，发现缺陷 | 将有问题的版本标为不推荐或回退下载入口；通过新标签发布修复包，保留审计记录。 |

不假定可以安全“撤销”已经被外部用户下载的 NuGet 版本或终端程序。

## 12. 分阶段落地顺序与验收

### 阶段 A：设计确定（本 PR）

- [ ] 负责人审查本总体设计，确认默认单级 PR、明确多级例外、\`main\` 必需门禁、Tag 发版以及分发边界。
- [ ] 本文档通过独立文档 PR 合入 \`main\`；**不改动 T001 PR #3，不变更 Actions/Rulesets，也不触发正式发布**。

### 阶段 B：G001 基础质量门禁（优先于 T001 最终合入 \`main\`）

- [ ] 审视 T001 的 \`ci.yml\` 实际目标 PR 和最新 SHA，确认三平台 Check 名称/来源及启动条件。
- [ ] 配置一次长期 \`main\` Ruleset：PR required、三项 OS status checks required、禁止 force push/deletion；Owner 不被自动授予合并权。
- [ ] 使用可撤销的验证 PR，分别核实 Check **缺失/失败会阻止合并**，通过后才能进入授权合并环节。
- [ ] **T001 特例**：是否额外保护 \`feat/t001-dotnet10-bootstrap\` 以阻止第一级 PR 的失败合并，需在 G001 实施方案中做一次明确决定；不要求每个未来 \`feat/*\` 配规则。
- [ ] 复核规则适用范围、管理员 Bypass、Workflow 可触发性。当前 GitHub 连接器没有仓库管理员写入权限，Ruleset 须由 Owner 在 Settings 配置并提供实际验证证据。

### 阶段 C：独立审查及合并 T001

- [ ] 独立完成 T001-A01～A08 验收并核对新增 C# 源码的 Apache-2.0 版权头（如果本次有新增 C# 文件）。
- [ ] 经 Owner 明确授权后执行 PR #3 → T001 集成分支；再创建单独的 T001 集成分支 → \`main\` PR，并以主干 Required Checks 为最终入库门禁。
- [ ] 确认最终合并和 CI 结果后清理临时任务分支；不自动开始 M2/M3。

### 阶段 D：增强自动审阅（G001 后续）

- [ ] 增加格式/静态分析等确定性 Job，验证其能正确失败后再设为 Required。
- [ ] 按实际代码安全需求增加 CodeQL；AI Review 仅在费用和权限获批准后接入。

### 阶段 E：首次 NuGet 发行

- [ ] 批准 NuGet 对外包列表和版本策略，注册/核验 Package IDs 与 Trusted Publishing 配置。
- [ ] 建立受控 \`release.yml\`；测试 NuGet Pack 元数据、可信身份和实际发布流程；正式发布需要独立版本授权。

### 阶段 F：首次 CLI/TUI 多平台发行

- [ ] 选定终端 UI/可执行项目和首批 RID；实现发行矩阵、可运行冒烟测试、制品校验和 GitHub Release。
- [ ] 签名、公证、系统包管理器发行等另行决策，避免在 T001/G001 中预支工作。

## 13. 当前仓库实况（快照，2026-10-23）

- \`main\` 已含 Apache License 2.0（PR #5 merged）；还没有 T001 工程骨架。
- T001 PR #3 为 Draft，源 \`cursor/t001-dotnet10-scaffold-64b7\`，目标 \`feat/t001-dotnet10-bootstrap\`，未合并。
- [T001 三平台 CI Run 39742956682](https://github.com/SharpHarness/SharpHarness/actions/runs/39742956682) 在 2026-10-22 成功，但不能据此宣布 T001 独立验收完成。
- \`main\` 和 T001 临时集成分支未启用 GitHub 分支保护，仓库没有启用的 Ruleset。
- G001 Issue #4 尚未实施；NuGet/Release/TUI Workflow 尚未实施。本文件不改变上述状态。

## 14. 参考资料

- [GitHub: Available rules for rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets)
- [GitHub: Workflow events and filters](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)
- [GitHub: Deployment environments and approvals](https://docs.github.com/en/actions/concepts/workflows-and-actions/deployment-environments)
- [NuGet: Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
- [.NET: Publish deployment models](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [.NET: Single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/)
