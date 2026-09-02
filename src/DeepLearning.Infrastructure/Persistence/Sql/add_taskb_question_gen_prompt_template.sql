-- =====================================================================
-- GenerateQuestion never actually supported TaskB — the one exam_specific/question_gen
-- row seeded by seed_naati_ct_en_zh.sql (§5.3) only describes a plain translation task
-- (source text + Translation Brief), never mentions generating a flawed translation or
-- seeded errors, and doesn't even reference {{ task_type }}. Calling
-- POST /questions/generate with TaskType=B silently produced a Question with
-- TaskType=B but FlawedTranslationText=null and zero TaskBSeededError rows — a
-- structurally broken TaskB question, un-gradable later since GradeSubmission's TaskB
-- path depends on both.
--
-- This adds a new shared_methodology/question_gen row (not exam_specific — the
-- {{ if task_type == "B" }} guard makes it generic to any translation-shaped exam
-- type, same scoping as the other shared_methodology rows) supplying the missing
-- TaskB instructions + the flawedTranslationText/seededErrors JSON contract fields
-- GenerateQuestionCommandHandler now parses. Guarded so it's a no-op for TaskA calls.
--
-- GenerateQuestionCommandHandler supplies: difficulty, task_type, error_taxonomies[]
-- (each with category_key/category_name/description) — see BuildTemplateModel-equivalent
-- inline object construction in GenerateQuestionCommandHandler.Handle.
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
{{ if task_type == "B" }}
【TaskB专属出题要求:审校任务】
本次不是普通翻译任务,而是审校任务,需要在刚才生成的原文基础上额外生成:
1. 一份完整的"含错译文"全文——即对原文做正常翻译,但故意在其中植入若干处错误
2. 每处错误需标注:该错误在"含错译文全文"中的字符起止偏移量(positionStart/positionEnd,从0开始计数,均为该含错译文字符串内的下标,不是原文中的位置)、错误类别、正确译法(correctReferenceText)
3. 错误数量与难度档位大致匹配:简单档3-4处,中等档5-6处,困难档7-8处(供参考,不强制)
4. 错误类型应覆盖多个不同类别,不要全部集中在一类
5. positionStart/positionEnd必须精确对应含错译文全文中该错误片段的实际字符位置,不允许估算,错误区间之间不得重叠

错误类别只能从以下列表中选取(errorCategory必须是下方category_key之一):
{{ for cat in error_taxonomies }}
- {{ cat.category_key }}({{ cat.category_name }})
{{ end }}

输出JSON在原有字段基础上,额外包含:
"flawedTranslationText": "<含错译文全文>",
"seededErrors": [
  {"positionStart": <int>, "positionEnd": <int>, "errorCategory": "<category_key>", "correctReferenceText": "<string>", "note": "<string或null>"}
]
{{ end }}
$tpl$,
    1,
    TRUE
);

COMMIT;
