using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabGlossaryAndReferenceTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_vocab_review_vocab_expressions_vocab_id",
                table: "user_vocab_review");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification,weak_point_detection_criteria,weak_point_recheck,score_challenge_summary,vocab_semantic_drift")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .Annotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .Annotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .Annotation("Npgsql:Enum:follow_up_thread_kind_enum", "dispute,knowledge,score_challenge")
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
                .Annotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned,regraded")
                .Annotation("Npgsql:Enum:task_type_enum", "A,B")
                .Annotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .Annotation("Npgsql:Enum:visibility_enum", "private,shared")
                .Annotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .Annotation("Npgsql:Enum:weak_point_status_enum", "active,resolved,tracking")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification,weak_point_detection_criteria,weak_point_recheck,score_challenge_summary")
                .OldAnnotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .OldAnnotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .OldAnnotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .OldAnnotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .OldAnnotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .OldAnnotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .OldAnnotation("Npgsql:Enum:follow_up_thread_kind_enum", "dispute,knowledge,score_challenge")
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
                .OldAnnotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned,regraded")
                .OldAnnotation("Npgsql:Enum:task_type_enum", "A,B")
                .OldAnnotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .OldAnnotation("Npgsql:Enum:visibility_enum", "private,shared")
                .OldAnnotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .OldAnnotation("Npgsql:Enum:weak_point_status_enum", "active,resolved,tracking")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "reference_title",
                table: "reference_translations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vocab_glossary",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    canonical_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    english_expr = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    accumulated_semantics = table.Column<string>(type: "text", nullable: false),
                    chinese_equiv = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scenario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    frequency_tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sense_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    first_seen_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vocab_glossary", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_vocab_glossary_canonical",
                table: "vocab_glossary",
                column: "canonical_key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_user_vocab_review_vocab_glossary_vocab_id",
                table: "user_vocab_review",
                column: "vocab_id",
                principalTable: "vocab_glossary",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_vocab_review_vocab_glossary_vocab_id",
                table: "user_vocab_review");

            migrationBuilder.DropTable(
                name: "vocab_glossary");

            migrationBuilder.DropColumn(
                name: "reference_title",
                table: "reference_translations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification,weak_point_detection_criteria,weak_point_recheck,score_challenge_summary")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .Annotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .Annotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .Annotation("Npgsql:Enum:follow_up_thread_kind_enum", "dispute,knowledge,score_challenge")
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
                .Annotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned,regraded")
                .Annotation("Npgsql:Enum:task_type_enum", "A,B")
                .Annotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .Annotation("Npgsql:Enum:visibility_enum", "private,shared")
                .Annotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .Annotation("Npgsql:Enum:weak_point_status_enum", "active,resolved,tracking")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification,weak_point_detection_criteria,weak_point_recheck,score_challenge_summary,vocab_semantic_drift")
                .OldAnnotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .OldAnnotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .OldAnnotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .OldAnnotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .OldAnnotation("Npgsql:Enum:error_severity_enum", "major,minor")
                .OldAnnotation("Npgsql:Enum:follow_up_message_role_enum", "user,ai")
                .OldAnnotation("Npgsql:Enum:follow_up_thread_kind_enum", "dispute,knowledge,score_challenge")
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
                .OldAnnotation("Npgsql:Enum:submission_status_enum", "draft,submitted,grading,grading_failed,graded,under_dispute,standard_revised,archived,grading_abandoned,regraded")
                .OldAnnotation("Npgsql:Enum:task_type_enum", "A,B")
                .OldAnnotation("Npgsql:Enum:template_layer_enum", "shared_methodology,exam_specific")
                .OldAnnotation("Npgsql:Enum:visibility_enum", "private,shared")
                .OldAnnotation("Npgsql:Enum:weak_point_catalog_status_enum", "active,deprecated,proposed")
                .OldAnnotation("Npgsql:Enum:weak_point_status_enum", "active,resolved,tracking")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddForeignKey(
                name: "fk_user_vocab_review_vocab_expressions_vocab_id",
                table: "user_vocab_review",
                column: "vocab_id",
                principalTable: "vocab_expressions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
