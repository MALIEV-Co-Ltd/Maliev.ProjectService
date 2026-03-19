using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quotation_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    total_estimated_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "THB"),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    author_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_notes_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "project_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_number = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    thumbnail_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    process_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    material_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    finish_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    color = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tolerance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    threads_inserts = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    custom_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    volume_cm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    support_volume_cm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    surface_area_cm2 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    bounding_box_x = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    bounding_box_y = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    bounding_box_z = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    is_manifold = table.Column<bool>(type: "boolean", nullable: true),
                    ai_suggested_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    confirmed_unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    price_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pricing_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    pricing_strategy = table.Column<int>(type: "integer", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_parts", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_parts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_project_notes_project_id",
                table: "project_notes",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_project_parts_job_id",
                table: "project_parts",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "idx_project_parts_order_id",
                table: "project_parts",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_project_parts_project_id",
                table: "project_parts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_projects_customer_id",
                table: "projects",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_projects_project_number",
                table: "projects",
                column: "project_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_projects_status",
                table: "projects",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_notes");

            migrationBuilder.DropTable(
                name: "project_parts");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
