-- =====================================================================
-- weak_point_classification 提示词 v2:除了把错误映射到 catalog code,同一趟再
-- 产出「该学习者在这个薄弱点上的滚动个性化摘要」(pattern_summary)。
--
-- 背景:WeakPointClassifier 现在多传一个 activeWeakPoints[](该用户当前 active 且
-- 已映射 catalog 的薄弱点 + 其现有 pattern_summary),让 AI 做「旧摘要 + 本篇新证据」
-- 的合并,而不是从零重写。UpdateWeakPointsOnGraded 把返回的摘要写进
-- weak_points.pattern_summary —— 这是评判 prompt 真正注入的那段文字(取代旧的
-- description 副本)。legacy(无 catalog)桶不参与本调用,由代码给确定性字符串。
--
-- model 字段(WeakPointClassifier 提供):
--   errors[]            : error_id / dimension_key / error_category_key / severity / snippet / explanation
--   catalog[]           : code / name / description
--   active_weak_points[]: code / pattern_summary  (可能为空字符串 = 还没有摘要)
--
-- 沿用 "新版本、停旧版" 约定,按内容标记 + template_type 命中当前活跃行。
-- 手动执行(Supabase)。幂等:version=2 的 NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'weak_point_classification'
  AND is_active = TRUE
  AND template_content LIKE '%薄弱点归类%';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'weak_point_classification',
    'shared_methodology',
    $tpl$
你在为一次翻译评判发现的错误做「薄弱点归类」,并顺带更新该学习者的薄弱点个性化摘要。

【任务一:归类】为每一处错误指派最贴切的一个薄弱点 code;若清单里没有任何 code 明显贴切,该错误的 catalogCode 填 null。只依据错误本身呈现的语言学特征判断,不臆测,不为了「都归上类」而勉强指派。

【任务二:更新摘要】对本次至少被指派了一处错误的每个 code,产出一条更新后的 patternSummary(≤120 字中文):把该 code 下方「现有摘要」与本次这些错误体现的新证据合并成一句话,概括【这个学习者】在这类问题上的具体表现倾向(不是这条 catalog 的通用定义)。现有摘要为空则新写一条。没有被指派错误的 code 不要出现在 summaries 里。

【本次错误清单】
{{ for e in errors }}
- errorId={{ e.error_id }} | 评分维度={{ e.dimension_key }} | 当前错误类别={{ e.error_category_key }} | 严重度={{ e.severity }}
  片段:{{ e.snippet }}
  评判说明:{{ e.explanation }}
{{ end }}

【规范薄弱点清单】
{{ for c in catalog }}
- {{ c.code }} = {{ c.name }}:{{ c.description }}
{{ end }}

【该学习者当前薄弱点的现有个性化摘要】
{{ for w in active_weak_points }}
- {{ w.code }}:{{ w.pattern_summary }}
{{ end }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"assignments": [{"errorId": "<原样照抄上面的 errorId>", "catalogCode": "<清单中的某个 code,或 null>"}],
 "summaries": [{"catalogCode": "<本次被指派了错误的 code>", "patternSummary": "<≤120字中文,合并后的个性化摘要>"}]}
assignments 里上面每一处错误都要出现且仅出现一次;summaries 只含本次被指派了错误的 code。
$tpl$,
    2,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'weak_point_classification'
      AND version = 2
      AND template_content LIKE '%更新该学习者的薄弱点个性化摘要%'
);

COMMIT;

-- 验证:
-- SELECT template_type, version, is_active, left(template_content, 30)
-- FROM prompt_templates WHERE template_type = 'weak_point_classification' ORDER BY version;
-- -> version 1 (is_active=false) + version 2 (is_active=true)
