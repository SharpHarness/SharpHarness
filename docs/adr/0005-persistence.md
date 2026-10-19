# ADR-0005: SQLite-Backed Append-Only Journal and Optional Snapshots

- **Status:** Accepted
- **Date:** 2026-10-19
- **Decision authority:** Product owner, approved revised proposal on 2026-10-19
- **Related:** ADR-0002 (Session Actor), ADR-0003 (Command/Event), ADR-0004 (typed stages)

## Context

A chat transcript alone cannot recover Turn, approval, command-deduplication, or tool side-effect states after a process crash. SharpHarness requires ordered durable facts, replay, safety around uncertain external operations, and cross-platform C# deployment without coupling Core to a storage engine. File-based JSONL + snapshots would require independently engineered crash-atomicity, write barriers, indexing, migration and multi-file consistency.

## Decision

1. **Storage abstraction:** `SharpHarness.Core` depends only on `ISessionStore`-style contracts from Abstractions. Database code belongs to Infrastructure. The public interface/DTO names, exact signatures and transaction schema are not yet finalized.
2. **Default local implementation:** use **SQLite via `Microsoft.Data.Sqlite`** for the first production-capable local Session Store. SQLite's native engine may ship as a NuGet runtime dependency; SharpHarness's own official runtime/adapter logic remains C#. No second JSONL *live* store implementation is required for v0.1. Human-readable JSONL export/import may be added separately.
3. **Durable representation:** store an append-only, ordered **Agent Journal** of durable facts with a monotonic per-Session sequence. **Optional versioned Snapshots** accelerate recovery but are derived caches, not a competing source of truth. Snapshot cursor/sequence must refer to committed journal facts.
4. **SQLite configuration:** require `PRAGMA journal_mode=WAL` and `PRAGMA synchronous=FULL` for the production-capable default store. Verify successful WAL activation, and apply/check connection-scoped durability settings for all writing connections. `foreign_keys=ON` is recommended where relational constraints are used. SQLite WAL itself is **not** the Agent Journal.
5. **Transactions and ordering:** logically related durable facts, Session version/sequence advancement, deduplication records and derived invocation indexes must remain atomic and mutually consistent. Snapshot writes must record their applied sequence coherently. Define conflict handling and read/replay cursor contract before implementation. A successful tool pre-execution intent commit is a **mandatory barrier** before dispatching an action capable of side effects. A failed/uncertain database commit cannot authorize a new side-effect action.
6. **Uncertain tool effects:** an external file write, shell or Git operation is **not** in the SQLite transaction. If a tool may have executed but no trustworthy completion record exists, restore as **Unknown**, do **not** automatically retry; reconcile using tool-specific checks or escalate. Do not claim exactly-once side-effect execution.
7. **Host and concurrency scope:** one Session has one logical state writer (ADR-0002). Multiple Sessions may be concurrent while SQLite still serializes write transactions. Database I/O must not block responsiveness of the Actor command mailbox. Use bounded, short transactions and document lock/busy behavior. Default WAL database is local; do **not** recommend direct multi-host network-filesystem sharing.
8. **Portability/security:** verify Windows, Linux and macOS publish/runtime loading of the native SQLite dependency. Native AOT is deferred to dedicated testing. Preserve schema versions, migrations, corruption detection, secure logging and safe backup/restore.

## Consequences

- **Benefits:** built-in transactional durability and unique-index/constraint support; fewer bespoke recovery and consistency mechanisms; operationally simple embedded local database.
- **Costs:** native SQLite dependency in the Infrastructure adapter, SQLite write contention, schema/migration lifecycle, WAL/checkpoint/backup considerations and a required cross-platform publish matrix.
- **Scope:** production-capable SQLite adapter is required by the first functional local-persistence milestone (M3), **not** by T001 bootstrap. M0 defines the contracts; M1 is only project skeleton/CI. An in-memory fake store can support deterministic M2 tests until the approved SQLite adapter is implemented.

## Alternatives considered

- **JSONL + JSON Snapshot as default:** easy to inspect and pure managed file I/O, but shifting multiple-record atomicity, crash barriers, deduplication, tail repair, locking and snapshot consistency to custom infrastructure. **Not selected** for v0.1 production store.
- **SQLite with `synchronous=NORMAL`:** potentially faster but can lose recently committed transactions after power/OS failures even when database consistency is retained. **Not selected** for the default durability-sensitive store.
- **Remote database / shared WAL:** out of v0.1 scope. Future adapters may target other storage systems under the same semantic contract.

## Acceptance evidence required

- Atomic journal+sequence+deduplication updates, including injected failure before commit and uncertain commit outcomes.
- Durable sequence correctness, immutable facts, duplicate CommandId policy, snapshot replay, migration/version errors and explicit corruption reporting.
- Tool intent committed before external side effect; no dispatch on commit failure; Unknown state after process crash between execution and terminal fact; no blind replay.
- SQLite WAL/FULL effective settings, writer contention/busy handling, WAL checkpoint/backup and restore, multiple Sessions, and platform dependency loading on Windows/Linux/macOS.
- Fault injection covers abrupt process exit; **do not claim true power-loss testing** without a matching test setup or hardware evidence.

**Outstanding contract design:** table/index schema, connection lifetime, multi-process file locking policy, busy timeout and retries, transaction boundaries, journaling retention/compaction, migration/backup procedure, encryption/retention policy and exactly which durable events accompany command receipts. These are to be specified before SQLite implementation rather than guessed by Coding Agents.
