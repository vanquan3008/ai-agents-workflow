# DevAgent MVP checklist

The MVP accepts a bug report from Discord and returns a task ID. For an ordinary bug in an approved repository, DevAgent plans the fix, makes it on a dedicated branch inside an isolated sandbox, runs build and tests, and reviews the diff. When the gates pass, the bot opens a pull request (PR) and reports the link, change summary, and test evidence. A human decides whether to merge on GitHub.

For the pilot, repository owners define which paths may be changed automatically and which areas require approval. Changes involving authentication, authorization, migrations, credentials, CI/CD, or destructive actions require approval before execution and diff approval before publishing. If the policy cannot classify the change, pause for human review. Production deployment and expanding agent permissions are outside the pilot scope.

Mark an item complete once its evidence is available, such as an approved design, test result, audit entry, or PR link. The [README](../README.md) and [implementation roadmap](IMPLEMENTATION_ROADMAP.md) describe current progress.

## Phase 0 · Approve the pilot design

- [ ] **0.1 Define the pilot.** Identify who submits work and approves changes, the pilot repository, the Discord, GitHub Issue, and dashboard entry points, and each role's permissions. Select a reproducible bug, record the intended behavior, and define how to verify the fix.
- [ ] **0.2 Review architecture and data ownership.** Map API, Worker, dashboard, PostgreSQL, sandbox, AI provider, GitHub, and Discord. Identify the modules that own tasks, executions, repository access, approval decisions, and PR links. Trace a request through them. See the [current architecture](../README.md#current-architecture).
- [ ] **0.3 Review the domain model and relationships.** Cover projects and repositories, tasks and executions, tools, approvals, and PR links. Distinguish `AgentProfile`, `AgentInstance`, and `AgentExecution`. Record idempotency keys, relationships, queue and timeline indexes, UTC timestamps, and concurrency handling. See the [domain relationships](audit/ARCHITECTURE_REVIEW.md#domain-relationships).
- [ ] **0.4 Approve the task and approval lifecycle.** Map `PENDING` to `COMPLETED`, including automatic progression and plan or diff approval. Define the actor, guards, and persisted data for each transition, including rejection, clarification, failure, blocking, retry, timeout, and cancellation.
- [ ] **0.5 Design agent execution.** Define the pilot's sequential Planner, Coding, Testing, and Reviewer steps and the failed build or test path. Specify inputs, outputs, tool permissions, checkpoints, and exit conditions. Keep model selection behind an Application port with configurable adapters.
- [ ] **0.6 Approve API and dashboard contracts.** Define `/api/v1/` for registration, task creation, reads, cancellation, executions, and approval decisions. Cover DTOs, errors, pagination, idempotency, and SignalR events. Design task list, execution detail, and approval views.
- [ ] **0.7 Approve the threat model and limits.** Identify trust boundaries for users, repository and issue content, models, the sandbox, and credential storage. Specify isolation, network policy, tool permissions, approval gates, per-task budgets, timeouts, attempts, and concurrency.
- [ ] **0.8 Approve technology choices and the roadmap.** Record compatible .NET 10, EF Core/Npgsql, PostgreSQL, React 19, and integration SDK versions. Use PostgreSQL for pilot state and durable jobs and identify what measured load would justify Redis. Obtain sign-off for the scope, diagrams, domain model, policy, and acceptance scenarios.

## Phase 1 · Run the foundation from a clean checkout

- [ ] **1.1 Bootstrap backend and frontend.** Provide an ASP.NET Core API, a hosted Worker, explicit module registration, and the first migration. Keep the domain independent of frameworks and integration SDKs. Use React, Vite, strict TypeScript, feature folders, and a shared API client.
- [ ] **1.2 Set up development and CI.** Provide Docker Compose, readiness checks, example configuration, and separate secret storage. Build and test backend and frontend in GitHub Actions, check TypeScript, and apply migrations to PostgreSQL from scratch.
- [ ] **1.3 Implement identity and access.** Enforce project-, repository-, and action-level permissions. Map Discord and GitHub identities. Test submitter and approver permissions and access denials for outsiders. Apply the same project policy to reads and SignalR subscriptions.
- [ ] **1.4 Persist tasks and process durable jobs.** Store the task and job atomically. Lease jobs, record checkpoints, retry within limits, and retain failed jobs for operators. Verify recovery after Worker termination and concurrent leasing by two Workers.
- [ ] **1.5 Add observability and automated coverage.** Correlate Serilog logs and OpenTelemetry traces and metrics with task and execution IDs. Redact secrets. Run domain, API, permission, and PostgreSQL integration tests with deterministic fake AI.

**Acceptance.** Start the stack from a clean checkout and pass CI. Submitting the same request twice yields one task. A Worker restart preserves its progress and history. Users outside the project cannot read the task or subscribe to its events.

## Phase 2 · Deliver the pilot workflow

### 2A · Connect the API to the pilot repository

- [ ] Install a least-privilege GitHub App and register repositories by installation and project. Encrypt persisted credentials and keep short-lived integration tokens out of model context.
- [ ] Let authorized users submit tasks and receive an ID. Persist the request source, repository, base commit, and idempotency key. Produce a plan, verification criteria, and risks. Use `NEEDS_CLARIFICATION` for incomplete requests.
- [ ] Evaluate repository policy on the plan and before writes. Record allow or approval reasons. Stop before a sensitive operation and notify the approver via Discord. Bind decisions to the approver, time, scope, and plan revision.

### 2B · Apply and verify changes in the sandbox

- [ ] Use a workspace per run and an unprivileged container with CPU, memory, time, and network limits. Keep host mounts, the Docker socket, and production credentials out of the sandbox. Verify containment and cleanup on success, failure, timeout, and cancellation.
- [ ] Expose file read, search, create, edit, diff, build, and test tools within the sandbox. Validate inputs, permissions, paths, and limits. Return structured results and audit tool use. Require distinct permission and any applicable approval for deletion.
- [ ] Apply the plan on a task branch. Capture build and test commands, exit codes, and results. Review the diff and evidence, with findings assigned a severity. Block publication on failing verification or blocking findings. Charge each fix loop against the retry limit and budget.
- [ ] Record tokens, estimated cost, duration, and tools per execution. Check the remaining budget before each model call and limit its output. Record why a limit was reached. Cancel active work and prevent subsequent steps when a task is canceled.

### 2C · Publish the verified change

- [ ] Re-evaluate policy on the actual diff before publishing. Show the diff, base commit, verification results, review findings, and cost when approval is required. Bind approval to the reviewed commit and repeat checks and approvals after changes.
- [ ] Publish the verified commit once build, tests, review, and policy pass. Open a PR with the task link, summary, and evidence. Set `COMPLETED` after persisting the PR link. An authorized person merges on GitHub.
- [ ] If a Worker stops after GitHub creates a PR, find it by repository and task branch on retry and restore its link. Apply the same idempotency principle to webhook ingestion and notifications.

### 2D · Complete the entry points and dashboard

- [ ] Accept Discord task commands and provide task IDs, status, and cancellation. Notify users about approvals with a dashboard link, failures, and completed PRs.
- [ ] Accept eligible GitHub Issues. Verify webhooks and installation, repository, and actor permissions. Deduplicate events and reflect PR and CI state in the task.
- [ ] Create dashboard tasks through the same API. Show active, completed, failed, and approval states, with plans, timelines, tools, errors, costs, and PRs. Refresh via SignalR and restore persisted history on reconnect.
- [ ] Cover loading, empty, success, and error states, plus keyboard access for task creation and approvals. Add component tests and an end-to-end test from task creation to the PR.

## MVP acceptance scenarios

| Done | Scenario | Evidence |
| --- | --- | --- |
| ☐ | Submit an in-scope reproducible bug through Discord with a real GitHub App and model provider. | The regression test fails before the fix and passes after it. Discord receives one PR link. The task records verification, review, cost, and policy reasons. |
| ☐ | Submit independent requests through Discord, GitHub Issue, and the dashboard. | Every entry point provides a task ID, traceable source, progress, and correct outcome. |
| ☐ | Propose a sensitive change. Reject a plan or diff, or change the commit after approval. | The Worker stops before sensitive work and withholds publishing without approval for the current change. The task records the reason. |
| ☐ | Fail verification or record a blocking review finding. | The task retains the failure evidence and history. A PR requires passing checks and approval. |
| ☐ | Deliver a duplicate webhook, run two Workers, and stop a Worker just after PR creation. | Recovery leaves one task per request, one PR per task, and consistent history. |
| ☐ | Attempt an action without project permission or valid human approval. Include untrusted repository instructions in the case. | Backend policy denies unauthorized actions, records the denial, and keeps tokens out of logs. |
| ☐ | Cancel a running task, exceed its timeout, or hit its budget. | The run stops with a reason, the sandbox is removed, and publication is prevented. |
| ☐ | Disconnect and reconnect the dashboard. | The ordered timeline is restored. The task links to execution, tools, decisions, approvals, cost, and the PR. |

Release the pilot when these scenarios have evidence, deterministic CI passes, and the project owner accepts the sample PR after reviewing its diff. For each work item, record the goal, changes, affected files, tests and results, and pending work.

Post-MVP work includes multi-agent coordination, agent-to-agent handoffs, teams, Workflow Designer, a knowledge base, hierarchical budgets, production deployment, and organization-level isolation.
