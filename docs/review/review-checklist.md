# Architecture and Code Review Checklist

**Status:** Draft. Use during PR review. Verdicts: Pass / Changes Required / Rejected / Not Verified.

## Pre-review

- [ ] Read assigned task, accepted ADRs, contract version and existing code (not just diff).
- [ ] Confirm scope and file list; note unrelated changes.
- [ ] Check branch against current base and identify any conflicting changes.

## Architecture

- [ ] Public vs internal API clearly separated; no unnecessary abstraction or circular dependency.
- [ ] Core contains no provider, UI or Coding scenario knowledge.
- [ ] Session single-writer invariant enforced; no blocking waits for full Turn inside actor mailbox loop.
- [ ] Commands, durable events and UI stream notifications distinguished.
- [ ] Backpressure, cancellation and budget semantics documented and tested.

## Model/Tool correctness and safety

- [ ] Streaming ToolCall validated before execution; all IDs correlated.
- [ ] Provider capabilities respected without silent loss of multimodal content.
- [ ] Every tool passes schema validation, policy, approval and controlled execution.
- [ ] Untrusted tool/plugin/remote output cannot acquire privileges.
- [ ] Files, paths, symlinks, subprocesses, network and credentials guarded by appropriate scope.
- [ ] No false claims of OS sandboxing or exactly-once tool execution.

## Persistence and failure

- [ ] Durable journal facts correctly ordered; state restoration deterministic.
- [ ] Side-effect actions never blindly retried on uncertain completion.
- [ ] Duplicate message/approval IDs handled safely.
- [ ] Logs redact secrets and handle large outputs.

## Tests and disposition

- [ ] Unit + integration test commands executed or explicitly Not Verified.
- [ ] Windows/Linux/macOS CI evidence checked for cross-platform changes.
- [ ] Tests cover negative paths, races, cancellation and abnormal termination.
- [ ] PR report includes commit SHA, commands, observed outputs, unresolved risks.
- [ ] Reviewer records precise change requests with file path/line/expected behavior.

## Review report template

```text
Task / commit / PR:
Specification & ADR versions:
Overall: Pass | Changes Required | Rejected | Not Verified
Blockers (with evidence):
Major findings:
Minor findings:
Executed checks (commands, platform, result):
Unverified items:
Acceptance decision & owner approval status:
```
