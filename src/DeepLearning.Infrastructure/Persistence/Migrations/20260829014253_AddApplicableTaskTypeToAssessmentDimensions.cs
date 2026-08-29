using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicableTaskTypeToAssessmentDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TaskType>(
                name: "applicable_task_type",
                table: "assessment_dimensions",
                type: "task_type_enum",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "applicable_task_type",
                table: "assessment_dimensions");
        }
    }
}
