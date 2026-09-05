-- =====================================================================
-- weak_point_recheck 提示词:整表重置为 v1(NAATI CT 英译中专属)。
--
-- 删除 prompt_templates 中该 template_type 的所有历史行,重新以 version=1、
-- layer='exam_specific'、exam_type_id 固定字面量写入唯一一行。取代此前的
-- add_weak_point_recheck_prompt_template.sql + fix_weak_point_prompts_exam_specific.sql。
--
-- 内容修正:补一句「也不要引入筛查标准之外的判断依据」,防止 AI 用自己对该
-- code 的理解替代 criteria。三种结果(resolved / still_weak / not_present)定义未变。
--
-- model 字段(WeakPointRecheckService 提供):
--   candidates[]     : catalog_code / detection_criteria
--   source_text      : 本次原文全文
--   translation_text : 用户本次译文全文
--
-- 手动执行(Supabase)。幂等:DELETE + INSERT,重复执行结果一致。
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'weak_point_recheck';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'weak_point_recheck',
    'exam_specific',
    $tpl$
你在复核一份翻译提交,判断学习者的几个历史薄弱点这次是否还应该保持「重点关注」状态。下面每个薄弱点都配有一条筛查标准,请只依据筛查标准去检查本次原文和译文,不要理会原文译文中其它类型的问题,也不要引入筛查标准之外的判断依据。

对每个薄弱点,给出以下三种结果之一:
- resolved:筛查标准描述的陷阱在原文中确实存在,且译文处理正确 —— 说明这次是有力的正面证据,可以不再重点关注。
- still_weak:筛查标准描述的陷阱在原文中确实存在,但译文处理得不正确 —— 说明这个问题依然存在。
- not_present:原文里根本不存在筛查标准描述的这种陷阱 —— 无法判断好坏,这次不构成任何证据。

{{ for c in candidates }}
【薄弱点 {{ c.catalog_code }}】
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
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates WHERE template_type = 'weak_point_recheck';
-- -> 唯一一行:layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111',
--    subject_category=NULL, version=1, is_active=true
