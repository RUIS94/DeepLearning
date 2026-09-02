-- =====================================================================
-- Step 7 (深入学习模块): reference_translations独立调用生成、sentence_patterns/
-- vocab_expressions — same recurring reason as every prior step's own content-
-- injection row (question_gen/Step 3, grading/Step 4, followup/Step 5): no
-- prompt_templates row existed for template_type='deep_learning' before this,
-- because GenerateDeepLearningContentCommandHandler didn't exist when the seed
-- data was written.
--
-- This is shared_methodology/translation (not exam_specific) — the content-
-- injection variable and JSON contract are generic to any translation-shaped
-- deep-learning call, not NAATI-CT-specific, same scoping as the grading/
-- question_gen shared_methodology rows.
--
-- Design doc §10.2's isolation guarantee, mirrored: this call is given ONLY
-- task_type + source_text (see GenerateDeepLearningContentCommandHandler's own
-- doc comment) — never a submission's content, its grading_results, or
-- meaning_checkpoints, so a generated reference translation can never be
-- contaminated by any one user's specific answer.
--
-- If deep-learning generation ever starts failing to parse, check this row
-- (prompt_templates where template_type='deep_learning' AND
-- layer='shared_methodology') hasn't been deactivated or superseded
-- incompatibly.
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'deep_learning',
    'shared_methodology',
    $tpl$
【原文】
{{ source_text }}

任务类型:{{ task_type }}

请针对以上原文,为正在备考翻译考试的学习者生成"深入学习"材料,包括:
1. 一份高质量的标准参考译文(仅供学习对照,不作为评分唯一依据)
2. 若干条关于本文翻译技巧/常见误译陷阱的简短笔记
3. 原文中出现的、值得积累的长难句句型(如果有)
4. 原文中出现的、值得积累的常用表达/固定搭配/习语(如果有)

句型和表达不要牵强凑数,原文中如果没有特别值得积累的长难句或表达,对应数组可以为空。

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "referenceText": "<完整参考译文>",
  "comparisonNotes": ["<技巧/易错点笔记1>", "<技巧/易错点笔记2>"],
  "sentencePatterns": [
    {"patternName": "<句型名称>", "exampleSentence": "<原文中的例句>", "breakdownSteps": {"主干": "...", "从句": "...", "插入语": "..."}, "variants": "<常见变体或string或null>", "domain": "<领域,如法律/医疗/政府公告,或null>", "scenario": "<应用场景,或null>", "frequencyTag": "<高频/中频/低频,或null>"}
  ],
  "vocabExpressions": [
    {"englishExpr": "<英文表达>", "chineseEquiv": "<中文对应,或null>", "contextNote": "<词典本义/引申义/语境义区分,或null>", "category": "<分类,或null>", "domain": "<领域,或null>", "scenario": "<应用场景,或null>", "frequencyTag": "<高频/中频/低频,或null>"}
  ]
}
$tpl$,
    1,
    TRUE
);

COMMIT;
