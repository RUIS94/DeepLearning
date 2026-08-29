-- =====================================================================
-- NAATI CT 翻译练习软件 数据库建表脚本 (PostgreSQL 15+)
-- 对应设计文档第六、九、十节
--
-- 说明:
-- 1. 本脚本供本地开发/初次搭建参考;实际项目落地后建议改由EF Core
--    Migrations管理(见第七节"数据库迁移纪律":只做加法,不改类型/删列)
-- 2. dimension/error_category从md文件里描述的VARCHAR弱字符串匹配,
--    收紧为真正的外键(dimension_id/error_taxonomy_id),原因见对话说明
-- 3. questions/submissions等业务表未加exam_type_id,是刻意保留(见
--    设计文档第九节9.4迁移路径),等接入第二个考试类型时再加
-- 4. 建议整体在一个事务里执行
-- =====================================================================

BEGIN;

-- =====================================================================
-- 扩展
-- =====================================================================
CREATE EXTENSION IF NOT EXISTS pgcrypto;   -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS vector;     -- pgvector,语义检索用(第七节)

-- =====================================================================
-- 枚举类型
-- =====================================================================
CREATE TYPE task_type_enum AS ENUM ('A','B');
CREATE TYPE difficulty_enum AS ENUM ('easy','medium','hard');
CREATE TYPE question_origin_enum AS ENUM ('ai_generated','user_uploaded','real_exam_seed');
CREATE TYPE source_type_enum AS ENUM ('real_exam','ai_generated','user_generated');
CREATE TYPE visibility_enum AS ENUM ('private','shared');
CREATE TYPE submission_status_enum AS ENUM (
    'draft','submitted','grading','grading_failed','graded',
    'under_dispute','standard_revised','archived','grading_abandoned'
);
CREATE TYPE followup_verdict_enum AS ENUM ('user_correct','user_incorrect','partial','pending');
CREATE TYPE override_scope_enum AS ENUM ('grading_rubric','translation_reference');
CREATE TYPE override_status_enum AS ENUM ('observing','active','deprecated');
CREATE TYPE weak_point_status_enum AS ENUM ('active','resolved');
CREATE TYPE priority_enum AS ENUM ('high','medium','low');
CREATE TYPE mastery_level_enum AS ENUM ('new','familiar','mastered');
CREATE TYPE category_type_enum AS ENUM ('domain','scenario');
CREATE TYPE subject_category_enum AS ENUM ('translation','language_arts','math','science','other');
CREATE TYPE scale_type_enum AS ENUM ('band_1_5','score_0_100','rubric_level');
CREATE TYPE ai_operation_type_enum AS ENUM ('question_gen','grading','followup','standard_revision');
CREATE TYPE template_layer_enum AS ENUM ('shared_methodology','exam_specific');
CREATE TYPE call_status_enum AS ENUM ('pending','calling','success','failed','final_failure');
CREATE TYPE checkpoint_importance_enum AS ENUM ('core','peripheral');
CREATE TYPE knowledge_item_type_enum AS ENUM ('sentence_pattern','vocab_expression','formula','concept','theorem','other');

-- =====================================================================
-- 第九/十节:考试类型配置骨架(MVP阶段即实现,不是未来才做)
-- =====================================================================

CREATE TABLE exam_types (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code                VARCHAR(50) UNIQUE NOT NULL,
    name                VARCHAR(100) NOT NULL,
    subject_category    subject_category_enum NOT NULL,
    source_language     VARCHAR(20),
    target_language     VARCHAR(20),
    grade_level         VARCHAR(50),
    description         TEXT,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE assessment_dimensions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_type_id        UUID NOT NULL REFERENCES exam_types(id) ON DELETE CASCADE,
    dimension_key       VARCHAR(50) NOT NULL,
    dimension_name      VARCHAR(100) NOT NULL,
    scale_type          scale_type_enum NOT NULL,
    pass_threshold      VARCHAR(20),
    level_descriptions  JSONB NOT NULL,
    rubric_version      VARCHAR(20) NOT NULL,
    effective_from      TIMESTAMPTZ NOT NULL DEFAULT now(),
    effective_to        TIMESTAMPTZ,
    source_reference    TEXT,
    verified_at         TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (exam_type_id, dimension_key, rubric_version)
);

CREATE TABLE error_taxonomies (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_type_id        UUID NOT NULL REFERENCES exam_types(id) ON DELETE CASCADE,
    category_key        VARCHAR(50) NOT NULL,
    category_name       VARCHAR(100) NOT NULL,
    description         TEXT,
    example_cases       JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (exam_type_id, category_key)
);

CREATE TABLE prompt_templates (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_type_id        UUID REFERENCES exam_types(id) ON DELETE CASCADE,
    subject_category    subject_category_enum,
    template_type       ai_operation_type_enum NOT NULL,
    layer               template_layer_enum NOT NULL,
    template_content    TEXT NOT NULL,
    version             INT NOT NULL DEFAULT 1,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (
        (layer = 'exam_specific' AND exam_type_id IS NOT NULL) OR
        (layer = 'shared_methodology' AND subject_category IS NOT NULL)
    )
);

CREATE TABLE generation_policy (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_type_id        UUID NOT NULL REFERENCES exam_types(id) ON DELETE CASCADE,
    policy_key          VARCHAR(50) NOT NULL,
    policy_value        JSONB NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (exam_type_id, policy_key)
);

-- =====================================================================
-- 第六节:用户与题目
-- =====================================================================

CREATE TABLE users (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username            VARCHAR(50) UNIQUE NOT NULL,
    email               VARCHAR(255) UNIQUE NOT NULL,
    password_hash       VARCHAR(255) NOT NULL,
    display_name        VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_login_at       TIMESTAMPTZ
);

CREATE TABLE questions (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_type                   task_type_enum NOT NULL,
    difficulty                  difficulty_enum NOT NULL,
    title                       VARCHAR(255) NOT NULL,
    brief                       JSONB,
    source_text                 TEXT NOT NULL,
    flawed_translation_text     TEXT,
    word_count                  INT,
    origin                      question_origin_enum NOT NULL,
    source_type                 source_type_enum NOT NULL,
    is_seed_reference           BOOLEAN NOT NULL DEFAULT FALSE,
    in_bank                     BOOLEAN NOT NULL DEFAULT FALSE,
    visibility                  visibility_enum NOT NULL DEFAULT 'private',
    created_by                  UUID REFERENCES users(id),
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_active                   BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE task_b_seeded_errors (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id             UUID NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    position_start          INT NOT NULL,
    position_end            INT NOT NULL,
    error_taxonomy_id       UUID NOT NULL REFERENCES error_taxonomies(id),
    correct_reference_text  TEXT NOT NULL,
    note                    TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE meaning_checkpoints (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id         UUID NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    checkpoint_text      TEXT NOT NULL,
    checkpoint_type      VARCHAR(50),
    importance           checkpoint_importance_enum NOT NULL,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE reference_translations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id         UUID NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    reference_text       TEXT NOT NULL,
    comparison_notes     JSONB,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =====================================================================
-- 第六节:提交与评判
-- =====================================================================

CREATE TABLE submissions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id         UUID NOT NULL REFERENCES questions(id),
    user_id             UUID NOT NULL REFERENCES users(id),
    task_type           task_type_enum NOT NULL,
    content              JSONB NOT NULL,
    status               submission_status_enum NOT NULL DEFAULT 'draft',
    submitted_at         TIMESTAMPTZ,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE grading_results (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    submission_id               UUID NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
    dimension_id                UUID NOT NULL REFERENCES assessment_dimensions(id),
    rubric_version               VARCHAR(20) NOT NULL,
    band                         INT NOT NULL CHECK (band BETWEEN 1 AND 5),
    pass_bool                    BOOLEAN NOT NULL,
    rationale                    TEXT NOT NULL,
    cumulative_density_flag      BOOLEAN NOT NULL DEFAULT FALSE,
    cumulative_density_note      TEXT,
    estimated_pass_probability   NUMERIC(5,2),
    created_at                   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE error_list (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    submission_id           UUID NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
    position_ref            VARCHAR(100),
    source_text_snippet     TEXT,
    user_text_snippet       TEXT,
    error_taxonomy_id       UUID NOT NULL REFERENCES error_taxonomies(id),
    dimension_id            UUID NOT NULL REFERENCES assessment_dimensions(id),
    impacts_core            BOOLEAN NOT NULL DEFAULT FALSE,
    explanation             TEXT,
    suggestion              TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE follow_up_questions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    submission_id       UUID NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
    user_id             UUID NOT NULL REFERENCES users(id),
    context_ref         VARCHAR(100),
    question_text        TEXT NOT NULL,
    ai_response          TEXT,
    verdict               followup_verdict_enum NOT NULL DEFAULT 'pending',
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE standard_overrides (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    scope                       override_scope_enum NOT NULL,
    dimension_or_rule           VARCHAR(100) NOT NULL,
    original_rule_text          TEXT,
    revised_rule_text           TEXT NOT NULL,
    triggered_by_followup_id    UUID REFERENCES follow_up_questions(id),
    status                      override_status_enum NOT NULL DEFAULT 'observing',
    previous_override_id        UUID REFERENCES standard_overrides(id),
    effective_from              TIMESTAMPTZ,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =====================================================================
-- 第六节:深入学习(长难句句型/常用表达)
-- =====================================================================

CREATE TABLE sentence_patterns (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id         UUID REFERENCES questions(id) ON DELETE SET NULL,
    pattern_name         VARCHAR(100) NOT NULL,
    example_sentence      TEXT,
    breakdown_steps       JSONB,
    variants               TEXT,
    domain                 VARCHAR(50),
    scenario                VARCHAR(100),
    frequency_tag           VARCHAR(20),
    created_at               TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE vocab_expressions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id         UUID REFERENCES questions(id) ON DELETE SET NULL,
    english_expr         VARCHAR(255) NOT NULL,
    chinese_equiv         VARCHAR(255),
    context_note           TEXT,
    category                VARCHAR(50),
    domain                   VARCHAR(50),
    scenario                  VARCHAR(100),
    frequency_tag              VARCHAR(20),
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =====================================================================
-- 第六节:薄弱点追踪
-- =====================================================================

CREATE TABLE weak_points (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL REFERENCES users(id),
    category             VARCHAR(100) NOT NULL,
    description           TEXT,
    first_detected_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    recurrence_count         INT NOT NULL DEFAULT 0,
    status                    weak_point_status_enum NOT NULL DEFAULT 'active',
    priority                   priority_enum NOT NULL DEFAULT 'medium'
);

CREATE TABLE weak_point_occurrences (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    weak_point_id        UUID NOT NULL REFERENCES weak_points(id) ON DELETE CASCADE,
    submission_id         UUID NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
    error_list_id           UUID REFERENCES error_list(id) ON DELETE SET NULL,
    is_recurrence            BOOLEAN NOT NULL DEFAULT FALSE,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =====================================================================
-- 第六节:进度分析
-- =====================================================================

CREATE TABLE progress_snapshots (
    id                              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                         UUID NOT NULL REFERENCES users(id),
    period_start                    DATE NOT NULL,
    period_end                      DATE NOT NULL,
    difficulty_tier                 VARCHAR(20),
    avg_band_meaning_transfer       NUMERIC(3,1),
    avg_band_textual_norms          NUMERIC(3,1),
    avg_band_language_proficiency   NUMERIC(3,1),
    pass_rate                       NUMERIC(5,2),
    trend_note                      TEXT,
    key_turning_point               BOOLEAN NOT NULL DEFAULT FALSE,
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =====================================================================
-- 第六节:AI调用日志
-- =====================================================================

CREATE TABLE ai_call_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    request_type        ai_operation_type_enum NOT NULL,
    related_id           UUID,
    status                call_status_enum NOT NULL DEFAULT 'pending',
    attempt_count          INT NOT NULL DEFAULT 0,
    max_retries             INT NOT NULL DEFAULT 3,
    last_error_message       TEXT,
    latency_ms                 INT,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at                  TIMESTAMPTZ
);

-- =====================================================================
-- 第八节:题库与复习库(Phase 2功能,MVP阶段可暂不建)
-- =====================================================================

CREATE TABLE question_bank_categories (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    category_type        category_type_enum NOT NULL,
    name                   VARCHAR(100) NOT NULL,
    parent_id                UUID REFERENCES question_bank_categories(id),
    description                TEXT,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE question_category_map (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    question_id         UUID NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    category_id           UUID NOT NULL REFERENCES question_bank_categories(id) ON DELETE CASCADE,
    UNIQUE (question_id, category_id)
);

CREATE TABLE seed_reference_links (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    generated_question_id       UUID NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    seed_question_id             UUID NOT NULL REFERENCES questions(id),
    similarity_reason              TEXT,
    created_at                       TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE user_pattern_review (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL REFERENCES users(id),
    pattern_id            UUID NOT NULL REFERENCES sentence_patterns(id) ON DELETE CASCADE,
    times_encountered       INT NOT NULL DEFAULT 1,
    mastery_level              mastery_level_enum NOT NULL DEFAULT 'new',
    last_reviewed_at             TIMESTAMPTZ,
    created_at                     TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, pattern_id)
);

CREATE TABLE user_vocab_review (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL REFERENCES users(id),
    vocab_id              UUID NOT NULL REFERENCES vocab_expressions(id) ON DELETE CASCADE,
    times_encountered        INT NOT NULL DEFAULT 1,
    mastery_level               mastery_level_enum NOT NULL DEFAULT 'new',
    last_reviewed_at              TIMESTAMPTZ,
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, vocab_id)
);

-- =====================================================================
-- 第七节:功能开关
-- =====================================================================

CREATE TABLE feature_flags (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key                  VARCHAR(50) UNIQUE NOT NULL,
    enabled                BOOLEAN NOT NULL DEFAULT FALSE,
    scope                    VARCHAR(50) NOT NULL DEFAULT 'global',
    updated_at                 TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =====================================================================
-- 第九节9.3:仍待通用化的知识点表(面向未来多学科,MVP可暂不建)
-- =====================================================================

CREATE TABLE knowledge_points (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_type_id         UUID NOT NULL REFERENCES exam_types(id),
    question_id            UUID REFERENCES questions(id) ON DELETE SET NULL,
    item_type                 knowledge_item_type_enum NOT NULL,
    title                       VARCHAR(255) NOT NULL,
    payload                       JSONB,
    domain                          VARCHAR(50),
    scenario                          VARCHAR(100),
    frequency_tag                       VARCHAR(20),
    created_at                            TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE user_knowledge_point_review (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                     UUID NOT NULL REFERENCES users(id),
    knowledge_point_id           UUID NOT NULL REFERENCES knowledge_points(id) ON DELETE CASCADE,
    times_encountered              INT NOT NULL DEFAULT 1,
    mastery_level                     mastery_level_enum NOT NULL DEFAULT 'new',
    last_reviewed_at                    TIMESTAMPTZ,
    created_at                            TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, knowledge_point_id)
);

-- =====================================================================
-- 索引建议(按常见查询路径补充,非穷举,后续可按实际慢查询调整)
-- =====================================================================

CREATE INDEX idx_questions_in_bank            ON questions(in_bank) WHERE in_bank = TRUE;
CREATE INDEX idx_questions_task_difficulty    ON questions(task_type, difficulty);
CREATE INDEX idx_submissions_user             ON submissions(user_id);
CREATE INDEX idx_submissions_status           ON submissions(status);
CREATE INDEX idx_grading_results_submission   ON grading_results(submission_id);
CREATE INDEX idx_error_list_submission        ON error_list(submission_id);
CREATE INDEX idx_weak_points_user_status      ON weak_points(user_id, status);
CREATE INDEX idx_weak_point_occurrences_wp    ON weak_point_occurrences(weak_point_id);
CREATE INDEX idx_ai_call_logs_status          ON ai_call_logs(status);
CREATE INDEX idx_standard_overrides_status    ON standard_overrides(status);
CREATE INDEX idx_follow_up_questions_sub      ON follow_up_questions(submission_id);
CREATE INDEX idx_progress_snapshots_user      ON progress_snapshots(user_id, period_start);

COMMIT;

-- =====================================================================
-- 增量迁移(第七节"数据库迁移纪律":只做加法,不改类型/删列)
-- 对应 EF Core 迁移: AddApplicableTaskTypeToAssessmentDimensions
-- =====================================================================
ALTER TABLE assessment_dimensions
    ADD COLUMN IF NOT EXISTS applicable_task_type task_type_enum;
