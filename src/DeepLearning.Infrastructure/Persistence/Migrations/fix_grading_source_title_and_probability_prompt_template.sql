-- =====================================================================
-- Two bugs in the grading content-injection row first seeded by
-- add_grading_content_prompt_template.sql (shared_methodology / grading,
-- subject_category='translation', version=1, distinctive marker 【待评判材料】):
--
-- 1. TITLE WAS NEVER GIVEN TO THE AI. The row prints only "原文:" + the body
--    text. questions.title existed but BuildTemplateModel never passed it. Result:
--    for any question whose source article has a title, the AI sees the user's
--    translated title with no source to match it against and reports it as
--    "无中生有的信息增添" ("原文无标题 ... 用户译文自行添加了标题"), i.e. it
--    penalises a faithful translation of a title the source actually had.
--    GradeSubmissionCommandHandler.BuildTemplateModel now passes SourceTitle
--    (= question.Title, Scriban-renamed to source_title); this row is replaced with
--    a version=2 that renders a 原文标题 block when it is non-empty and tells the
--    model that a title shown there is source-owned, not a user addition. When the
--    block is absent the source genuinely had no title and the old behaviour stands.
--
-- 2. estimatedPassProbability UNIT MISMATCH. The output contract asked for
--    "<0-100数字>", so the model returned e.g. 70 / 85 / 60, persisted verbatim to
--    grading_results.estimated_pass_probability (NUMERIC(5,2)). The frontend
--    (grading-result-panel.tsx) renders it as `Math.round(p * 100)%`, expecting a
--    0..1 fraction (its mock data uses 0.78 / 0.41) — so 70 rendered as "7000%".
--    version=2 changes the contract to "0到1之间的两位小数,如0.65". The optional
--    UPDATE at the bottom normalises rows already written under the old contract.
--
-- Same "new version, deactivate old" convention as
-- fix_grading_dimension_key_prompt_template.sql. Matched by the 【待评判材料】
-- content marker + version=1 (the id was DB-generated, not fixed in the seed file).
--
-- Hand-run against Supabase. A fresh DB built from add_grading_content_prompt_template.sql
-- must run this file too (that file is kept as historical record and not edited).
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'grading'
  AND layer = 'shared_methodology'
  AND version = 1
  AND is_active = TRUE
  AND template_content LIKE '%【待评判材料】%';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'grading',
    'shared_methodology',
    $tpl$
【待评判材料】
{{ if source_title != "" }}
原文标题(原文自带,非用户添加):
{{ source_title }}

用户译文中与上述标题对应的译文属于对原文既有内容的翻译,不得判为"无中生有"的信息增添;仅当本节完全缺失时,才可按"原文无标题"处理。
{{ end }}
原文正文:
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
    {"dimensionKey": "<必须是上方评分维度列表中的dimension_key之一>", "band": <1-5整数>, "rationale": "<引用对应Band英文原文作为依据>", "cumulativeDensityFlag": <true/false>, "cumulativeDensityNote": "<string或null>", "estimatedPassProbability": <0到1之间的两位小数,如0.65,表示本篇译文的主观通过概率,非官方数据>}
  ],
  "errors": [
    {"positionRef": "<定位信息>", "sourceTextSnippet": "<原文片段>", "userTextSnippet": "<用户译文片段>", "errorCategory": "<必须是上方错误类别列表中的category_key之一>", "dimensionKey": "<所属评分维度key>", "impactsCore": <true/false>, "explanation": "<说明>", "suggestion": "<建议>"}
  ]
}

dimensions数组必须覆盖上方给出的每一个评分维度,逐一给出Band判断,不得遗漏。
$tpl$,
    2,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'grading'
      AND layer = 'shared_methodology'
      AND version = 2
      AND template_content LIKE '%【待评判材料】%'
);

-- Optional data fix: normalise grading_results rows written while the contract still
-- asked for 0-100. Anything > 1 was a percentage; scale it back to a fraction.
UPDATE grading_results
SET estimated_pass_probability = ROUND(estimated_pass_probability / 100, 2)
WHERE estimated_pass_probability > 1;

COMMIT;

-- Verify:
-- SELECT layer, version, is_active, left(template_content, 30)
-- FROM prompt_templates WHERE template_type='grading' ORDER BY layer, version;
-- -> the 【待评判材料】 row is now version=1 (is_active=false) + version=2 (is_active=true).
-- SELECT max(estimated_pass_probability) FROM grading_results;  -- -> should be <= 1
