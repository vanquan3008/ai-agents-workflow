using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The project-scoped task FK below would fail on rows whose repository belongs to another project.
            // Stop with an actionable message instead of a constraint error so those rows can be fixed first.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM devagent.tasks t
                        JOIN devagent.code_repositories r ON r.id = t.repository_id
                        WHERE r.project_id <> t.project_id) THEN
                        RAISE EXCEPTION 'tasks.repository_id references a repository of another project; fix those rows before applying ItDepartments';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_tasks_code_repositories_repository_id",
                schema: "devagent",
                table: "tasks");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_code_repositories_project_id_id",
                schema: "devagent",
                table: "code_repositories",
                columns: new[] { "project_id", "id" });

            migrationBuilder.CreateTable(
                name: "it_department_revision_repositories",
                schema: "devagent",
                columns: table => new
                {
                    repository_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_it_department_revision_repositories", x => new { x.project_id, x.department_id, x.version, x.repository_id });
                    table.ForeignKey(
                        name: "fk_it_department_revision_repositories_repository",
                        columns: x => new { x.project_id, x.repository_id },
                        principalSchema: "devagent",
                        principalTable: "code_repositories",
                        principalColumns: new[] { "project_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "it_department_revision_rooms",
                schema: "devagent",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    asset_preset = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_it_department_revision_rooms", x => new { x.project_id, x.department_id, x.version, x.room_id });
                    table.CheckConstraint("ck_it_department_revision_rooms_action", "action IS NULL OR (stage IS NULL AND action IN ('OpenTasks', 'OpenApprovals', 'OpenProject', 'OpenWorkflow'))");
                    table.CheckConstraint("ck_it_department_revision_rooms_geometry", "x >= 0 AND y >= 0 AND width > 0 AND height > 0 AND x + width <= 24 AND y + height <= 16");
                    table.CheckConstraint("ck_it_department_revision_rooms_label_not_blank", "btrim(label) <> ''");
                    table.CheckConstraint("ck_it_department_revision_rooms_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_it_department_revision_rooms_stage", "stage IS NULL OR stage IN ('Planning', 'Development', 'Testing', 'Review', 'Approval', 'Publishing')");
                });

            migrationBuilder.CreateTable(
                name: "it_department_revisions",
                schema: "devagent",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_it_department_revisions", x => new { x.project_id, x.department_id, x.version });
                    table.CheckConstraint("ck_it_department_revisions_name_not_blank", "btrim(name) <> ''");
                    table.CheckConstraint("ck_it_department_revisions_theme", "theme IN ('Indigo', 'Emerald', 'Amber', 'Slate')");
                    table.CheckConstraint("ck_it_department_revisions_version_positive", "version > 0");
                    table.ForeignKey(
                        name: "fk_it_department_revisions_published_by",
                        column: x => x.published_by,
                        principalSchema: "devagent",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "it_departments",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    edit_version = table.Column<int>(type: "integer", nullable: false),
                    draft_config = table.Column<string>(type: "jsonb", nullable: true),
                    published_version = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_it_departments", x => x.id);
                    table.UniqueConstraint("ak_it_departments_project_id_id", x => new { x.project_id, x.id });
                    table.CheckConstraint("ck_it_departments_draft_config_object", "draft_config IS NULL OR (jsonb_typeof(draft_config) = 'object' AND pg_column_size(draft_config) <= 65536)");
                    table.CheckConstraint("ck_it_departments_edit_version_positive", "edit_version > 0");
                    table.CheckConstraint("ck_it_departments_name_not_blank", "btrim(name) <> ''");
                    table.CheckConstraint("ck_it_departments_published_version_positive", "published_version IS NULL OR published_version > 0");
                    table.CheckConstraint("ck_it_departments_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_it_departments_created_by",
                        column: x => x.created_by,
                        principalSchema: "devagent",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_it_departments_project",
                        column: x => x.project_id,
                        principalSchema: "devagent",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_it_departments_published_revision",
                        columns: x => new { x.project_id, x.id, x.published_version },
                        principalSchema: "devagent",
                        principalTable: "it_department_revisions",
                        principalColumns: new[] { "project_id", "department_id", "version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_project_repository_created",
                schema: "devagent",
                table: "tasks",
                columns: new[] { "project_id", "repository_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_it_department_revision_repositories_repository",
                schema: "devagent",
                table: "it_department_revision_repositories",
                columns: new[] { "project_id", "repository_id" });

            migrationBuilder.CreateIndex(
                name: "ux_it_department_revision_rooms_sort_order",
                schema: "devagent",
                table: "it_department_revision_rooms",
                columns: new[] { "project_id", "department_id", "version", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_it_department_revision_rooms_stage",
                schema: "devagent",
                table: "it_department_revision_rooms",
                columns: new[] { "project_id", "department_id", "version", "stage" },
                unique: true,
                filter: "stage IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_it_department_revisions_published_by",
                schema: "devagent",
                table: "it_department_revisions",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "ix_it_departments_created_by",
                schema: "devagent",
                table: "it_departments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_it_departments_project_id_id_published_version",
                schema: "devagent",
                table: "it_departments",
                columns: new[] { "project_id", "id", "published_version" });

            migrationBuilder.CreateIndex(
                name: "ux_it_departments_project_slug",
                schema: "devagent",
                table: "it_departments",
                columns: new[] { "project_id", "slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_code_repositories_project_scope",
                schema: "devagent",
                table: "tasks",
                columns: new[] { "project_id", "repository_id" },
                principalSchema: "devagent",
                principalTable: "code_repositories",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_it_department_revision_repositories_revision",
                schema: "devagent",
                table: "it_department_revision_repositories",
                columns: new[] { "project_id", "department_id", "version" },
                principalSchema: "devagent",
                principalTable: "it_department_revisions",
                principalColumns: new[] { "project_id", "department_id", "version" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_it_department_revision_rooms_revision",
                schema: "devagent",
                table: "it_department_revision_rooms",
                columns: new[] { "project_id", "department_id", "version" },
                principalSchema: "devagent",
                principalTable: "it_department_revisions",
                principalColumns: new[] { "project_id", "department_id", "version" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_it_department_revisions_department",
                schema: "devagent",
                table: "it_department_revisions",
                columns: new[] { "project_id", "department_id" },
                principalSchema: "devagent",
                principalTable: "it_departments",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tasks_code_repositories_project_scope",
                schema: "devagent",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "fk_it_departments_published_revision",
                schema: "devagent",
                table: "it_departments");

            migrationBuilder.DropTable(
                name: "it_department_revision_repositories",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "it_department_revision_rooms",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "it_department_revisions",
                schema: "devagent");

            migrationBuilder.DropTable(
                name: "it_departments",
                schema: "devagent");

            migrationBuilder.DropIndex(
                name: "ix_tasks_project_repository_created",
                schema: "devagent",
                table: "tasks");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_code_repositories_project_id_id",
                schema: "devagent",
                table: "code_repositories");

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_code_repositories_repository_id",
                schema: "devagent",
                table: "tasks",
                column: "repository_id",
                principalSchema: "devagent",
                principalTable: "code_repositories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
