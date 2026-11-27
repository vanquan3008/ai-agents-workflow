using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "devagent");

            migrationBuilder.CreateTable(
                name: "agent_instances",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    host_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    active_jobs = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_instances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_profiles",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allowed_tools = table.Column<List<string>>(type: "text[]", nullable: false),
                    max_output_tokens = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    data_json = table.Column<string>(type: "jsonb", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    deduplication_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    leased_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    leased_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "code_repositories",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    installation_id = table.Column<long>(type: "bigint", nullable: false),
                    owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    policy = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_code_repositories", x => x.id);
                    table.ForeignKey(
                        name: "fk_code_repositories_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "devagent",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                schema: "devagent",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_members", x => new { x.project_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_project_members_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "devagent",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_identities",
                schema: "devagent",
                columns: table => new
                {
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_identities", x => new { x.user_id, x.provider });
                    table.ForeignKey(
                        name: "fk_user_identities_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "devagent",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    source_channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    base_commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    work_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    pr_number = table.Column<int>(type: "integer", nullable: true),
                    pr_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pr_head_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    pr_head_commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_tasks_code_repositories_repository_id",
                        column: x => x.repository_id,
                        principalSchema: "devagent",
                        principalTable: "code_repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "devagent",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "devagent",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_executions",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    agent_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    worker_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    usage_input_tokens = table.Column<long>(type: "bigint", nullable: false),
                    usage_output_tokens = table.Column<long>(type: "bigint", nullable: false),
                    usage_cache_read_tokens = table.Column<long>(type: "bigint", nullable: false),
                    usage_cost_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    checkpoint = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_executions_agent_profiles_agent_profile_id",
                        column: x => x.agent_profile_id,
                        principalSchema: "devagent",
                        principalTable: "agent_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_agent_executions_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "devagent",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approvals",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    plan_version = table.Column<int>(type: "integer", nullable: true),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sensitive_areas = table.Column<int>(type: "integer", nullable: false),
                    policy_version = table.Column<int>(type: "integer", nullable: false),
                    scope = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_approvals_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "devagent",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_plans",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    steps = table.Column<List<string>>(type: "text[]", nullable: false),
                    acceptance_criteria = table.Column<List<string>>(type: "text[]", nullable: false),
                    risks = table.Column<List<string>>(type: "text[]", nullable: false),
                    affected_paths = table.Column<List<string>>(type: "text[]", nullable: false),
                    policy_outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sensitive_areas = table.Column<int>(type: "integer", nullable: false),
                    policy_version = table.Column<int>(type: "integer", nullable: false),
                    policy_reasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_plans_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "devagent",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_status_transitions",
                schema: "devagent",
                columns: table => new
                {
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_status_transitions", x => new { x.task_id, x.sequence });
                    table.ForeignKey(
                        name: "fk_task_status_transitions_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "devagent",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tool_executions",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_json = table.Column<string>(type: "jsonb", nullable: false),
                    output_json = table.Column<string>(type: "jsonb", nullable: true),
                    exit_code = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    denial_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tool_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_tool_executions_agent_executions_execution_id",
                        column: x => x.execution_id,
                        principalSchema: "devagent",
                        principalTable: "agent_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_agent_profile_id",
                schema: "devagent",
                table: "agent_executions",
                column: "agent_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_status",
                schema: "devagent",
                table: "agent_executions",
                column: "status",
                filter: "status = 'Running'");

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_task_id_started_at",
                schema: "devagent",
                table: "agent_executions",
                columns: new[] { "task_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_task_id_step_attempt",
                schema: "devagent",
                table: "agent_executions",
                columns: new[] { "task_id", "step", "attempt" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_instances_status_last_heartbeat_at",
                schema: "devagent",
                table: "agent_instances",
                columns: new[] { "status", "last_heartbeat_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_instances_worker_id",
                schema: "devagent",
                table: "agent_instances",
                column: "worker_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_profiles_name",
                schema: "devagent",
                table: "agent_profiles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approvals_status_expires_at",
                schema: "devagent",
                table: "approvals",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_approvals_task_id_status",
                schema: "devagent",
                table: "approvals",
                columns: new[] { "task_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_outcome_occurred_at",
                schema: "devagent",
                table: "audit_entries",
                columns: new[] { "outcome", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_project_id_occurred_at",
                schema: "devagent",
                table: "audit_entries",
                columns: new[] { "project_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_task_id_occurred_at",
                schema: "devagent",
                table: "audit_entries",
                columns: new[] { "task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_code_repositories_installation_id",
                schema: "devagent",
                table: "code_repositories",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_code_repositories_owner_name",
                schema: "devagent",
                table: "code_repositories",
                columns: new[] { "owner", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_code_repositories_project_id",
                schema: "devagent",
                table: "code_repositories",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_deduplication_key",
                schema: "devagent",
                table: "jobs",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_leased_until",
                schema: "devagent",
                table: "jobs",
                column: "leased_until",
                filter: "status = 'Leased'");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_queued_available_at",
                schema: "devagent",
                table: "jobs",
                column: "available_at",
                filter: "status = 'Queued'");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_status_updated_at",
                schema: "devagent",
                table: "jobs",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_project_members_user_id",
                schema: "devagent",
                table: "project_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_slug",
                schema: "devagent",
                table: "projects",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_plans_task_id_version",
                schema: "devagent",
                table: "task_plans",
                columns: new[] { "task_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_status_transitions_occurred_at",
                schema: "devagent",
                table: "task_status_transitions",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_project_id_idempotency_key",
                schema: "devagent",
                table: "tasks",
                columns: new[] { "project_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_project_id_status_created_at",
                schema: "devagent",
                table: "tasks",
                columns: new[] { "project_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_repository_id",
                schema: "devagent",
                table: "tasks",
                column: "repository_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_requested_by",
                schema: "devagent",
                table: "tasks",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_status_updated_at",
                schema: "devagent",
                table: "tasks",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tool_executions_execution_id_sequence",
                schema: "devagent",
                table: "tool_executions",
                columns: new[] { "execution_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_identities_provider_external_id",
                schema: "devagent",
                table: "user_identities",
                columns: new[] { "provider", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_instances",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "approvals",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "project_members",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "task_plans",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "task_status_transitions",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "tool_executions",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "user_identities",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "agent_executions",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "agent_profiles",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "code_repositories",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "users",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "devagent");
        }
    }
}
