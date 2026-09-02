-- =====================================================================
-- M9 — grading / shared_methodology 模板:注入「学习者历史薄弱点」与
--       「历次追问沉淀的评判修正补丁」两段
--
-- 背景:GradeSubmissionCommandHandler.BuildTemplateModel 现在额外提供
--   - weak_points[]    : 该用户 status=active 的薄弱点(name/description/recurring)
--   - active_overrides[]: 该考试类型 status=active 的 standard_overrides
--                         (scope/dimension_or_rule/revised_rule_text)
--   在此之前评判 prompt 完全不消费这两者(见改造清单 §2.2「未覆盖」)。
--
-- 做法:沿用 fix_grading_source_title_and_probability_prompt_template.sql 的
--       "新版本、停旧版" 约定。按 【评分关键原则】 内容标记 + is_active 命中当前
--       活跃行(种子里是 version 2,fix_grading_dimension_key 那版),停用之,插入
--       version 3:在【评卷者自我纠正的方法论】之后、"请依据以下评分维度"之前
--       插两段条件块。官方 Band 描述本身一字未改。
--
-- 本批(B1)不含 overallPassProbability——那随 grading_summaries 表在 B2 一起做。
--
-- 手动执行(Supabase)。幂等:靠 version=3 + 新标记的 NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'grading'
  AND layer = 'shared_methodology'
  AND is_active = TRUE
  AND template_content LIKE '%【评分关键原则】%';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'grading',
    'shared_methodology',
    $tpl$
【评分关键原则】
1. 判断Meaning transfer和Language proficiency是否致命,核心标准是是否达到"impact the core message"或"impact the understanding of the target text"这个质变点。
2. 官方Band 3明确包含"taken together, have a significant impact"条款——即使单个问题都不严重,多处轻微/中等问题的累积密度本身也可能构成不达标的独立理由,不能因"没有一处特别致命"就判定安全过关。
3. Application of textual norms and conventions需综合判断是否达到"mostly appropriate/consistent"(Band 2)还是仅"some demonstrated ability...appropriate...consistent"(Band 3)。
4. 凡是"无中生有地引入具体专有名词、历史事件、比例数字"这类捏造细节的错误,一律按高优先级严重问题评估。
5. 同一类错误在同一篇译文中重复出现,须明确指出这是"系统性重复错误"而非孤立笔误,并在严重度评估中加权。
6. 每次评判先定位问题落在哪个Band区间(对照完整五档英文原文),再据此给出该维度的具体Band等级,同时明确评估"累积密度"是否本身构成风险。

【核对严谨度的强制性要求】
1. 提供参考译文时,须套用与批改用户译文完全相同的核查流程和颗粒度,写完后视为待批改译文自查一遍。若为流畅度选用了语义不完全对等的简化词,须主动识别并说明这是简化处理。
2. 介词/习语结构不预设其对应中文字面结构成立:
   - "from A to B"用于纯枚举、不暗示关联/过渡时,不可机械对应"从……到……"
   - 同样原则适用于"with little warning""down the road""outside X"
   - "over + 时间":带the past/the last/recent等限定词时表示"贯穿/在这段时间内";无此类限定接纯数量时才是"超过"
   - "less A than B"等比较结构只表达程度侧重,不可具体化为原文没有的比例/数字
3. 并列结构须先划出完整语法树,确认哪些成分真正共享同一个并列连词或介词,注意"共享修饰语"现象(A or B of X)。
4. 副词修饰范围以贴近它的动词/成分为准,不要跨越并列连词修饰更远的成分。
5. 句尾分词短语状语(尤其含"years of"等时间跨度词):判断是伴随动作还是背景/原因,若为背景/原因应提到中文句首处理。
6. 完成后自查是否遗漏"看似流畅但实际有精度问题"的地方,包括细微的无理由增删(如"似乎""社会""自己"等)。
7. 生词/易混淆词须说明词典本义与语境引申义的区别,以及最终译法是完全对应、部分对应还是简化处理。
8. 区分"翻译准确性问题"和"中文表达优化问题"——语义准确但不够自然的不应直接判错;流畅但存在精度问题的必须指出。
9. 遇到高度复杂的长难句主动提供结构拆解辅助,不要直接要求给出完整译文。

【评卷者自我纠正的方法论(必须持续遵守)】
1. 不要用"考官式扣分理由"把风格精度问题过度拔高成"严重逻辑偏离",但也不要对捏造细节或核心术语方向性误译判断过轻。
2. 不要想当然认为某维度"通过线数字更高=要求更严格",须核对该Band等级具体英文描述。
3. 警惕并识别欧化中文结构:"对……的+名词"、生硬被动直译、让步状语生硬前置/后置、抽象名词化搭配。
4. 警惕自己在提供参考译文时无理由增添"看似合理"的修饰词、语气词、因果/关联关系,或捏造细节。
5. 参考译文不是绝对正确答案,需要接受同等严格、同等颗粒度的自查。
6. 术语解释要说明专业语境原意,承认为可读性做简化是合理的,但须明确告知这是简化而非精确对应。
7. 不要假设某个语法结构的修饰/并列关系"显而易见",主动先做语法树拆解再下结论。
8. 数字类错误一律按纸面呈现结果评判严重程度。
9. 首次给出的严重度判断如经追问重新审视后发现证据不足或过轻,应坦诚上调/下调。
10. 用户独立提交的练习材料同样按完整标准评估,不因缺少Translation Brief而降低评判严谨度。
11. 同一类错误在全篇多次出现时,须明确标注为"系统性重复错误"。

{{ if weak_points.size > 0 }}
【本学习者的历史薄弱点(须逐条核查本次译文是否复发;凡复发的,必须在对应维度的rationale中点名指出,并计入该维度的累积密度评估)】
{{ for w in weak_points }}
- {{ w.name }}{{ if w.recurring }}(已多次复发,应重点警惕){{ end }}:{{ w.description }}
{{ end }}
{{ end }}
{{ if active_overrides.size > 0 }}
【历次追问沉淀的评判修正补丁(必须遵守。这些不改写下方官方Band描述,而是纠正评卷者以往在同类情形下被确认过的误判倾向)】
{{ for o in active_overrides }}
- [{{ o.scope }} / {{ o.dimension_or_rule }}] {{ o.revised_rule_text }}
{{ end }}
{{ end }}

请依据以下评分维度对译文进行评判。每个维度下方的 dimension_key 是它的机器标识:输出 JSON 的 dimensions[].dimensionKey 以及 errors[].dimensionKey 必须【原样】使用该 dimension_key 字符串,绝对不要用维度名称、名称的下划线形式或任何其他写法。
{{ for dim in dimensions }}
### {{ dim.dimension_name }}(通过线: {{ dim.pass_threshold }})
dimension_key: {{ dim.dimension_key }}
{{ for band in dim.level_descriptions }}
Band {{ band.key }}: {{ band.value }}
{{ end }}
{{ end }}

请依据以下错误类别对发现的问题分类。每一项前面的 category_key 是机器标识:输出 JSON 的 errors[].errorCategory 必须【原样】使用该 category_key。
{{ for cat in error_taxonomies }}
- {{ cat.category_key }}({{ cat.category_name }}): {{ cat.description }}
{{ end }}

评判要零容忍,标题、标点、语法、句子结构、逻辑关系逐句逐词核查,明确指出每处错误所属评分维度、大致Band区间、是否影响核心信息或理解。
$tpl$,
    3,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'grading'
      AND layer = 'shared_methodology'
      AND version = 3
      AND template_content LIKE '%历次追问沉淀的评判修正补丁%'
);

COMMIT;

-- 验证:
-- SELECT layer, version, is_active, left(template_content, 24)
-- FROM prompt_templates WHERE template_type = 'grading' ORDER BY layer, version;
-- -> 【评分关键原则】 行:version 2 (is_active=false) + version 3 (is_active=true)
