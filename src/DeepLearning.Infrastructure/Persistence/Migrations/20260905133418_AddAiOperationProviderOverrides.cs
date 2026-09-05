using System;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiOperationProviderOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_operation_provider_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    operation_type = table.Column<AiOperationType>(type: "ai_operation_type_enum", nullable: false),
                    provider_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_operation_provider_overrides", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_operation_provider_overrides_operation_type",
                table: "ai_operation_provider_overrides",
                column: "operation_type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_operation_provider_overrides");
        }
    }
}
