using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAddressSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "selected_billing_address_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "selected_shipping_address_id",
                table: "projects",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "selected_billing_address_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "selected_shipping_address_id",
                table: "projects");
        }
    }
}
