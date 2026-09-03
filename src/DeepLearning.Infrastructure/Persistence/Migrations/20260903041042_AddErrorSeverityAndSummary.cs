using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorSeverityAndSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .Annotation("Npgsql:Enum:error_severity_enum", "critical,major,minor,moderate")
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
                .Annotation("Npgsql:Enum:weak_point_status_enum", "active,resolved")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification")
                .OldAnnotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .OldAnnotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .OldAnnotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .OldAnnotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
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
                .OldAnnotation("Npgsql:Enum:weak_point_status_enum", "active,resolved")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<ErrorSeverity>(
                name: "severity",
                table: "error_list",
                type: "error_severity_enum",
                nullable: false,
                defaultValue: ErrorSeverity.moderate);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "error_list",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "severity",
                table: "error_list");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "error_list");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
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
                .Annotation("Npgsql:Enum:weak_point_status_enum", "active,resolved")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision,deep_learning,progress_trend,followup_summary,weak_point_classification")
                .OldAnnotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .OldAnnotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .OldAnnotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .OldAnnotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
                .OldAnnotation("Npgsql:Enum:error_severity_enum", "critical,major,minor,moderate")
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
                .OldAnnotation("Npgsql:Enum:weak_point_status_enum", "active,resolved")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
