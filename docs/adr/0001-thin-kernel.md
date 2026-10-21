# ADR-0001: Thin Kernel with Standard Runtime Services

- **Status:** Accepted (product owner selected option B)
- **Date:** 2026-10-19
- **Scope:** Overall SharpHarness architecture

## Context

The project must be both an embeddable general-purpose C# Agent Runtime and an immediately usable Coding Agent, without turning the kernel into a large framework tied to one UI or model vendor.

## Decision

Keep Core responsible for lifecycle, Agent Loop execution, command and event semantics, and fixed extension points. Context, Provider, Tool, Policy, Store, Telemetry and business Coding behavior are replaceable standard services, assembled through dependency injection at the host boundary.

## Consequences

Good: lightweight kernel, independent SDK, easier deterministic tests, future applications can reuse it. Tradeoff: clear interfaces and a consistent composition root are required. Avoid dynamic plugin complexity and unnecessary interfaces in M1.

## Alternatives considered

- Pi-style ultra-thin Loop-only Core: too much lifecycle/security behavior delegated to extensions.
- Monolithic Codex-style complete Core: simpler immediate packaging, but higher business/OS coupling.

**Implementation gate:** this ADR is approved at the architectural direction level; precise DTO and API signatures remain Proposed until their own review.
