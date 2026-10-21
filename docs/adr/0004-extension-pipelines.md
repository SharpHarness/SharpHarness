# ADR-0004: Typed, Stage-Specific Extension Pipelines

- **Status:** Accepted
- **Date:** 2026-10-19
- **Decision authority:** Product owner, approved in architecture review on 2026-10-19

## Context

One universal middleware chain is easy to introduce but hard to govern: ordering conflicts and skipped authorization become possible.

## Decision

Adopt **typed, stage-specific extension pipelines** instead of a universal middleware mechanism. The Core controls lifecycle, stage order and mandatory execution checkpoints; standard runtime services are composed through explicit interfaces and Dependency Injection at the host composition root.

- **Context stage:** context contributors supply candidate information; the context builder controls priority, deduplication, budget and immutable model-request assembly. Contributors must not mutate Session facts or elevate untrusted input to instructions.
- **Model stage:** model clients/adapters implement provider-specific transport and streaming, normalized capability-aware events, and structured errors. Core must not branch on specific model vendors or silently discard multimodal data.
- **Tool stage:** registry, validation, policy, optional approval, controlled execution, and result normalization have explicit typed boundaries. **No extension can skip, reorder or override mandatory authorization checkpoints**; the full authorization decision is governed by ADR-0006.
- **Event/observation stage:** observers subscribe to typed notifications for UI, logging, tracing and metrics without owning mutable Session state. Best-effort telemetry failures may be isolated; **durable journal writers are NOT optional observers** and their failure must follow persistence guarantees.

Each extension may influence only its documented stage; the Core enforces all cross-stage invariants. Do not create a global `IMiddleware` around the Agent Loop, an unrestricted service locator, or a generic Hook capable of bypassing validation/authorization.

**Scope limit:** v0.1 supports in-process .NET composition via explicit DI registration. Dynamic assembly hot-loading and Native AOT compatibility are not guaranteed in v0.1. Exact interfaces, ordering among contributors, lifetimes, exceptions and failure policies remain proposals to be fixed in the API contract specification before implementation.

## Consequences / tests

Predictable instrumentation and replacements at the cost of maintaining specialized contracts. Verify DI replacement of each standard service without changing Core, deterministic contributor ordering, isolated telemetry failures, explicit non-optional persistence failure, no mutation of Session state from observers, and that no plugin/adapter can bypass Tool authorization. Preserve provider-specific multimodal/reasoning semantics without assuming a single vendor protocol.

## Alternatives

Generic `IMiddleware` around everything or service locator; rejected because mandatory gates become difficult to guarantee.
