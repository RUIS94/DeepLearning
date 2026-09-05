-- =====================================================================
-- weak_point_classification 提示词 v3:适配两级分类体系(8 一级 + 全局共享的
-- 二级叶子),并允许 AI 在判断现有叶子都不贴切时,建议一个新叶子(status=proposed,
-- 等人工审核转正,不会被本次归类立即使用)。
--
-- 背景:薄弱点分类与生命周期管理_策划书.md §1.4。catalog 参数从旧的扁平
-- code/name/description 列表,改为 categories[](按一级分类分组)+
-- uncategorized_leaves[](待审核、还没分类的叶子,仍参与匹配)。
--
-- model 字段(WeakPointClassifier 提供):
--   errors[]              : error_id / dimension_key / error_category_key / severity / snippet / explanation
--   categories[]          : category_code / category_name / leaves[](code/name/description)
--   uncategorized_leaves[]: code / name / description
--   active_weak_points[]  : code / pattern_summary
--
-- 沿用 "新版本、停旧版" 约定。手动执行(Supabase)。幂等:version=3 的 NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'weak_point_classification'
  AND is_active = TRUE;

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

【任务一:归类】为每一处错误在下方【规范薄弱点清单】里指派最贴切的一个叶子 code。只依据错误本身呈现的语言学特征判断,不臆测,不为了「都归上类」而勉强指派。
- 若某个一级分类下的某条叶子明显贴切,直接用它的 code。
- 若清单里(含"待审核叶子")没有任何一条真正贴切,该错误的 catalogCode 填 null。
- 只有当你确信这类错误代表一种清单里完全没有覆盖到的、有必要单独追踪的新模式时,才在 proposedNewLeaf 里给出建议:categoryCode 必须是下方 8 个一级分类之一,code 为小写字母数字下划线(如 semantic_xxx),name 给"英文 / 中文"两种叫法,description 一句话精简说明。不要为了凑数或碰到罕见错误就轻易提议新叶子——清单已覆盖绝大多数常见模式,新建应该是例外。catalogCode 不为 null 时,proposedNewLeaf 必须为 null。

【任务二:更新摘要】对本次至少被指派了一处错误的每个叶子 code,产出一条更新后的 patternSummary(≤120 字中文):把该 code 下方「现有摘要」与本次这些错误体现的新证据合并成一句话,概括【这个学习者】在这类问题上的具体表现倾向(不是这条 catalog 的通用定义)。现有摘要为空则新写一条。没有被指派错误的 code 不要出现在 summaries 里。

【本次错误清单】
{{ for e in errors }}
- errorId={{ e.error_id }} | 评分维度={{ e.dimension_key }} | 当前错误类别={{ e.error_category_key }} | 严重度={{ e.severity }}
  片段:{{ e.snippet }}
  评判说明:{{ e.explanation }}
{{ end }}

【规范薄弱点清单(按一级分类分组)】
{{ for cat in categories }}
◆ {{ cat.category_code }} = {{ cat.category_name }}
{{ for l in cat.leaves }}
  - {{ l.code }} = {{ l.name }}:{{ l.description }}
{{ end }}
{{ end }}
{{ if uncategorized_leaves.size > 0 }}
◆ 待审核叶子(尚未归入一级分类,但仍可使用)
{{ for l in uncategorized_leaves }}
  - {{ l.code }} = {{ l.name }}:{{ l.description }}
{{ end }}
{{ end }}

【该学习者当前薄弱点的现有个性化摘要】
{{ for w in active_weak_points }}
- {{ w.code }}:{{ w.pattern_summary }}
{{ end }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"assignments": [{"errorId": "<原样照抄上面的 errorId>", "catalogCode": "<清单中的某个叶子 code,或 null>", "proposedNewLeaf": {"categoryCode": "<8个一级分类之一>", "code": "<新叶子code>", "name": "<英文 / 中文>", "description": "<精简说明>"} 或 null}],
 "summaries": [{"catalogCode": "<本次被指派了错误的叶子 code>", "patternSummary": "<≤120字中文,合并后的个性化摘要>"}]}
assignments 里上面每一处错误都要出现且仅出现一次;summaries 只含本次被指派了错误的叶子 code;proposedNewLeaf 只在 catalogCode 为 null 且确有必要时才给出,否则整个字段填 null。
$tpl$,
    3,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'weak_point_classification'
      AND version = 3
);

COMMIT;

-- 验证:
-- SELECT template_type, version, is_active FROM prompt_templates
-- WHERE template_type = 'weak_point_classification' ORDER BY version;
-- -> version 1/2 (is_active=false) + version 3 (is_active=true)
