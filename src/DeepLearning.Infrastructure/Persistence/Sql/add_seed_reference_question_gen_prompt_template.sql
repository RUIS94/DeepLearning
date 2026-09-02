-- =====================================================================
-- Step 8 (design doc §11.2): GenerateQuestionCommandHandler now retrieves a few real-exam
-- samples (Question.IsSeedReference=true, filtered by task type + difficulty, optionally
-- category — "先按领域/难度过滤" per the design doc, pgvector semantic search deferred) and
-- passes them into the template model as seed_samples[] (each { title, source_text }).
--
-- This adds a new shared_methodology/question_gen row (additive — IExamConfigLoader
-- concatenates every active row matching a template_type, same convention as
-- add_taskb_question_gen_prompt_template.sql) instructing the AI to use these as *style*
-- reference only: topic register, sentence complexity, typical length. It must NOT copy or
-- closely paraphrase their content — seed_samples exist to calibrate what a "real" exam
-- passage feels like, not to be plagiarized. Renders to nothing when seed_samples is empty
-- (Scriban's {{ for }} over an empty list is a no-op), so this is a no-op for exam types or
-- difficulty/task-type combinations with no seed_reference questions on file yet.
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'question_gen',
    'shared_methodology',
    $tpl$
{{ if seed_samples.size > 0 }}
【真题参考样本】
以下是若干篇真实存在、领域/难度与本次出题要求相近的真题原文,仅供你参考其题材选择、语言难度、句式复杂度与篇幅长度的"手感",帮助你生成的新题更贴近真实考试:
{{ for s in seed_samples }}
---
标题:{{ s.title }}
{{ s.source_text }}
{{ end }}
---
重要:上述样本仅作风格参照,禁止直接复制、逐句改写或大段挪用其内容——你生成的原文必须是全新的、与样本不同的具体内容。
{{ end }}
$tpl$,
    1,
    TRUE
);

COMMIT;
