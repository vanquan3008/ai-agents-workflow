using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowsAndReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "workflow",
                schema: "devagent",
                table: "tasks",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "workflow_version",
                schema: "devagent",
                table: "tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_at",
                schema: "devagent",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reconciled_at",
                schema: "devagent",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "workflow",
                schema: "devagent",
                table: "code_repositories",
                type: "jsonb",
                nullable: false,
                // "{}" được đọc thành quy trình mặc định của hệ thống (phiên bản 0) cho các repo đã có.
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "workflow_version",
                schema: "devagent",
                table: "code_repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "model_calls",
                schema: "devagent",
                table: "agent_executions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "workflow_revisions",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_revisions_code_repositories_repository_id",
                        column: x => x.repository_id,
                        principalSchema: "devagent",
                        principalTable: "code_repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_revisions_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "devagent",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_unreconciled_dead_letters",
                schema: "devagent",
                table: "jobs",
                column: "dead_lettered_at",
                filter: "status = 'DeadLetter' AND reconciled_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_revisions_changed_by",
                schema: "devagent",
                table: "workflow_revisions",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_revisions_repository_id_version",
                schema: "devagent",
                table: "workflow_revisions",
                columns: new[] { "repository_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_revisions",
                schema: "devagent");

            migrationBuilder.DropIndex(
                name: "ix_jobs_unreconciled_dead_letters",
                schema: "devagent",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "workflow",
                schema: "devagent",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "workflow_version",
                schema: "devagent",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "dead_lettered_at",
                schema: "devagent",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "reconciled_at",
                schema: "devagent",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "workflow",
                schema: "devagent",
                table: "code_repositories");

            migrationBuilder.DropColumn(
                name: "workflow_version",
                schema: "devagent",
                table: "code_repositories");

            migrationBuilder.DropColumn(
                name: "model_calls",
                schema: "devagent",
                table: "agent_executions");
        }
    }
}
