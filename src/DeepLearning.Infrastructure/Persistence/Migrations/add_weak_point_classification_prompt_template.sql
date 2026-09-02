-- =====================================================================
-- B5 (M10) — weak_point_classification 提示词
--
-- WeakPointClassifier(Infrastructure/Ai)在每次评判后调一次,把本次错误清单
-- 映射到 weak_point_catalog 的 code。用途:B1 的纯规则分桶按
-- (default_dimension_key, default_error_category) 匹配,无法区分同属
-- (meaning_transfer, distortion) 的「数字陷阱 / 逻辑关系还原 / 形近词混淆」——
-- 这些只能靠 AI 看错误本身的语言学特征来分。
--
-- 可选:不建这一行,WeakPointClassifier 的 BuildPromptAsync 返回空 -> 直接回退
-- 纯规则,行为与 B1 完全一致。建了则 AI 归类优先、规则兜底。
--
-- model 字段(WeakPointClassifier 提供):
--   errors[]  : error_id / dimension_key / error_category_key / impacts_core / snippet / explanation
--   catalog[] : code / name / description
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
    'weak_point_classification',
    'shared_methodology',
    $tpl$
你在为一次翻译评判发现的错误做「薄弱点归类」。下面是本次的错误清单,以及本考试类型的规范薄弱点清单。

请为每一处错误指派最贴切的一个薄弱点 code;若清单里没有任何 code 明显贴切,该错误的 catalogCode 填 null。只依据错误本身呈现的语言学特征判断,不要臆测,也不要为了「都归上类」而勉强指派。

【错误清单】
{{ for e in errors }}
- errorId={{ e.error_id }} | 评分维度={{ e.dimension_key }} | 当前错误类别={{ e.error_category_key }} | 是否影响核心={{ e.impacts_core }}
  片段:{{ e.snippet }}
  评判说明:{{ e.explanation }}
{{ end }}

【规范薄弱点清单】
{{ for c in catalog }}
- {{ c.code }} = {{ c.name }}:{{ c.description }}
{{ end }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"assignments": [{"errorId": "<原样照抄上面的 errorId>", "catalogCode": "<清单中的某个 code,或 null>"}]}
上面每一处错误都要在 assignments 里出现且仅出现一次。
$tpl$,
    1,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'weak_point_classification'
      AND layer = 'shared_methodology'
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, is_active FROM prompt_templates
-- WHERE template_type = 'weak_point_classification';
