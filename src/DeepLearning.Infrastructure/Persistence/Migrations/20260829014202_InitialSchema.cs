using System;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_operation_type_enum", "question_gen,grading,followup,standard_revision")
                .Annotation("Npgsql:Enum:call_status_enum", "pending,calling,success,failed,final_failure")
                .Annotation("Npgsql:Enum:category_type_enum", "domain,scenario")
                .Annotation("Npgsql:Enum:checkpoint_importance_enum", "core,peripheral")
                .Annotation("Npgsql:Enum:difficulty_enum", "easy,medium,hard")
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
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ai_call_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    request_type = table.Column<AiOperationType>(type: "ai_operation_type_enum", nullable: false),
                    related_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<CallStatus>(type: "call_status_enum", nullable: false, defaultValue: CallStatus.pending),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    last_error_message = table.Column<string>(type: "text", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_call_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exam_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_category = table.Column<SubjectCategory>(type: "subject_category_enum", nullable: false),
                    source_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    target_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    grade_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "global"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "question_bank_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category_type = table.Column<CategoryType>(type: "category_type_enum", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_bank_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_bank_categories_question_bank_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "question_bank_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assessment_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dimension_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scale_type = table.Column<ScaleType>(type: "scale_type_enum", nullable: false),
                    pass_threshold = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    level_descriptions = table.Column<string>(type: "jsonb", nullable: false),
                    rubric_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_reference = table.Column<string>(type: "text", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_dimensions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_dimensions_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "error_taxonomies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    example_cases = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_taxonomies", x => x.id);
                    table.ForeignKey(
                        name: "fk_error_taxonomies_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generation_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    policy_value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_policy", x => x.id);
                    table.ForeignKey(
                        name: "fk_generation_policy_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_category = table.Column<SubjectCategory>(type: "subject_category_enum", nullable: true),
                    template_type = table.Column<AiOperationType>(type: "ai_operation_type_enum", nullable: false),
                    layer = table.Column<TemplateLayer>(type: "template_layer_enum", nullable: false),
                    template_content = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prompt_templates", x => x.id);
                    table.CheckConstraint("ck_prompt_templates_layer_scope", "(layer = 'exam_specific' AND exam_type_id IS NOT NULL) OR (layer = 'shared_methodology' AND subject_category IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_prompt_templates_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "progress_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    difficulty_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    avg_band_meaning_transfer = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    avg_band_textual_norms = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    avg_band_language_proficiency = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    pass_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    trend_note = table.Column<string>(type: "text", nullable: true),
                    key_turning_point = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_progress_snapshots_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    task_type = table.Column<TaskType>(type: "task_type_enum", nullable: false),
                    difficulty = table.Column<Difficulty>(type: "difficulty_enum", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    brief = table.Column<string>(type: "jsonb", nullable: true),
                    source_text = table.Column<string>(type: "text", nullable: false),
                    flawed_translation_text = table.Column<string>(type: "text", nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: true),
                    origin = table.Column<QuestionOrigin>(type: "question_origin_enum", nullable: false),
                    source_type = table.Column<SourceType>(type: "source_type_enum", nullable: false),
                    is_seed_reference = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    in_bank = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    visibility = table.Column<Visibility>(type: "visibility_enum", nullable: false, defaultValue: Visibility.Private),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_questions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weak_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    first_detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recurrence_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<WeakPointStatus>(type: "weak_point_status_enum", nullable: false, defaultValue: WeakPointStatus.active),
                    priority = table.Column<Priority>(type: "priority_enum", nullable: false, defaultValue: Priority.medium)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weak_points", x => x.id);
                    table.ForeignKey(
                        name: "fk_weak_points_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_type = table.Column<KnowledgeItemType>(type: "knowledge_item_type_enum", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scenario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    frequency_tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_points", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_points_exam_types_exam_type_id",
                        column: x => x.exam_type_id,
                        principalTable: "exam_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_knowledge_points_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meaning_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkpoint_text = table.Column<string>(type: "text", nullable: false),
                    checkpoint_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    importance = table.Column<CheckpointImportance>(type: "checkpoint_importance_enum", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meaning_checkpoints", x => x.id);
                    table.ForeignKey(
                        name: "fk_meaning_checkpoints_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_category_map",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_category_map", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_category_map_question_bank_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "question_bank_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_question_category_map_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reference_translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_text = table.Column<string>(type: "text", nullable: false),
                    comparison_notes = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_reference_translations_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seed_reference_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    generated_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    similarity_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seed_reference_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_seed_reference_links_questions_generated_question_id",
                        column: x => x.generated_question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_seed_reference_links_questions_seed_question_id",
                        column: x => x.seed_question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sentence_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pattern_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    example_sentence = table.Column<string>(type: "text", nullable: true),
                    breakdown_steps = table.Column<string>(type: "jsonb", nullable: true),
                    variants = table.Column<string>(type: "text", nullable: true),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scenario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    frequency_tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sentence_patterns", x => x.id);
                    table.ForeignKey(
                        name: "fk_sentence_patterns_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_type = table.Column<TaskType>(type: "task_type_enum", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<SubmissionStatus>(type: "submission_status_enum", nullable: false, defaultValue: SubmissionStatus.draft),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_submissions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_b_seeded_errors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_start = table.Column<int>(type: "integer", nullable: false),
                    position_end = table.Column<int>(type: "integer", nullable: false),
                    error_taxonomy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correct_reference_text = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_b_seeded_errors", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_b_seeded_errors_error_taxonomies_error_taxonomy_id",
                        column: x => x.error_taxonomy_id,
                        principalTable: "error_taxonomies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_b_seeded_errors_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vocab_expressions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    english_expr = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    chinese_equiv = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    context_note = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scenario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    frequency_tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vocab_expressions", x => x.id);
                    table.ForeignKey(
                        name: "fk_vocab_expressions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_knowledge_point_review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    times_encountered = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    mastery_level = table.Column<MasteryLevel>(type: "mastery_level_enum", nullable: false, defaultValue: MasteryLevel.New),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_knowledge_point_review", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_knowledge_point_review_knowledge_points_knowledge_poin",
                        column: x => x.knowledge_point_id,
                        principalTable: "knowledge_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_knowledge_point_review_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_pattern_review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_id = table.Column<Guid>(type: "uuid", nullable: false),
                    times_encountered = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    mastery_level = table.Column<MasteryLevel>(type: "mastery_level_enum", nullable: false, defaultValue: MasteryLevel.New),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_pattern_review", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_pattern_review_sentence_patterns_pattern_id",
                        column: x => x.pattern_id,
                        principalTable: "sentence_patterns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_pattern_review_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "error_list",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_text_snippet = table.Column<string>(type: "text", nullable: true),
                    user_text_snippet = table.Column<string>(type: "text", nullable: true),
                    error_taxonomy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impacts_core = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    suggestion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_list", x => x.id);
                    table.ForeignKey(
                        name: "fk_error_list_assessment_dimensions_dimension_id",
                        column: x => x.dimension_id,
                        principalTable: "assessment_dimensions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_error_list_error_taxonomies_error_taxonomy_id",
                        column: x => x.error_taxonomy_id,
                        principalTable: "error_taxonomies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_error_list_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "follow_up_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    context_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    ai_response = table.Column<string>(type: "text", nullable: true),
                    verdict = table.Column<FollowUpVerdict>(type: "followup_verdict_enum", nullable: false, defaultValue: FollowUpVerdict.pending),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_follow_up_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_follow_up_questions_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_follow_up_questions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grading_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    band = table.Column<int>(type: "integer", nullable: false),
                    pass_bool = table.Column<bool>(type: "boolean", nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: false),
                    cumulative_density_flag = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cumulative_density_note = table.Column<string>(type: "text", nullable: true),
                    estimated_pass_probability = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_results", x => x.id);
                    table.CheckConstraint("ck_grading_results_band_range", "band BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_grading_results_assessment_dimensions_dimension_id",
                        column: x => x.dimension_id,
                        principalTable: "assessment_dimensions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grading_results_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_vocab_review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vocab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    times_encountered = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    mastery_level = table.Column<MasteryLevel>(type: "mastery_level_enum", nullable: false, defaultValue: MasteryLevel.New),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_vocab_review", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_vocab_review_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_vocab_review_vocab_expressions_vocab_id",
                        column: x => x.vocab_id,
                        principalTable: "vocab_expressions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weak_point_occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    weak_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    error_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_recurrence = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weak_point_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "fk_weak_point_occurrences_error_list_error_list_id",
                        column: x => x.error_list_id,
                        principalTable: "error_list",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_weak_point_occurrences_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_weak_point_occurrences_weak_points_weak_point_id",
                        column: x => x.weak_point_id,
                        principalTable: "weak_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "standard_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    scope = table.Column<OverrideScope>(type: "override_scope_enum", nullable: false),
                    dimension_or_rule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_rule_text = table.Column<string>(type: "text", nullable: true),
                    revised_rule_text = table.Column<string>(type: "text", nullable: false),
                    triggered_by_followup_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<OverrideStatus>(type: "override_status_enum", nullable: false, defaultValue: OverrideStatus.observing),
                    previous_override_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_standard_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_standard_overrides_follow_up_questions_triggered_by_followu",
                        column: x => x.triggered_by_followup_id,
                        principalTable: "follow_up_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_standard_overrides_standard_overrides_previous_override_id",
                        column: x => x.previous_override_id,
                        principalTable: "standard_overrides",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ai_call_logs_status",
                table: "ai_call_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_dimensions_exam_type_id_dimension_key_rubric_ver",
                table: "assessment_dimensions",
                columns: new[] { "exam_type_id", "dimension_key", "rubric_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_error_list_submission",
                table: "error_list",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_error_list_dimension_id",
                table: "error_list",
                column: "dimension_id");

            migrationBuilder.CreateIndex(
                name: "ix_error_list_error_taxonomy_id",
                table: "error_list",
                column: "error_taxonomy_id");

            migrationBuilder.CreateIndex(
                name: "ix_error_taxonomies_exam_type_id_category_key",
                table: "error_taxonomies",
                columns: new[] { "exam_type_id", "category_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_types_code",
                table: "exam_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feature_flags_key",
                table: "feature_flags",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_follow_up_questions_sub",
                table: "follow_up_questions",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_follow_up_questions_user_id",
                table: "follow_up_questions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_policy_exam_type_id_policy_key",
                table: "generation_policy",
                columns: new[] { "exam_type_id", "policy_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_grading_results_submission",
                table: "grading_results",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_grading_results_dimension_id",
                table: "grading_results",
                column: "dimension_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_points_exam_type_id",
                table: "knowledge_points",
                column: "exam_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_points_question_id",
                table: "knowledge_points",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_meaning_checkpoints_question_id",
                table: "meaning_checkpoints",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "idx_progress_snapshots_user",
                table: "progress_snapshots",
                columns: new[] { "user_id", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ix_prompt_templates_exam_type_id",
                table: "prompt_templates",
                column: "exam_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_bank_categories_parent_id",
                table: "question_bank_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_category_map_category_id",
                table: "question_category_map",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_category_map_question_id_category_id",
                table: "question_category_map",
                columns: new[] { "question_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_questions_in_bank",
                table: "questions",
                column: "in_bank",
                filter: "in_bank = true");

            migrationBuilder.CreateIndex(
                name: "idx_questions_task_difficulty",
                table: "questions",
                columns: new[] { "task_type", "difficulty" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_created_by",
                table: "questions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_reference_translations_question_id",
                table: "reference_translations",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_seed_reference_links_generated_question_id",
                table: "seed_reference_links",
                column: "generated_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_seed_reference_links_seed_question_id",
                table: "seed_reference_links",
                column: "seed_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_sentence_patterns_question_id",
                table: "sentence_patterns",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "idx_standard_overrides_status",
                table: "standard_overrides",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_standard_overrides_previous_override_id",
                table: "standard_overrides",
                column: "previous_override_id");

            migrationBuilder.CreateIndex(
                name: "ix_standard_overrides_triggered_by_followup_id",
                table: "standard_overrides",
                column: "triggered_by_followup_id");

            migrationBuilder.CreateIndex(
                name: "idx_submissions_status",
                table: "submissions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_submissions_user",
                table: "submissions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_question_id",
                table: "submissions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_b_seeded_errors_error_taxonomy_id",
                table: "task_b_seeded_errors",
                column: "error_taxonomy_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_b_seeded_errors_question_id",
                table: "task_b_seeded_errors",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_knowledge_point_review_knowledge_point_id",
                table: "user_knowledge_point_review",
                column: "knowledge_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_knowledge_point_review_user_id_knowledge_point_id",
                table: "user_knowledge_point_review",
                columns: new[] { "user_id", "knowledge_point_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_pattern_review_pattern_id",
                table: "user_pattern_review",
                column: "pattern_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_pattern_review_user_id_pattern_id",
                table: "user_pattern_review",
                columns: new[] { "user_id", "pattern_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_vocab_review_user_id_vocab_id",
                table: "user_vocab_review",
                columns: new[] { "user_id", "vocab_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_vocab_review_vocab_id",
                table: "user_vocab_review",
                column: "vocab_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vocab_expressions_question_id",
                table: "vocab_expressions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "idx_weak_point_occurrences_wp",
                table: "weak_point_occurrences",
                column: "weak_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_weak_point_occurrences_error_list_id",
                table: "weak_point_occurrences",
                column: "error_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_weak_point_occurrences_submission_id",
                table: "weak_point_occurrences",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "idx_weak_points_user_status",
                table: "weak_points",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_call_logs");

            migrationBuilder.DropTable(
                name: "feature_flags");

            migrationBuilder.DropTable(
                name: "generation_policy");

            migrationBuilder.DropTable(
                name: "grading_results");

            migrationBuilder.DropTable(
                name: "meaning_checkpoints");

            migrationBuilder.DropTable(
                name: "progress_snapshots");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "question_category_map");

            migrationBuilder.DropTable(
                name: "reference_translations");

            migrationBuilder.DropTable(
                name: "seed_reference_links");

            migrationBuilder.DropTable(
                name: "standard_overrides");

            migrationBuilder.DropTable(
                name: "task_b_seeded_errors");

            migrationBuilder.DropTable(
                name: "user_knowledge_point_review");

            migrationBuilder.DropTable(
                name: "user_pattern_review");

            migrationBuilder.DropTable(
                name: "user_vocab_review");

            migrationBuilder.DropTable(
                name: "weak_point_occurrences");

            migrationBuilder.DropTable(
                name: "question_bank_categories");

            migrationBuilder.DropTable(
                name: "follow_up_questions");

            migrationBuilder.DropTable(
                name: "knowledge_points");

            migrationBuilder.DropTable(
                name: "sentence_patterns");

            migrationBuilder.DropTable(
                name: "vocab_expressions");

            migrationBuilder.DropTable(
                name: "error_list");

            migrationBuilder.DropTable(
                name: "weak_points");

            migrationBuilder.DropTable(
                name: "assessment_dimensions");

            migrationBuilder.DropTable(
                name: "error_taxonomies");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropTable(
                name: "exam_types");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
