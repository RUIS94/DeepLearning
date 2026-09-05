-- =====================================================================
-- weak_point_detection_criteria 提示词:整表重置为 v1(NAATI CT 英译中专属)。
--
-- 删除 prompt_templates 中该 template_type 的所有历史行,重新以 version=1、
-- layer='exam_specific'、exam_type_id 固定字面量写入唯一一行。取代此前的
-- add_weak_point_detection_criteria_prompt_template.sql + fix_weak_point_prompts_exam_specific.sql。
--
-- 内容修正:要求 criteria 自包含。复核 AI(weak_point_recheck)只拿到 catalog_code
-- 和这段 criteria,看不到通用定义,也看不到历史错误证据,所以规则必须自己把该
-- 检查什么讲清楚,不能依赖读者已知这是什么薄弱点。
--
-- model 字段(WeakPointDetectionCriteriaGenerator 提供):
--   weak_points[]: catalog_code / catalog_name / catalog_description / historical_errors[](snippet/explanation)
--
-- 手动执行(Supabase)。幂等:DELETE + INSERT,重复执行结果一致。
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'weak_point_detection_criteria';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'weak_point_detection_criteria',
    'exam_specific',
    $tpl$
你在为翻译学习者的薄弱点生成「筛查标准」——一条给后续 AI 复核使用的可执行规则,用来判断:①一段新的原文里是否存在这类易错陷阱,②如果存在,译文是否处理正确。

注意:复核 AI 到时候只会看到 catalog_code 和你写的这段 criteria 文本,看不到下面的通用定义,也看不到学习者的历史错误证据。因此 criteria 必须自包含、可独立执行——用你自己的话把"要检查什么语言现象、什么情况算陷阱存在、什么情况算处理正确"讲清楚,不能依赖复核 AI 已经知道这条薄弱点是什么,也不要只是简单复述通用定义原文。

对下面每一个薄弱点,结合它的通用定义和该学习者过往在这类问题上的具体错误证据,写一条规则,格式参考:"【关键提示词/结构特征】遇到 xxx 结构时,检查译文是否 xxx"。规则要具体、可执行,不要泛泛而谈。

{{ for w in weak_points }}
【薄弱点】{{ w.catalog_code }} = {{ w.catalog_name }}
通用定义:{{ w.catalog_description }}
该学习者的历史错误证据:
{{ for e in w.historical_errors }}
- 片段:{{ e.snippet }};说明:{{ e.explanation }}
{{ end }}
{{ end }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"detectionCriteria": [{"catalogCode": "<原样照抄上面的薄弱点 catalog_code>", "criteria": "<该薄弱点的筛查标准规则文本,需自包含、无需依赖上方定义即可被理解和执行>"}]}
上面每一个薄弱点都要在 detectionCriteria 里出现且仅出现一次。
$tpl$,
    1,
    TRUE
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates WHERE template_type = 'weak_point_detection_criteria';
-- -> 唯一一行:layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111',
--    subject_category=NULL, version=1, is_active=true
