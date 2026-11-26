using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Authentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Người dùng tạo trước khi có đăng nhập: gán email tạm duy nhất để cột có thể NOT NULL + UNIQUE.
            migrationBuilder.Sql("UPDATE devagent.users SET email = 'user-' || id::text || '@invalid.local' WHERE email IS NULL;");
            migrationBuilder.Sql("UPDATE devagent.users SET email = lower(email);");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "devagent",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failed_login_count",
                schema: "devagent",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_login_at",
                schema: "devagent",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_ends_at",
                schema: "devagent",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                schema: "devagent",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "devagent",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                schema: "devagent",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "system_role",
                schema: "devagent",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "devagent",
                table: "users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // Mỗi người dùng cũ cần security stamp riêng (đổi stamp làm token cũ mất hiệu lực).
            migrationBuilder.Sql("UPDATE devagent.users SET security_stamp = replace(gen_random_uuid()::text, '-', '') WHERE security_stamp = '';");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "devagent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    replaced_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "devagent",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "devagent",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_system_role_is_active",
                schema: "devagent",
                table: "users",
                columns: new[] { "system_role", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                schema: "devagent",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id_revoked_at",
                schema: "devagent",
                table: "refresh_tokens",
                columns: new[] { "family_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "devagent",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_revoked_at",
                schema: "devagent",
                table: "refresh_tokens",
                columns: new[] { "user_id", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "devagent");

            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_system_role_is_active",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "failed_login_count",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_ends_at",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "system_role",
                schema: "devagent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "devagent",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "devagent",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);
        }
    }
}
