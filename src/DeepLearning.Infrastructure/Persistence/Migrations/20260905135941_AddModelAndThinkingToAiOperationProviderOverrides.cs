using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelAndThinkingToAiOperationProviderOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "ai_operation_provider_overrides",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "thinking_enabled",
                table: "ai_operation_provider_overrides",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "model",
                table: "ai_operation_provider_overrides");

            migrationBuilder.DropColumn(
                name: "thinking_enabled",
                table: "ai_operation_provider_overrides");
        }
    }
}
