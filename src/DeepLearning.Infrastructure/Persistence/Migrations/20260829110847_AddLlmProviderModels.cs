using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmProviderModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "model",
                table: "llm_provider_settings");

            migrationBuilder.CreateTable(
                name: "llm_provider_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_llm_provider_models", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_llm_provider_models_provider_key_model",
                table: "llm_provider_models",
                columns: new[] { "provider_key", "model" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_llm_provider_models_single_current_per_provider",
                table: "llm_provider_models",
                column: "provider_key",
                unique: true,
                filter: "is_current = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_provider_models");

            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "llm_provider_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
