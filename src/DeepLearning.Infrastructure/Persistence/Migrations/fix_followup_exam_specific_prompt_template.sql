-- =====================================================================
-- Step 5 follow-up: the exam_specific/followup row seeded by seed_naati_ct_en_zh.sql
-- (id 4ac934d3-85ca-4742-809c-ef1017d0cf90, created 2026-08-29, BEFORE
-- CreateFollowUpQuestionCommandHandler existed) is actively harmful once concatenated
-- alongside add_followup_prompt_template.sql's new shared_methodology row, for two
-- independent reasons — found by inspection before either file was run against real
-- Supabase, the same day both were written:
--
-- 1. It references `{{ followup_question_text }}`, but CreateFollowUpQuestionCommandHandler's
--    template model exposes the question text as `question_text` (from
--    BuildTemplateModel's `QuestionText` property, Scriban-renamed to snake_case) — a name that
--    was never actually wired up, since nothing consumed this row before now. Scriban silently
--    renders an unknown variable as empty string rather than erroring, so this wasn't caught by
--    a parse failure — it would just silently drop the user's actual question text from the prompt.
-- 2. IExamConfigLoader.BuildPromptAsync concatenates shared_methodology rows FIRST, then
--    exam_specific rows AFTER (`sharedTemplates.Concat(specificTemplates)`). This row's closing
--    line — "最终请给出verdict:user_correct / user_incorrect / partial,并说明理由" — is a plain-text
--    instruction that would land AFTER add_followup_prompt_template.sql's strict "严格只输出以下
--    JSON,不要输出markdown代码块围栏之外的任何文字" instruction, directly contradicting it. Since
--    CreateFollowUpQuestionCommandHandler parses the AI's response as JSON and rejects anything
--    else, this risked actually breaking every NAATI CT follow-up call, not just degrading quality.
--
-- Fix: deactivate the old row (IPromptTemplateRepository.ListAsync only returns is_active rows,
-- but does NOT limit to the latest version — if both were left active, both would render and
-- concatenate) and insert a version=2 replacement that keeps its genuinely useful guidance
-- (re-check severity against the Band text rather than going by impression, don't cave to an
-- emotional appeal, don't dodge a clear verdict) without repeating the question text (the
-- shared_methodology row's 【用户的追问】 section already includes it) or re-specifying an output
-- format (the shared_methodology row already fully owns the JSON contract).
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE id = '4ac934d3-85ca-4742-809c-ef1017d0cf90';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'followup',
    'exam_specific',
    $tpl$
本考试类型(NAATI CT英译中)追问复核的额外提醒:
- 重新审视原始判断依据时,对照assessment_dimensions的Band英文原文与error_taxonomies定义逐项复核,不要凭印象下结论
- 如果首次给出的严重度判断证据不足或过轻,应坦诚上调/下调,并在aiResponse中说明修正后的推理过程
- 不要因为用户情绪化的申诉而降低标准,也不要因为担心冲突而回避明确表态
$tpl$,
    2,
    TRUE
);

COMMIT;
