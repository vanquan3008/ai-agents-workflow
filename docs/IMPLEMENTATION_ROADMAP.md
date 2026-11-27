# Agent Harness implementation roadmap

See the [current capability audit](audit/SOURCE_CODE_AUDIT.md) and [proposed ADR-0001](adr/0001-agent-harness-runtime.md). Preserve verified behavior as milestones advance. Keep builds and tests passing and apply schema changes with new migrations. Major changes require an approved ADR.

The [engineering office and workflow expansion plan](ENGINEERING_OFFICE_PLAN.md) proposes a one-repository pilot and a 2D and 3D dashboard view backed by the task lifecycle. PR 2 (custom departments and the 2D office) is implemented; see [ADR-0002](adr/0002-it-departments-module.md).

## M0 · Review the codebase

Completed. The review covers source, architecture, security, and frontend, with the validation record in the [source audit](audit/SOURCE_CODE_AUDIT.md).

## M1 · Stabilize the foundation

In progress.

| Item | Status | Reference |
| --- | --- | --- |
| Consistent nginx security headers | Done | SEC-001 |
| Keep SignalR access tokens out of nginx logs | Done | SEC-002 |
| Bind published PostgreSQL to loopback | Done | SEC-004 |
| Check `dotnet format` in CI | Done | SRC-010 |
| Distinct pending approval badge | Done | FE-002 |
| Remove broken source maps from production dashboard assets | Done | FE-008 |
| Invalidate SignalR subscriptions after access is revoked, close connections at token expiry | Done | SEC-010 |
| Preserve per-client authentication limits behind nginx | Open | SEC-011 |
| Rotate each refresh token at most once under concurrent refreshes; a different user agent inside the 30-second window still revokes the family | Done | — |
| Content Security Policy | Open | SEC-003 |
| Playwright end-to-end checks in CI | Open | FE-001 |
| Denial audit through an isolated unit of work | Open | SRC-011 |
| One user lookup per authorized request | Open | SRC-009 |

## M2 · Agent Harness core

Pending approval of [ADR-0001](adr/0001-agent-harness-runtime.md).

| Capability | Current state | Work |
| --- | --- | --- |
| Provider abstraction | Partial | Provider and model inventory, encrypted credentials, capabilities, structured output, tool calls, and error classification. |
| Agent registry and versioning | Partial | Versioned agent definitions, permissions, and management API. |
| Runtime loop | Partial | A bounded loop with checkpoints and cooperative cancellation. |
| Tool registry | Missing | Start with read-only repository snapshot tools. |
| Context manager | Missing | Assemble context within token limits and keep untrusted data separate from instructions. |
| Execution records | Partial | Model invocations, tool invocations, and ordered execution events. |
| Agent playground | Missing | Run published agents through the Fake or Anthropic adapter. |

M2 is complete when the current Planner can use the new runtime with equivalent test outcomes and users can create, test, and publish an agent through the dashboard.

## M3 · Workflow management

Add versioned workflow graphs, validation before publishing, durable execution on PostgreSQL jobs, retry and recovery, a visual workflow editor, and execution inspection. Package the current task pipeline as a template.

## M4 · Developer Agent

Add isolated workspaces and the Docker sandbox. Add repository-aware reads, build, test, diff, Coding, Testing, Reviewer, and Publisher, plus diff approval, PR creation and recovery, and a review interface. See SRC-001, SRC-008, and SEC-006.

## M5 · Multi-agent runs

Add parent and child executions, bounded delegation, structured handoffs, independent parallel work where workspaces permit it, result aggregation, and separate review.

## M6 · Production readiness

The pilot has OTLP telemetry, health checks, unprivileged application containers, and per-task budgets. Further work includes agent, workflow, and organization budgets, load and recovery testing, JWT key rotation (SEC-007), SSO (SEC-009), organization isolation (ARC-005), and operational runbooks.
