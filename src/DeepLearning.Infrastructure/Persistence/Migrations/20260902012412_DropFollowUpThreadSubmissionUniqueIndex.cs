using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropFollowUpThreadSubmissionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_follow_up_threads_submission",
                table: "follow_up_threads");

            migrationBuilder.CreateIndex(
                name: "ix_follow_up_threads_submission",
                table: "follow_up_threads",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_follow_up_threads_submission",
                table: "follow_up_threads");

            migrationBuilder.CreateIndex(
                name: "ux_follow_up_threads_submission",
                table: "follow_up_threads",
                column: "submission_id",
                unique: true);
        }
    }
}
