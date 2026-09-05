-- =====================================================================
-- weak_point_classification 提示词:整表重置为 v1(NAATI CT 英译中专属)。
--
-- 本脚本删除 prompt_templates 中该 template_type 的所有历史行(v1/v2/v3/v4 全部),
-- 重新以 version=1、layer='exam_specific'、exam_type_id 固定字面量写入唯一一行。
-- 取代此前的 upgrade_weak_point_classification_prompt_v3_two_level_taxonomy.sql /
-- fix_weak_point_classification_prompt_add_summaries.sql /
-- fix_weak_point_prompts_exam_specific.sql —— 这三个操作的最终效果都并入这里。
--
-- 内容上相对旧 v3 的两处修正(见 后端进度跟踪.md / 策划书 §1.4、§2):
--   1) 待审核(proposed)叶子仅供参考、避免重复提议,禁止用作 catalogCode。
--   2) 摘要合并的上下文由「仅 active」改为「该 code 任意状态(tracking/active/resolved)
--      下的现有摘要」,避免覆盖重写。
-- 后端同步:WeakPointClassifier 只把 status=active 的叶子作为可选 catalogCode,
-- 并改用 ListCatalogMappedWithCatalogByUserAsync(任意状态、catalog 映射)。
--
-- model 字段(WeakPointClassifier 提供):
--   errors[]                     : error_id / dimension_key / error_category_key / severity / snippet / explanation
--   categories[]                 : category_code / category_name / leaves[](code/name/description)   —— 均为 active
--   uncategorized_active_leaves[]: code / name / description   —— active 但未归一级分类,仍可选
--   pending_proposed_leaves[]    : code / name / description / category_code   —— proposed,仅展示,禁止用作 catalogCode
--   existing_weak_points[]       : code / pattern_summary   —— 该用户此 code 下的记录,任意状态
--
-- 手动执行(Supabase SQL Editor 或 psql)。幂等:DELETE + INSERT,重复执行结果一致。
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'weak_point_classification';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'weak_point_classification',
    'exam_specific',
    $tpl$
你在为一次翻译评判发现的错误做「薄弱点归类」,并顺带更新该学习者的薄弱点个性化摘要。

【任务一:归类】为每一处错误在下方【规范薄弱点清单】里指派最贴切的一个叶子 code。只依据错误本身呈现的语言学特征判断,不臆测,不为了「都归上类」而勉强指派。
- catalogCode 只能是【规范薄弱点清单】中列出的叶子 code;下方【待审核的新叶子提议】仅供参考,禁止作为 catalogCode 使用——它们尚未人工审核通过,还不生效。
- 若某个一级分类下的某条叶子明显贴切,直接用它的 code。
- 若清单里没有任何一条真正贴切,该错误的 catalogCode 填 null。
- 若该错误的模式与【待审核的新叶子提议】中已有的某一条高度相似,说明已经有人在等待审核同一类新模式:直接把 catalogCode 和 proposedNewLeaf 都留 null,不要重复提议。
- 只有当你确信这类错误代表一种清单里(含待审核提议)完全没有覆盖到的、有必要单独追踪的新模式时,才在 proposedNewLeaf 里给出建议:categoryCode 必须是下方 8 个一级分类之一,code 为小写字母数字下划线(如 semantic_xxx,且不能与清单中任何已有 code 或待审核 code 重复),name 给"英文 / 中文"两种叫法,description 一句话精简说明。不要为了凑数或碰到罕见错误就轻易提议新叶子——清单已覆盖绝大多数常见模式,新建应该是例外。catalogCode 不为 null 时,proposedNewLeaf 必须为 null。

【任务二:更新摘要】对本次至少被指派了一处错误的每个叶子 code,产出一条更新后的 patternSummary(≤120 字中文):把该 code 下方「现有摘要」(不论该薄弱点当前是 tracking / active / resolved 哪种状态,只要存在摘要就要合并,不能因为状态不是 active 就当成空摘要重写)与本次这些错误体现的新证据合并成一句话,概括【这个学习者】在这类问题上的具体表现倾向(不是这条 catalog 的通用定义)。该 code 确实是该学习者第一次命中、现有摘要为空,则新写一条。没有被指派错误的 code 不要出现在 summaries 里。

【本次错误清单】
{{ for e in errors }}
- errorId={{ e.error_id }} | 评分维度={{ e.dimension_key }} | 当前错误类别={{ e.error_category_key }} | 严重度={{ e.severity }}
  片段:{{ e.snippet }}
  评判说明:{{ e.explanation }}
{{ end }}

【规范薄弱点清单(按一级分类分组,均为已审核通过的叶子,catalogCode 只能从这里选)】
{{ for cat in categories }}
◆ {{ cat.category_code }} = {{ cat.category_name }}
{{ for l in cat.leaves }}
  - {{ l.code }} = {{ l.name }}:{{ l.description }}
{{ end }}
{{ end }}
{{ if uncategorized_active_leaves.size > 0 }}
◆ (已审核通过、尚未归入某个一级分类,同样可作为 catalogCode)
{{ for l in uncategorized_active_leaves }}
  - {{ l.code }} = {{ l.name }}:{{ l.description }}
{{ end }}
{{ end }}

{{ if pending_proposed_leaves.size > 0 }}
【待人工审核的新叶子提议(仅供参考、避免重复提议,禁止用作 catalogCode)】
{{ for l in pending_proposed_leaves }}
  - {{ l.code }} = {{ l.name }}:{{ l.description }}(拟属一级分类:{{ l.category_code }})
{{ end }}
{{ end }}

【该学习者以下叶子 code 现有的个性化摘要(涵盖 tracking / active / resolved 全部状态,只要曾经写过摘要就列在这里)】
{{ for w in existing_weak_points }}
- {{ w.code }}:{{ w.pattern_summary }}
{{ end }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"assignments": [{"errorId": "<原样照抄上面的 errorId>", "catalogCode": "<清单中的某个叶子 code,或 null>", "proposedNewLeaf": {"categoryCode": "<8个一级分类之一>", "code": "<新叶子code>", "name": "<英文 / 中文>", "description": "<精简说明>"} 或 null}],
 "summaries": [{"catalogCode": "<本次被指派了错误的叶子 code>", "patternSummary": "<≤120字中文,合并后的个性化摘要>"}]}
assignments 里上面每一处错误都要出现且仅出现一次;summaries 只含本次被指派了错误的叶子 code;proposedNewLeaf 只在 catalogCode 为 null 且确有必要时才给出,否则整个字段填 null。
$tpl$,
    1,
    TRUE
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates WHERE template_type = 'weak_point_classification';
-- -> 唯一一行:layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111',
--    subject_category=NULL, version=1, is_active=true
