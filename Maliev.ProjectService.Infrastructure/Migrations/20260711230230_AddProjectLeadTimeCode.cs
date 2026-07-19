using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLeadTimeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lead_time_code",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "STANDARD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lead_time_code",
                table: "projects");
        }
    }
}
