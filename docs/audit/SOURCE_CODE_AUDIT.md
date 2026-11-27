# Source code audit · M0

Reviewed 2026-11-26 on `feat/devagent-foundation`, starting from `dc8b27c` and the working-tree refactor. DevAgent accepts engineering tasks, prepares plans, and routes sensitive proposals to an authorized reviewer. The assessment assumes a single-organization pilot with a small team and the default two concurrent task jobs.

See the [architecture review](ARCHITECTURE_REVIEW.md), [security review](SECURITY_REVIEW.md), [frontend review](FRONTEND_REVIEW.md), and [implementation roadmap](../IMPLEMENTATION_ROADMAP.md).

## Verification

| Check | Result |
| --- | --- |
| Backend restore and Release build | Succeeded with 0 warnings and 0 errors. |
| Backend tests | 151 passed. Domain 67, Application 43, Infrastructure 6, Worker 7, Architecture 7, and API integration 21. Integration tests used Testcontainers and PostgreSQL. |
| Backend formatting | `dotnet format DevAgent.slnx --verify-no-changes --no-restore` passed. |
| EF Core model | `dotnet ef migrations has-pending-model-changes` reported no changes. |
| Frontend | TypeScript, lint, 23 Vitest tests, and production build passed. Removing broken production source maps cleared the build warning. |
| Containers | Built API, Worker, and dashboard images. The isolated Compose stack started healthy with a fresh PostgreSQL database. |
| Planner smoke test | Ordinary, sensitive, and incomplete requests reached `BLOCKED`, `WAITING_FOR_APPROVAL`, and `NEEDS_CLARIFICATION` respectively. |
| Browser smoke test | Edge loaded the overview, task list, task detail, approvals, and project detail against the Compose stack. All five pages fit at 1,440 px and 390 px without page-level horizontal overflow. |

## Capability matrix

| Capability | State | Evidence |
| --- | --- | --- |
| Identity, JWT, refresh rotation, and account lockout | Implemented | `Api/Auth`, `Features/Auth`, and `AuthTests`. |
| Project authorization and denial audit | Implemented | `ProjectAccess` and `ProjectRolePermissions`. See SEC-010 for active realtime subscriptions. |
| Task lifecycle and append-only audit entries | Implemented | `AgentTaskStateMachine`, `AgentTaskTests`, and `Domain/Auditing/AuditEntry.cs`. |
| Durable queue, lease renewal, and dead-letter reconciliation | Implemented | `PostgresJobQueue`, `Worker/Jobs`, and `ReliabilityAndWorkflowTests`. |
| Worker concurrency and graceful shutdown | Implemented | `JobProcessingService` uses job-type limits and one service scope per job. |
| Notification outbox | Implemented | `Features/Notifications/TaskNotificationOutbox.cs`. |
| Model call, token, and USD controls | Implemented | `AgentRuntime/Budget/BudgetGuard.cs` and `WorkflowAndBudgetTests`. |
| Repository policy and plan-version approval | Implemented | `ChangePolicyEvaluator` and `Approval.ForPlan`. |
| Task realtime and persisted timeline | Implemented | `PostgresRealtime.cs`, `TasksHub.cs`, and the dashboard's API refetch. |
| Serilog, OpenTelemetry, OTLP, and sensitive-log redaction | Implemented | `Infrastructure/Observability`. |
| Model provider and Planner runtime | Partial | `IModelClient.CompleteAsync`, Fake and Anthropic adapters, and a fixed Planner. |
| Agent registry and versioning | Partial | `AgentProfile` and its table exist. The current Planner does not assign a profile. |
| Recovery checkpoints and tool records | Partial | Jobs advance by step. The runtime does not write `Checkpoint` or `ToolExecution` records. |
| Tools, sandbox, Coding, Testing, Reviewer, and Publisher | Missing | `UnavailableSandboxManager` and `PendingImplementationStep` mark those boundaries. |
| Diff approval, PR creation, and PR recovery | Missing | Domain and gateway contracts exist for later implementation. |
| Context management, memory, and repository intelligence | Missing | The Planner's direct context contains a bug description and repository file paths. |
| Workflow graph, evaluation, multi-agent dispatch, and organization isolation | Missing | The repository's `WorkflowDefinition` configures a fixed pipeline. Project membership is the current isolation boundary. |

## Findings

| ID | Priority | Evidence and concrete impact | Proposed change and acceptance |
| --- | --- | --- | --- |
| SRC-001 | High | `PendingImplementationStep` moves Coding, Testing, Reviewing, and Publishing states to `BLOCKED`. An ordinary task can produce a plan, then stops before a fix or PR. | Implement the sandbox and downstream steps in their roadmap order. Verify a pilot task reaches `COMPLETED` with a real PR and stored test evidence. |
| SRC-002 | High | `PlannerAgent.SystemPrompt` and step selection in `AgentOrchestrator` are fixed in code. Each new agent or execution shape requires application changes. | Add immutable agent versions and a bounded runtime under ADR-0001. Verify current Planner behavior and execution-to-version traceability. |
| SRC-003 | Medium | `AgentProfile` has an EF table, while `PlanningStep` starts executions without a profile. Runs cannot trace their agent configuration through this record. | Evolve the type into the versioned registry planned for M2. Verify management permissions and version links. |
| SRC-004 | Medium | `AgentExecution.SaveCheckpoint` and `BeginTool` have no production callers. The Worker resumes by step and execution detail cannot report tool calls. | After a runtime loop exists, record invocations and checkpoints. Verify a restart resumes without repeating a completed side effect. |
| SRC-005 | Medium | A task cancel updates persisted state. An in-flight Planner request receives the job cancellation token, which does not observe the task update. Up to one ongoing model call can continue and incur cost. | Link active work to task cancellation. Verify a canceled task interrupts a live model call and closes the execution. |
| SRC-006 | Medium | `IModelClient` returns a text completion and the provider is selected in settings. Agents cannot choose distinct providers or request native structured results and tools. | Add the provider registry and an encrypted credential model in M2. Verify two providers work and credentials stay out of API responses and logs. |
| SRC-007 | Medium | `WorkflowDefinition` stores configuration for the fixed pipeline. It cannot express user-defined nodes and edges. | Add a versioned graph in M3. Reject invalid graphs at publication and verify restart from a completed node. |
| SRC-008 | Low | Diff approval, PR links, and later task transitions have domain or port definitions awaiting downstream callers. | Keep the tested domain contracts for M4 and describe them as planned capabilities. Connect them through the application flow once those features exist. |
| SRC-009 | Low | JWT validation and `ProjectAccess` both look up the current user. An authorized project request can make two user queries. | Share the validated current user within the request scope. Verify one user lookup for a representative project request. |
| SRC-010 | Low · fixed | Earlier CI did not catch formatting errors. | `.github/workflows/ci.yml` now verifies formatting and the current tree passed. |
| SRC-011 | Low | `ProjectAccess.DenyAsync` saves through the shared request unit of work. A future caller that mutates an aggregate before checking permission could also save that mutation. | Isolate the denial audit transaction. Verify a denial saves its audit entry while leaving pending aggregate changes unpersisted. |
| SRC-012 | Low · fixed | Startup fatal messages used a different language from surrounding operational logs. | API and Worker startup fatal log messages now use English. |

## Verdict

The layer boundaries, durable planning flow, and verification suite support the current pilot. Prioritize revoking active realtime subscriptions when permissions change (SEC-010). The source, tests, and smoke run cover the existing Planner pilot. Real Anthropic, Discord, GitHub App, PR delivery, and load behavior remain outside this verification run.
