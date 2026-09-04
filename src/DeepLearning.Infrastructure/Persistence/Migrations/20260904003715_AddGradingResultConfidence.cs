using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Two additive columns on grading_results, both nullable so every existing row stays valid.
    ///
    /// The verdict stage of the three-stage grading pipeline
    /// (rebuild_grading_prompt_three_stage.sql) now reports how firmly it settled on a band and
    /// which band it considered second-best. <c>confidence</c> also feeds
    /// GradeSubmissionCommandHandler.EstimateDimensionPassProbability, which replaced the
    /// AI-supplied estimated_pass_probability — that is what stops two dimensions sitting at the
    /// same band-to-threshold gap from being handed the identical number.
    ///
    /// Rows written before this migration keep NULL in both: the frontend renders the confidence
    /// note only when it is present, and an alternative_band equal to band means "no genuine
    /// second choice", which is distinct from NULL ("this row predates the field").
    /// </summary>
    public partial class AddGradingResultConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "alternative_band",
                table: "grading_results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confidence",
                table: "grading_results",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_grading_results_alternative_band_range",
                table: "grading_results",
                sql: "alternative_band IS NULL OR alternative_band BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_grading_results_alternative_band_range",
                table: "grading_results");

            migrationBuilder.DropColumn(
                name: "alternative_band",
                table: "grading_results");

            migrationBuilder.DropColumn(
                name: "confidence",
                table: "grading_results");
        }
    }
}
