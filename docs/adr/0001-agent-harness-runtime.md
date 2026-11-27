# ADR-0001 · Evolve the fixed pipeline into an Agent Harness runtime

- **Status** · Proposed, pending project-owner approval
- **Date** · 2026-11-26

## Context

The current DevAgent pipeline advances through hand-written `IWorkflowStep` implementations selected by `AgentOrchestrator`. The Planner uses a fixed prompt. Configuration selects one AI provider for the system. The existing PostgreSQL queue, leases, outbox, budgets, policy evaluation, and approvals provide a tested foundation.

The Agent Harness proposal adds versioned user-defined agents, multiple providers and models, bounded tool loops, workflow graphs, and coordination between agents. See SRC-002, SRC-006, ARC-001, and ARC-002 in the [audit reports](../audit/SOURCE_CODE_AUDIT.md).

## Options

1. Replace the pipeline with a new engine. This discards verified behavior and increases migration risk.
2. Base the runtime on a framework such as Semantic Kernel or Microsoft.Extensions.AI. A framework could reduce provider integration work. DevAgent would still own authorization, budgets, checkpoints, and approvals.
3. Extend the current platform incrementally. Add the new concepts alongside the pipeline and migrate the Planner once the new path has equivalent behavior.

## Proposed decision

Use option 3.

- Add a Providers module with `Provider`, `ProviderCredential`, and `Model`. Encrypt credentials using `ISecretProtector`. Extend `IModelClient` toward `IChatModelProvider` for chat, structured results, tool calls, and failure classification, including ambiguous responses that may incur charges. Adapt Fake and Anthropic first. Evaluate Microsoft.Extensions.AI during implementation if it reduces code.
- Evolve `AgentProfile` into `AgentDefinition` and add immutable `AgentVersion` records for instructions, tools, model, limits, result schema, and approval policy. Each new execution identifies its agent version.
- Load run state, assemble bounded context, reserve budget, call the model, validate output, authorize requested tools, obtain approval where required, execute, record evidence, and checkpoint. Bound rounds, tool calls, tokens, cost, duration, and cancellation. Backend code owns permission, state, budget, and approval decisions.
- Introduce a tool registry with schema, permissions, risk, timeout, side effects, and idempotency. Start with reads and search. Add writes and command execution after sandbox isolation is available in M4.
- Introduce a separate workflow graph aggregate in M3. Keep `WorkflowDefinition` for per-repository policy. Run each durable graph node using the existing PostgreSQL queue and a node idempotency key.
- Package the task pipeline and Planner as templates and preserve the task state machine.

The old and new paths will coexist during migration. Owning the runtime requires more implementation work and lets the project enforce and record its own budgets, checkpoints, approvals, and audits.

## Migration

1. Add tables for agent versions, providers, provider credentials, models, model and tool invocations, and execution events. Add a nullable `agent_executions.agent_version_id`.
2. Seed a provider and model from configuration and Planner Agent v1 with the current prompt.
3. Route `PlanningStep` through the new runtime when enabled. Verify equivalent behavior with the current Planner tests.
4. Retire the old Planner implementation after equivalence is established.

## Rollback

Keep the original execution path selectable during migration via configuration. Leave the additive schema in place while reverting the application to the old path.
