using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeakPointCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "catalog_id",
                table: "weak_points",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detection_source",
                table: "weak_points",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "rule");

            migrationBuilder.AddColumn<string>(
                name: "evidence_note",
                table: "weak_points",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "exam_type_id",
                table: "weak_points",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolved_at",
                table: "weak_points",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "detected_band",
                table: "weak_point_occurrences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "snippet",
                table: "weak_point_occurrences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "exam_type_id",
                table: "standard_overrides",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "weak_point_catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    default_dimension_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    default_error_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weak_point_catalog", x => x.id);
                    table.ForeignKey(
                        name: "fk_weak_point_catalog_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_weak_points_catalog_id",
                table: "weak_points",
                column: "catalog_id");

            migrationBuilder.CreateIndex(
                name: "ix_weak_points_exam_type_id",
                table: "weak_points",
                column: "exam_type_id");

            migrationBuilder.CreateIndex(
                name: "ux_weak_points_user_catalog",
                table: "weak_points",
                columns: new[] { "user_id", "catalog_id" },
                unique: true,
                filter: "catalog_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_standard_overrides_exam_type_id",
                table: "standard_overrides",
                column: "exam_type_id");

            migrationBuilder.CreateIndex(
                name: "ux_weak_point_catalog_exam_code",
                table: "weak_point_catalog",
                columns: new[] { "exam_type_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_standard_overrides_exam_types_exam_type_id",
                table: "standard_overrides",
                column: "exam_type_id",
                principalTable: "exam_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_weak_points_exam_types_exam_type_id",
                table: "weak_points",
                column: "exam_type_id",
                principalTable: "exam_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_weak_points_weak_point_catalog_catalog_id",
                table: "weak_points",
                column: "catalog_id",
                principalTable: "weak_point_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_standard_overrides_exam_types_exam_type_id",
                table: "standard_overrides");

            migrationBuilder.DropForeignKey(
                name: "fk_weak_points_exam_types_exam_type_id",
                table: "weak_points");

            migrationBuilder.DropForeignKey(
                name: "fk_weak_points_weak_point_catalog_catalog_id",
                table: "weak_points");

            migrationBuilder.DropTable(
                name: "weak_point_catalog");

            migrationBuilder.DropIndex(
                name: "ix_weak_points_catalog_id",
                table: "weak_points");

            migrationBuilder.DropIndex(
                name: "ix_weak_points_exam_type_id",
                table: "weak_points");

            migrationBuilder.DropIndex(
                name: "ux_weak_points_user_catalog",
                table: "weak_points");

            migrationBuilder.DropIndex(
                name: "ix_standard_overrides_exam_type_id",
                table: "standard_overrides");

            migrationBuilder.DropColumn(
                name: "catalog_id",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "detection_source",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "evidence_note",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "exam_type_id",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "detected_band",
                table: "weak_point_occurrences");

            migrationBuilder.DropColumn(
                name: "snippet",
                table: "weak_point_occurrences");

            migrationBuilder.DropColumn(
                name: "exam_type_id",
                table: "standard_overrides");
        }
    }
}
