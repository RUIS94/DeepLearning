-- =====================================================================
-- Step 4 (提交与评判主链路): grading needed a new prompt_templates row for
-- the same reason question_gen did back in Step 3 — the two shared_methodology/grading
-- rows and the one exam_specific/grading row already seeded by seed_naati_ct_en_zh.sql
-- only specify HOW to grade (rubric criteria, error taxonomies, task-type pass
-- thresholds); none of them inject the actual material to grade (source text,
-- the user's submission, meaning_checkpoints, TaskB's seeded errors) or an output
-- contract, because GradeSubmissionCommandHandler didn't exist yet when they were
-- written.
--
-- This is shared_methodology/translation (not exam_specific) because the content-
-- injection variables and JSON contract are generic to any translation-shaped
-- grading call, not NAATI-CT-specific — consistent with how the two existing
-- shared_methodology/grading rows are scoped.
--
-- GradeSubmissionCommandHandler supplies: task_type, source_text, flawed_translation_text
-- (null for TaskA), submission_content, meaning_checkpoints[], seeded_errors[] (empty for
-- TaskA), dimensions[] (each with dimension_key/dimension_name/pass_threshold/level_descriptions),
-- error_taxonomies[] (each with category_key/category_name/description) — see BuildTemplateModel.
--
-- flawed_translation_text was missing from the first version of this row — TaskBSeededError's
-- position_start/position_end are character offsets into that text, so without the text itself
-- the AI had positions and category labels but nothing to locate them against, making it unable
-- to actually judge whether the user's annotations were correct. Fixed same day, before this
-- file was ever run against real Supabase.
--
-- If grading ever starts failing to parse, check this row hasn't been deactivated
-- or superseded incompatibly (same caveat as the question_gen row from Step 3).
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'grading',
    'shared_methodology',
    $tpl$
【待评判材料】
原文:
{{ source_text }}

用户提交内容(JSON):
{{ submission_content }}

{{ if meaning_checkpoints.size > 0 }}
必须核对是否准确传达以下信息点(仅用于核查依据,解释文本中禁止出现"参考译文"字样):
{{ for cp in meaning_checkpoints }}
- [{{ cp.importance }}] {{ cp.checkpoint_text }}
{{ end }}
{{ end }}

{{ if seeded_errors.size > 0 }}
本题为TaskB审校任务,以下是含错译文全文(用户基于这份译文进行划词标注、错误归类与更正,下方错误位置均为该文本中的字符偏移量):
{{ flawed_translation_text }}

译文中预先植入了以下错误,请核对用户的标注是否准确识别、归类、更正了它们:
{{ for e in seeded_errors }}
- 位置[{{ e.position_start }}-{{ e.position_end }}] 类别:{{ e.error_category }} 正确译法:{{ e.correct_reference_text }}
{{ end }}
{{ end }}

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "dimensions": [
    {"dimensionKey": "<必须是上方评分维度列表中的dimension_key之一>", "band": <1-5整数>, "rationale": "<引用对应Band英文原文作为依据>", "cumulativeDensityFlag": <true/false>, "cumulativeDensityNote": "<string或null>", "estimatedPassProbability": <0-100数字,主观估算,非官方数据>}
  ],
  "errors": [
    {"positionRef": "<定位信息>", "sourceTextSnippet": "<原文片段>", "userTextSnippet": "<用户译文片段>", "errorCategory": "<必须是上方错误类别列表中的category_key之一>", "dimensionKey": "<所属评分维度key>", "impactsCore": <true/false>, "explanation": "<说明>", "suggestion": "<建议>"}
  ]
}

dimensions数组必须覆盖上方给出的每一个评分维度,逐一给出Band判断,不得遗漏。
$tpl$,
    1,
    TRUE
);

COMMIT;
