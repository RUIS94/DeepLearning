-- =====================================================================
-- weak_point_recheck 提示词(新 template_type,首次插入)。
--
-- 背景:薄弱点分类与生命周期管理_策划书.md §3。每次评判后,对该用户当前
-- active 且【本次未被 weak_point_classification 命中】的薄弱点,批量做一次
-- 复核:结合各自的筛查标准(detection_criteria),判断本次原文+译文对每个
-- 薄弱点分别是 resolved / still_weak / not_present。这次复核不产生新的错误
-- 记录,只用于决定是否维持 active。
--
-- model 字段(WeakPointRecheckService 提供):
--   candidates[] : catalog_code / detection_criteria
--   source_text  : 本次原文全文
--   translation_text: 用户本次译文全文
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
    'weak_point_recheck',
    'shared_methodology',
    $tpl$
你在复核一份翻译提交,判断学习者的几个历史薄弱点这次是否还应该保持「重点关注」状态。下面每个薄弱点都配有一条筛查标准,请只依据筛查标准去检查本次原文和译文,不要理会原文译文中其它类型的问题。

对每个薄弱点,给出以下三种结果之一:
- resolved:筛查标准描述的陷阱在原文中确实存在,且译文处理正确 —— 说明这次是有力的正面证据,可以不再重点关注。
- still_weak:筛查标准描述的陷阱在原文中确实存在,但译文处理得不正确 —— 说明这个问题依然存在。
- not_present:原文里根本不存在筛查标准描述的这种陷阱 —— 无法判断好坏,这次不构成任何证据。

{{ for c in candidates }}
【薄弱点】{{ c.catalog_code }}
筛查标准:{{ c.detection_criteria }}
{{ end }}

【原文】
{{ source_text }}

【译文】
{{ translation_text }}

严格只输出以下 JSON,不要 markdown 代码块围栏,不要任何多余文字:
{"results": [{"catalogCode": "<原样照抄上面的薄弱点 catalog_code>", "outcome": "resolved|still_weak|not_present"}]}
上面每一个薄弱点都要在 results 里出现且仅出现一次。
$tpl$,
    1,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates WHERE template_type = 'weak_point_recheck'
);

COMMIT;

-- 验证:SELECT template_type, version, is_active FROM prompt_templates WHERE template_type = 'weak_point_recheck';
