using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "devagent",
                table: "refresh_tokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "devagent",
                table: "refresh_tokens");
        }
    }
}
