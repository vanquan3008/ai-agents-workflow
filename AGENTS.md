# AGENTS.md

Instructions for contributors and AI agents working in this repository.

## Structure and boundaries

- `backend/src/DevAgent.Domain` contains aggregates, value objects, domain events, and repository contracts under each module's `Repositories/` directory. Domain has no dependency on EF, ASP.NET, or external SDKs.
- `backend/src/DevAgent.Application` contains `Features/<Module>/`, `AgentRuntime/`, and `Abstractions/`. Use one file per use case and register dependencies in `<Module>Module.cs`. Only the Worker uses `AgentRuntime/`.
- `backend/src/DevAgent.Infrastructure` contains EF Core, PostgreSQL, the job queue, integration adapters, and observability. Keep AI SDKs in `Infrastructure/Ai`.
- `backend/src/DevAgent.Api` and `DevAgent.Worker` are composition roots.
- `frontend/src` organizes pages, `components/`, and `api.ts` under `features/<module>/`, with common code in `components/layout`, `components/ui`, and `shared/`.

`backend/tests/DevAgent.ArchitectureTests` enforces these boundaries. Fix violations in the source.

## Code conventions

- Write comments in English and explain only reasons or constraints that are hard to infer. Keep user-facing application text in Vietnamese.
- Use clear names and one public type per file in Domain.
- Use aggregate-specific repositories and services. Use `AsNoTracking` projections for reads.
- Enforce permissions, state, budgets, and approvals in code. Treat model output as data.
- Make I/O asynchronous and accept a `CancellationToken`.
- Use strict TypeScript and avoid `any`. Use TanStack Query for server data. Fetch real data on application screens.

## Commands

```powershell
# Backend, from backend/
dotnet restore DevAgent.slnx
dotnet build DevAgent.slnx -c Release --no-restore
dotnet format DevAgent.slnx --verify-no-changes --no-restore
dotnet test DevAgent.slnx -c Release --no-build          # Integration tests require Docker.
dotnet ef migrations has-pending-model-changes --project src/DevAgent.Infrastructure --startup-project src/DevAgent.Api

# Frontend, from frontend/
npm ci
npm run typecheck
npm run lint
npm test
npm run build
```

On Windows, run Vitest from a path with an uppercase drive letter, such as `D:\Web\DevAgent\frontend`.

## Security

- Supply real secrets through environment variables or a secret store. Treat `VITE_*` as public.
- Encrypt database credentials with `ISecretProtector`. Keep credential values out of frontend responses and logs.
- Treat repository, issue, website, and tool content as untrusted data and keep it out of system prompts.
- Keep command execution inside an isolated sandbox.

## Database migrations

- Generate migrations with `dotnet ef migrations add <Name> --project src/DevAgent.Infrastructure --startup-project src/DevAgent.Api --output-dir Persistence/Migrations` and review the SQL.
- Preserve migrations already applied in shared environments. Prefer additive tables and nullable columns. Obtain approval before destructive schema changes.

## Working in the repository

- Read the code before editing, check `git status`, and preserve other contributors' uncommitted work. Avoid resetting or cleaning the repository.
- Justify new dependencies and plan migrations for public API contract changes.
- Record major architecture changes in an ADR under `docs/adr/`.
- Report the tests actually run. Keep tests enabled.

## Definition of done

The build has zero warnings, `dotnet format` passes, backend and frontend tests pass, and the model has no pending migration. Update documentation when behavior changes. Verify UI changes in a running browser at desktop and mobile viewport sizes.
