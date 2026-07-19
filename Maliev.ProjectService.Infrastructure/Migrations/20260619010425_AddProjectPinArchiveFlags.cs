using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPinArchiveFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_pinned",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_projects_customer_archive_pin_updated",
                table: "projects",
                columns: new[] { "customer_id", "is_archived", "is_pinned", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_projects_customer_archive_pin_updated",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_pinned",
                table: "projects");
        }
    }
}
