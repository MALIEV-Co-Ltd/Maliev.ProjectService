using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPartDfmWarningState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_dfm_warnings",
                table: "project_parts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_dfm_warnings",
                table: "project_parts");
        }
    }
}
