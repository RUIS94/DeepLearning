-- =====================================================================
-- weak_point_detection_criteria 提示词(新 template_type,首次插入)。
--
-- 背景:薄弱点分类与生命周期管理_策划书.md §2/§3。仅在某薄弱点首次累计满 3 次
-- 提交命中(tracking→active),或 resolved 后又复现时触发,产出一条可执行的
-- 「筛查该类错误的标准」规则文本,写入 weak_points.detection_criteria,供
-- weak_point_recheck 复核时使用。一次调用批量处理本轮所有需要(重新)生成的薄弱点。
--
-- model 字段(WeakPointDetectionCriteriaGenerator 提供):
--   weak_points[]: catalog_code / catalog_name / catalog_description / historical_errors[](snippet/explanation)
--
-- 手动执行(Supabase)。幂等:template_type 首次插入的 NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'weak_point_detection_criteria',
    'shared_methodology',
    $tpl$
你在为翻译学习者的薄弱点生成「筛查标准」——一条给后续 AI 复核使用的可执行规则,用来判断:①一段新的原文里是否存在这类易错陷阱,②如果存在,译文是否处理正确。

对下面每一个薄弱点,结合它的通用定义和该学习者过往在这类问题上的具体错误证据,写一条规则,格式参考:"【关键提示词/结构特征】遇到 xxx 结构时,检查译文是否 xxx"。规则要具体、可执行,不要泛泛而谈,不要重复该薄弱点的通用定义原文。

{{ for w in weak_points }}
【薄弱点】{{ w.catalog_code }} = {{ w.catalog_name }}
通用定义:{{ w.catalog_description }}
该学习者的历史错误证据:
{{ for e in w.historical_errors }}
- 片段:{{ e.snippet }};说明:{{ e.explanation }}
{{ end }}
{{ end }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"detectionCriteria": [{"catalogCode": "<原样照抄上面的薄弱点 catalog_code>", "criteria": "<该薄弱点的筛查标准规则文本>"}]}
上面每一个薄弱点都要在 detectionCriteria 里出现且仅出现一次。
$tpl$,
    1,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates WHERE template_type = 'weak_point_detection_criteria'
);

COMMIT;

-- 验证:SELECT template_type, version, is_active FROM prompt_templates WHERE template_type = 'weak_point_detection_criteria';
