using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectQuotationVersionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_quotation_version_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_quotation_version_number",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_project_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_project_number",
                table: "projects",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_projects_current_quotation_version_id",
                table: "projects",
                column: "current_quotation_version_id");

            migrationBuilder.CreateIndex(
                name: "idx_projects_source_project_id",
                table: "projects",
                column: "source_project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_projects_current_quotation_version_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "idx_projects_source_project_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "current_quotation_version_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "current_quotation_version_number",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "source_project_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "source_project_number",
                table: "projects");
        }
    }
}
