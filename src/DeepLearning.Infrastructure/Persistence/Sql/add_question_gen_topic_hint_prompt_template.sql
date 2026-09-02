-- =====================================================================
-- B3 — question_gen 题材倾向提示
--
-- GenerateQuestionCommandHandler 现在会把 topic_hint 传入 question_gen 模板 model:
--   - 调用方显式指定 CategoryId 时 = 该分类名
--   - 未指定时,按 generation_policy.topic_distribution 掷骰,命中则从已有 domain
--     分类里随机取一个名字;未命中为 null
--
-- 这里新增一条独立的 shared_methodology/question_gen 行(加法,IExamConfigLoader
-- 会把每条 active 行拼接;做法与 add_weak_point_targeting_question_gen_prompt_template.sql
-- 完全一致)。topic_hint 为 null 时 Scriban 的 {{ if }} 判定为假,整条渲染为空,
-- 对未命中的调用是彻底 no-op。
--
-- 手动执行(Supabase)。幂等:NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'question_gen',
    'shared_methodology',
    $tpl$
{{ if topic_hint }}
【题材倾向】本次出题优先选取「{{ topic_hint }}」领域的真实文章。若该领域确实没有合适的真实素材,可适当放宽,但不要为贴合题材而牺牲文章的真实性与自然度。
{{ end }}
$tpl$,
    1,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'question_gen'
      AND layer = 'shared_methodology'
      AND template_content LIKE '%【题材倾向】%'
);

COMMIT;

-- 验证:
-- SELECT layer, version, is_active, left(template_content, 20)
-- FROM prompt_templates WHERE template_type = 'question_gen' ORDER BY layer, version;
