-- =====================================================================
-- AI-generated questions store source_text as a single unbroken blob — the
-- Step 3 shared_methodology/question_gen JSON-contract row (created via the API,
-- not in any SQL file) only asks for a "sourceText" string and never says to
-- paragraph it, and models default to one-paragraph prose inside a JSON string
-- field. Result: the frontend has no \n\n to render paragraphs from, and a
-- ~250-word passage shows as one wall of text.
--
-- This adds one more additive shared_methodology/question_gen row (same
-- concatenation convention as add_taskb_question_gen_prompt_template.sql /
-- add_seed_reference_question_gen_prompt_template.sql — IExamConfigLoader
-- appends every active row for a template_type) instructing the model to break
-- sourceText into real paragraphs separated by a blank line (\n\n). No handler
-- or schema change: GenerateQuestionCommandHandler already stores payload.SourceText
-- verbatim, so whatever \n\n the model emits is persisted as-is.
--
-- Hand-run against Supabase, same convention as the other prompt-template SQL files.
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
【原文排版要求】
- sourceText 必须分段：按自然的语义层次分成 3-5 段,段落之间用一个空行分隔(即 JSON 字符串里的 \n\n)
- 段内不要加换行,一段就是连续的一段文字
- 首段点题/给出背景,中间段展开,末段收束,符合真实文章的段落节奏
- 这条同样适用于 TaskB 的 flawedTranslationText:含错译文的分段应与原文一致
$tpl$,
    1,
    TRUE
);

COMMIT;
