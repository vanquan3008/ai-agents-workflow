# ADR-0002 · Add an IT Departments module for configurable office views

- **Status** · Proposed, pending project-owner approval
- **Date** · 2026-11-27

## Context

The [engineering office plan](../ENGINEERING_OFFICE_PLAN.md) adds project-scoped IT departments. Each department follows a set of the project's repositories and presents the fixed task pipeline as rooms in a 2D and later 3D office. Owners customize names, rooms, layout, and theme, and publish numbered revisions. The office reads existing task, execution, and approval data and never drives execution.

## Decision

- Add a `Departments` module in the modular monolith. It owns the tables `it_departments`, `it_department_revisions`, `it_department_revision_rooms`, and `it_department_revision_repositories` in the `devagent` schema. Other modules do not read these tables.
- `ItDepartment` is the aggregate for the mutable part: slug, directory name, private draft, `edit_version`, published pointer, and archive state. `ItDepartmentRevision` is an append-only snapshot written by publication. Restore copies a revision into a new draft, so published history never changes.
- Domain rules live in `DepartmentConfigurationRules`. Drafts must be structurally valid (bounds, unique room IDs and sort orders, at most one room per stage, approved presets). Publication additionally requires 6 to 12 rooms, one room for each of the six stages, at least one repository, and no overlapping rooms. PostgreSQL repeats the per-row bounds, uniqueness, and project ownership as named constraints.
- The module depends on Projects (authorization and repository ownership) and on the Tasks read model (department task list and single task). Tasks, Workflows, and the Worker do not depend on Departments. `WorkflowDefinition` and the Worker keep ownership of execution order, models, budgets, and policy.
- Add `ManageDepartments` to the permission matrix. Only Owners (and super admins) hold it. `ViewTasks` grants read access to published, active departments.
- Every write takes `expectedEditVersion`. `edit_version` is an EF concurrency token, and a competing revision insert violates the revision primary key. Both become HTTP 409.
- Published revisions are immutable, so the read path caches revision snapshots in process by `(department, version)`. Pointer and authorization checks still hit the database on every request, so revocation and archive apply immediately.

## Consequences

- Scoped composite foreign keys require a unique key on `code_repositories (project_id, id)`. The same key lets `tasks` reference its repository within the project.
- Department pages make one pointer query and use the cached snapshot for task scoping. The task list filters by `repository_id = ANY(...)` within the project before pagination, served by a new `(project_id, repository_id, created_at DESC)` index.
- Draft documents are bounded JSONB objects validated by the domain before saving.

## Rollback

The schema is additive. Removing the module means dropping its endpoints and frontend routes. The tables and the new task index can stay in place without affecting the task pipeline.
