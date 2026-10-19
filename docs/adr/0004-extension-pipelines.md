# ADR-0004: Typed, Stage-Specific Extension Pipelines

- **Status:** Proposed
- **Date:** 2026-10-19

## Context

One universal middleware chain is easy to introduce but hard to govern: ordering conflicts and skipped authorization become possible.

## Proposal

Provide explicit extension contracts for ContextBuilder/Contributors, Model Client/Adapters, Tool Registry/Executor, Policy Evaluator and Event Observers. The Core owns stage ordering and execution safety points. Extensions can enrich inputs/outputs only within their documented scope; no hook may bypass validation or policy.

## Consequences / tests

Predictable instrumentation and replacements; some additional interfaces. Test that untrusted plugin metadata cannot cause a tool to skip authorization. Preserve provider-specific multimodal and reasoning semantics without imposing one vendor protocol.

## Alternatives

Generic `IMiddleware` around everything or service locator; rejected because mandatory gates become difficult to guarantee.
