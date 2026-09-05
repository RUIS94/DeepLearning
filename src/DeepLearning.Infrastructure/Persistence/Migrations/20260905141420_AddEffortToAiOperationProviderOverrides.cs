using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEffortToAiOperationProviderOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "effort",
                table: "ai_operation_provider_overrides",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "effort",
                table: "ai_operation_provider_overrides");
        }
    }
}
