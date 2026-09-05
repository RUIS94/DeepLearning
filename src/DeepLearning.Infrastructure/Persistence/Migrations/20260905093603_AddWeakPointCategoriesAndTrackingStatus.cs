using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeakPointCategoriesAndTrackingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_weak_point_catalog_exam_types_exam_type_id",
                table: "weak_point_catalog");

            migrationBuilder.DropIndex(
                name: "ux_weak_point_catalog_exam_code",
                table: "weak_point_catalog");

            migrationBuilder.DropColumn(
                name: "exam_type_id",
                table: "weak_point_catalog");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification,weak_point_detection_criteria,weak_point_recheck")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .Annotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .Annotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .Annotation("Npgsql:Enum:follow_up_thread_status_enum", "open,closed")
                .Annotation("Npgsql:Enum:followup_verdict_enum", "user_correct,user_incorrect,partial,pending")
                .Annotation("Npgsql:Enum:knowledge_item_type_enum", "sentence_pattern,vocab_expression,formula,concept,theorem,other")
                .Annotation("Npgsql:Enum:mastery_level_enum", "new,familiar,mastered")
                .Annotation("Npgsql:Enum:override_scope_enum", "grading_rubric,translation_reference")
                .Annotation("Npgsql:Enum:override_status_enum", "observing,active,deprecated")
                .Annotation("Npgsql:Enum:priority_enum", "high,medium,low")
                .Annotation("Npgsql:Enum:question_origin_enum", "ai_generated,user_uploaded,real_exam_seed")
                .Annotation("Npgsql:Enum:scale_type_enum", "band_1_5,score_0_100,rubric_level")
                .Annotation("Npgsql:Enum:source_type_enum", "real_exam,ai_generated,user_generated")
                .Annotation("Npgsql:Enum:subject_category_enum", "translation,language_arts,math,science,other")
                .Annotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned")
                .Annotation("Npgsql:Enum:task_type_enum", "A,B")
                .Annotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .Annotation("Npgsql:Enum:visibility_enum", "private,shared")
                .Annotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .Annotation("Npgsql:Enum:weak_point_status_enum", "active,resolved,tracking")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification")
                .OldAnnotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .OldAnnotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .OldAnnotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .OldAnnotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .OldAnnotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .OldAnnotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .OldAnnotation("Npgsql:Enum:follow_up_thread_status_enum", "open,closed")
                .OldAnnotation("Npgsql:Enum:followup_verdict_enum", "user_correct,user_incorrect,partial,pending")
                .OldAnnotation("Npgsql:Enum:knowledge_item_type_enum", "sentence_pattern,vocab_expression,formula,concept,theorem,other")
                .OldAnnotation("Npgsql:Enum:mastery_level_enum", "new,familiar,mastered")
                .OldAnnotation("Npgsql:Enum:override_scope_enum", "grading_rubric,translation_reference")
                .OldAnnotation("Npgsql:Enum:override_status_enum", "observing,active,deprecated")
                .OldAnnotation("Npgsql:Enum:priority_enum", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:question_origin_enum", "ai_generated,user_uploaded,real_exam_seed")
                .OldAnnotation("Npgsql:Enum:scale_type_enum", "band_1_5,score_0_100,rubric_level")
                .OldAnnotation("Npgsql:Enum:source_type_enum", "real_exam,ai_generated,user_generated")
                .OldAnnotation("Npgsql:Enum:subject_category_enum", "translation,language_arts,math,science,other")
                .OldAnnotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned")
                .OldAnnotation("Npgsql:Enum:task_type_enum", "A,B")
                .OldAnnotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .OldAnnotation("Npgsql:Enum:visibility_enum", "private,shared")
                .OldAnnotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .OldAnnotation("Npgsql:Enum:weak_point_status_enum", "active,resolved")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "detection_criteria",
                table: "weak_points",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "no_evidence_streak",
                table: "weak_points",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "occurrence_submission_count",
                table: "weak_points",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "weak_point_catalog",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "weak_point_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weak_point_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_weak_point_catalog_category_id",
                table: "weak_point_catalog",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_weak_point_catalog_code",
                table: "weak_point_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_weak_point_categories_code",
                table: "weak_point_categories",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_weak_point_catalog_weak_point_categories_category_id",
                table: "weak_point_catalog",
                column: "category_id",
                principalTable: "weak_point_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_weak_point_catalog_weak_point_categories_category_id",
                table: "weak_point_catalog");

            migrationBuilder.DropTable(
                name: "weak_point_categories");

            migrationBuilder.DropIndex(
                name: "ix_weak_point_catalog_category_id",
                table: "weak_point_catalog");

            migrationBuilder.DropIndex(
                name: "ux_weak_point_catalog_code",
                table: "weak_point_catalog");

            migrationBuilder.DropColumn(
                name: "detection_criteria",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "no_evidence_streak",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "occurrence_submission_count",
                table: "weak_points");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "weak_point_catalog");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .Annotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .Annotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .Annotation("Npgsql:Enum:follow_up_thread_status_enum", "open,closed")
                .Annotation("Npgsql:Enum:followup_verdict_enum", "user_correct,user_incorrect,partial,pending")
                .Annotation("Npgsql:Enum:knowledge_item_type_enum", "sentence_pattern,vocab_expression,formula,concept,theorem,other")
                .Annotation("Npgsql:Enum:mastery_level_enum", "new,familiar,mastered")
                .Annotation("Npgsql:Enum:override_scope_enum", "grading_rubric,translation_reference")
                .Annotation("Npgsql:Enum:override_status_enum", "observing,active,deprecated")
                .Annotation("Npgsql:Enum:priority_enum", "high,medium,low")
                .Annotation("Npgsql:Enum:question_origin_enum", "ai_generated,user_uploaded,real_exam_seed")
                .Annotation("Npgsql:Enum:scale_type_enum", "band_1_5,score_0_100,rubric_level")
                .Annotation("Npgsql:Enum:source_type_enum", "real_exam,ai_generated,user_generated")
                .Annotation("Npgsql:Enum:subject_category_enum", "translation,language_arts,math,science,other")
                .Annotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned")
                .Annotation("Npgsql:Enum:task_type_enum", "A,B")
                .Annotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .Annotation("Npgsql:Enum:visibility_enum", "private,shared")
                .Annotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .Annotation("Npgsql:Enum:weak_point_status_enum", "active,resolved")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification,weak_point_detection_criteria,weak_point_recheck")
                .OldAnnotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .OldAnnotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .OldAnnotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .OldAnnotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .OldAnnotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .OldAnnotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .OldAnnotation("Npgsql:Enum:follow_up_thread_status_enum", "open,closed")
                .OldAnnotation("Npgsql:Enum:followup_verdict_enum", "user_correct,user_incorrect,partial,pending")
                .OldAnnotation("Npgsql:Enum:knowledge_item_type_enum", "sentence_pattern,vocab_expression,formula,concept,theorem,other")
                .OldAnnotation("Npgsql:Enum:mastery_level_enum", "new,familiar,mastered")
                .OldAnnotation("Npgsql:Enum:override_scope_enum", "grading_rubric,translation_reference")
                .OldAnnotation("Npgsql:Enum:override_status_enum", "observing,active,deprecated")
                .OldAnnotation("Npgsql:Enum:priority_enum", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:question_origin_enum", "ai_generated,user_uploaded,real_exam_seed")
                .OldAnnotation("Npgsql:Enum:scale_type_enum", "band_1_5,score_0_100,rubric_level")
                .OldAnnotation("Npgsql:Enum:source_type_enum", "real_exam,ai_generated,user_generated")
                .OldAnnotation("Npgsql:Enum:subject_category_enum", "translation,language_arts,math,science,other")
                .OldAnnotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned")
                .OldAnnotation("Npgsql:Enum:task_type_enum", "A,B")
                .OldAnnotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .OldAnnotation("Npgsql:Enum:visibility_enum", "private,shared")
                .OldAnnotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .OldAnnotation("Npgsql:Enum:weak_point_status_enum", "active,resolved,tracking")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "exam_type_id",
                table: "weak_point_catalog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ux_weak_point_catalog_exam_code",
                table: "weak_point_catalog",
                columns: new[] { "exam_type_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_weak_point_catalog_exam_types_exam_type_id",
                table: "weak_point_catalog",
                column: "exam_type_id",
                principalTable: "exam_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
