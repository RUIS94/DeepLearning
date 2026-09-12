using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserExamTypeActivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_exam_type_activations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_exam_type_activations", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_exam_type_activations_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_exam_type_activations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_exam_type_activations_exam_type_id",
                table: "user_exam_type_activations",
                column: "exam_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_exam_type_activations_user_id_exam_type_id",
                table: "user_exam_type_activations",
                columns: new[] { "user_id", "exam_type_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_exam_type_activations");
        }
    }
}
