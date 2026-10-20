# Test Strategy and Acceptance Criteria v0.1

**Status:** Draft. Architecture/review agent designs independent acceptance; Coding Agent implements tests and runtime. Reviewer executes tests when an executable environment is available; otherwise reports **Not Verified**, not Pass.

## 1. Quality gates

1. Spec/ADR compliance and dependency boundary check.
2. `dotnet restore`, `dotnet build` with warnings-as-errors per approved task, `dotnet test` reproducible results.
3. Deterministic unit tests with Scripted/Fake Model, Fake Tool, Fake Journal/Clock.
4. Integration tests with temporary workspace and temporary Git repository; no secrets, internet or paid API needed.
5. CI on Windows, Linux and macOS; optional real-provider smoke tests isolated from blocking PR workflow.
6. Review explicitly assesses security, concurrency, recovery and compatibility, rather than counting coverage percentage alone.

## 2. Required test matrix (M2/M3)

| ID | Scenario | Expected acceptance evidence |
| --- | --- | --- |
| RT-001 | Model returns final answer | Exactly one terminal turn; expected events and message |
| RT-002 | One tool call then final model answer | Tool result correlated, ordered, fed back once |
| RT-003 | Multiple tool calls with out-of-order completion | Results ordered by CallIndex; no lost/crossed IDs |
| RT-004 | Invalid JSON/tool name | No execution; structured tool/protocol error |
| RT-005 | Policy deny or approval refuse | Executor not called, auditable rejection |
| RT-006 | Waiting approval + user Interrupt | Interrupt handled despite blocked tool; terminal state |
| RT-007 | Steer mid-execution | Constraint applied at next safe checkpoint only |
| RT-008 | Model/provider timeout and 429 | Classified correctly; bounded retry, no looping |
| RT-009 | Tool failure/output truncation | Explicit tool error/truncation, controlled continuation |
| RT-010 | Token/step/time budget | Terminates with exact limit reason |
| RT-011 | Two simultaneous Sessions | No state/message/event leakage between Sessions |
| RT-012 | Event multi-subscriber + slow subscriber | Both observe durable facts independently, no competing consumers; unsubscribing one does not stop other subscribers |
| RT-013 | Journal write fails before modifying tool | No external modification starts |
| RT-014 | Modification applied, terminal fact lost, then restore | Unknown detected; no automatic replay |
| RT-015 | Snapshot + journal replay | Identical reconstructed facts/state and monotonic sequence |
| RT-016 | Malicious tool output/prompt injection | Untrusted text cannot grant capabilities or bypass Policy via the managed Tool Pipeline; no unconditional claim of LLM prompt-injection immunity |
| RT-017 | Workspace path traversal/symlink escape | Disallowed operations rejected |
| RT-018 | Cancellation of process tree | No unmanaged child process remains where platform permits; exceptions documented |
| RT-019 | Serialize unknown event version | Explicit error/migration, no silent fallback |
| RT-020 | Provider streaming incomplete ToolCall | Must not execute incomplete args |
| RT-021 | Duplicate CommandId submission | No duplicate Turn or duplicated side effects; conflicting payload rejected per approved contract |
| RT-022 | Receipt semantics and cancellation | Receipt does not indicate Turn completion; submit wait cancellation does not interrupt accepted Turn |
| RT-023 | Durable cursor replay and ephemeral delta | Journal facts replay in sequence on reconnect when store configured; ephemeral deltas not promised |
| RT-024 | Busy StartTurn | Second StartTurn rejected Busy, no implicit queueing, current Turn unaffected |
| RT-025 | Stage extension isolation and ordering | Context/Model/Tool/Event contracts cannot mutate unrelated stages or reorder mandatory gates; injectable services are replaceable |
| RT-026 | Observer and journal failure boundaries | Telemetry observer failure can be isolated; durable journal failure cannot be silently ignored or treated as telemetry failure |
| RT-027 | SQLite transaction atomicity | Journal fact, Session Sequence, CommandId dedup and related state/index changes are all committed together or none are; injected failures observed |
| RT-028 | SQLite durability configuration | WAL enabled and verified, FULL synchronous applied/verified for writes; pre-tool durable intent confirmed before dispatch |
| RT-029 | SQLite writer contention and failure | Bounded busy/lock behavior and disk failure reported; Session Actor remains responsive |
| RT-030 | SQLite backup, restore, migration | Consistent WAL-aware backup/restore; snapshot+journal reconstruction; unknown schema version/corruption diagnosed |
| RT-031 | SQLite native deployment matrix | Windows/Linux/macOS deployment loads native SQLite and performs real read/write/restart test without paid providers |
| RT-032 | Tool policy deny or policy failure | Executor is never invoked; structured denial/failure recorded and no effective capabilities granted |
| RT-033 | Approval binding, stale and replayed decision | Modified args/scope/tool, expired, duplicate or post-cancel approval cannot invoke Executor |
| RT-034 | Tool invocation before durable intent | Side-effect Executor never starts until SQLite intent commit is confirmed; uncertain commit fails closed |
| RT-035 | Managed adapters and extension gate ordering | Built-in/Coding/MCP ToolInvocation goes through identical validation/policy/approval gates; no managed Hook reorders security stages |
| RT-036 | Sandbox claims / runtime boundary | Tests explicitly establish Policy is not OS isolation; untrusted in-process arbitrary .NET code excluded from protection claims |
| RT-037 | C01 exclusive Session ownership | Second same-Runtime OpenSession for same SessionId returns SessionAlreadyOpen with no second Actor; independent read-only subscriptions continue unaffected |
| RT-038 | C02 Accepted/Busy idempotent retry | Same SessionId+CommandId+payload returns original durable receipt across retry/restart, including an old Busy after Session becomes idle |
| RT-039 | C02 payload conflict and uncertain storage commit | Mismatched canonical payload conflicts without new action; storage failure/uncertain commit never yields a fabricated durable receipt or authorizes effects |

## 3. Evidence requirements

For each test: source commit SHA, OS/runtime, exact command, result summary, relevant logs (sanitized), known untested cases. Test files should avoid timing-sensitive sleeps and external dependencies, use injected clock/transport where possible. PR review decision recorded as Pass / Changes Required / Rejected / Not Verified.

## 4. Acceptance stages

- **M0 documentation:** architecture and ADR approved, no contradictory lifecycle rules, task boundaries clear.
- **M1 bootstrap:** correct project graph; build and trivial test suite run on all three OS; no unsupported claims of implementation.
- **M2 runtime:** RT-001–RT-012 and RT-020–RT-026 and RT-037–RT-039 pass against deterministic Fake Model/Tool/SessionStore; APIs reviewed. Fake/volatile persistence is **test-only**, not permission to run production side-effect tools without the durable SQLite intent barrier established by ADR-0005/0006.
- **M3 coding:** RT-013–RT-019 and RT-027–RT-036, plus realistic temporary-repository end-to-end scenarios, must pass for the first production-capable SQLite-backed Coding Agent; known security limitations listed. Risk-based CLI defaults must be separately approved before shipping capabilities; policy tests are mandatory without implying a completed OS Sandbox. Process-kill tests do not equal hardware power-loss validation.

Do not merge a change claiming a completed capability without matching test evidence. Manual inspection and CI status are different evidence types and must not be conflated.