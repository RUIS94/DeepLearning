using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "llm_provider_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    thinking_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    effort = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    extra_settings = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_llm_provider_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_llm_provider_settings_provider_key",
                table: "llm_provider_settings",
                column: "provider_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_llm_provider_settings_single_active",
                table: "llm_provider_settings",
                column: "is_active",
                unique: true,
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_provider_settings");
        }
    }
}
