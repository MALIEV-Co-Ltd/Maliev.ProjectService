using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ProjectService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPartReorderState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bag_and_tag",
                table: "project_parts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "bodies_json",
                table: "project_parts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "body_count",
                table: "project_parts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificates",
                table: "project_parts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "dfm_acknowledged",
                table: "project_parts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "drawing_files",
                table: "project_parts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "glb_storage_path",
                table: "project_parts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_inserts",
                table: "project_parts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_threaded_holes",
                table: "project_parts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "insert_count",
                table: "project_parts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "insert_type",
                table: "project_parts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inspection_level",
                table: "project_parts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marking_text",
                table: "project_parts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marking_type",
                table: "project_parts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overlay_paths",
                table: "project_parts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "process_config",
                table: "project_parts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "roughness_code",
                table: "project_parts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "selected_body_index",
                table: "project_parts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplementary_files",
                table: "project_parts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "threaded_hole_count",
                table: "project_parts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "threaded_hole_spec",
                table: "project_parts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "thumbnail_large_gcs_path",
                table: "project_parts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "thumbnail_small_gcs_path",
                table: "project_parts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bag_and_tag",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "bodies_json",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "body_count",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "certificates",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "dfm_acknowledged",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "drawing_files",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "glb_storage_path",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "has_inserts",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "has_threaded_holes",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "insert_count",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "insert_type",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "inspection_level",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "marking_text",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "marking_type",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "overlay_paths",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "process_config",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "roughness_code",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "selected_body_index",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "supplementary_files",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "threaded_hole_count",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "threaded_hole_spec",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "thumbnail_large_gcs_path",
                table: "project_parts");

            migrationBuilder.DropColumn(
                name: "thumbnail_small_gcs_path",
                table: "project_parts");
        }
    }
}
