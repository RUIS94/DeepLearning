-- =====================================================================
-- B4 — deep_learning 模板 v4:可否直译字段 + 跨题去重回喂 + 输出量收敛
--
-- 相对 v2 的改动:
--   1. vocabExpressions 每条新增 literalTranslatable(true=可直译 / false=习语
--      比喻等不可机械直译 / null=拿不准) —— 原始提示词第九节。
--   2. 顶部新增 {{ if prior_vocab.size > 0 }} 段:handler 把「此前在其它篇目
--      积累过、且 canonical_key 在本文原文里再次出现」的 vocab 喂进来,要求结合
--      本文语境重新说明、指出与旧笔记差异 —— 第九节。prior_vocab 为空时整段渲染为空。
--   3. 【输出量收敛】v2/v3 要求「宁全勿缺、系统性穷举、不要只给三五条」,配合
--      thinking 模型 + MaxTokens 会把 JSON 截断在 vocabExpressions 中段导致解析
--      失败、重试三次全废。v4 改为「句型 4–8 条、词汇 12–20 条,只收高价值的,
--      宁精勿滥」,配合 handler 把 MaxTokens 提到 8192,输出能完整闭合。
--
-- 沿用「新版本、停旧版」。手动执行(Supabase)。幂等:version=4 + 新标记 NOT EXISTS。
-- 会停用当前所有 active 的 deep_learning/shared_methodology 行(无论你现在停在 v2 还是 v3)。
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'deep_learning'
  AND layer = 'shared_methodology'
  AND is_active = TRUE;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'deep_learning',
    'shared_methodology',
    $tpl$
【原文】
{{ source_text }}

任务类型:{{ task_type }}

{{ if prior_vocab.size > 0 }}
【以下表达此前已在其它篇目积累过】
{{ for pv in prior_vocab }}
- {{ pv.english_expr }}{{ if pv.chinese_equiv }}({{ pv.chinese_equiv }}){{ end }}{{ if pv.context_note }} —— 旧笔记:{{ pv.context_note }}{{ end }}
{{ end }}
若上述表达在本文再次出现:请结合本文语境重新说明其含义 / 译法,并指出与旧笔记的差异(如语境义不同、搭配不同、语域不同);不要原样照抄旧条目,也不要因为已积累就略去不收。若某条其实没有在本文出现,忽略即可。
{{ end }}

请针对以上原文,为正在备考翻译考试的学习者生成一份精炼、可直接用于复习积累的"深入学习"材料,包含四部分:

1. 一份高质量的标准参考译文(仅供学习对照,不作为评分唯一依据)。

2. 3–6 条关于本文翻译技巧 / 常见误译陷阱的简短笔记(comparisonNotes)。

3. 句型拆解(sentencePatterns):挑 4–8 条对翻译最有借鉴价值的结构,不要只挑"最长的那句",覆盖不同类型,如:
   - 长难句 / 多重修饰(定语从句叠加、同位语、插入语、并列宾语)
   - 被动语态、名词化结构(nominalization)、there be、形式主语 it、强调句
   - 条件 / 让步 / 目的状语从句,非谓语动词作状语或后置定语
   - 公告 / 法律 / 医疗等文体的惯用框架句(如 "X will do Y from + 日期"、"Those who … should …")
   对每一条:
   - patternName: 用简洁的中文/英文混合式命名点出结构骨架
   - exampleSentence: 原文中的原句(不要改写)
   - breakdownSteps: 一个对象,键名自拟但要覆盖"主干 / 修饰成分 / 中英语序差异 / 翻译处理要点"这几个角度,把这句到底难在哪、中文该怎么落地讲透
   - variants: 同结构的常见变体说法(字符串或 null)
   - domain / scenario / frequencyTag: 见下

4. 词汇与表达(vocabExpressions):从原文里挑 12–20 条最值得积累的词汇与表达,优先高频、易误译、有迁移价值的;不必穷举,宁精勿滥。尽量覆盖多种类别(category 字段填下面这些中文标签之一):
   - 专业术语:某一领域(法律 / 医疗 / 政务 / 金融 / 教育 / 移民等)的行话、规范译名
   - 机构与专名:机构名、职位名、法案 / 项目 / 表格名称、缩略语及其全称
   - 固定搭配:动词 + 名词 / 形容词 + 名词等词典级搭配(collocation)
   - 短语动词:phrasal verb(如 carry out、opt in)
   - 介词搭配:与特定介词绑定的用法(如 eligible for、subject to)
   - 习语与比喻:idiom、比喻性表达、谚语
   - 俚语与口语:非正式、口语化或地区性说法,以及它在正式译文里应如何处理
   - 常用短语 / 句式碎片:高频功能性表达(如 as soon as possible、in the event that)
   - 易混词 / 假朋友:形近或看似对应、实则译法不同的词(false friend)
   - 数量与范围表述:and over、up to、within、no later than 等端点/范围词
   对每一条:
   - englishExpr: 原文中的英文表达(可含少量上下文,便于定位)
   - chineseEquiv: 推荐中文译法,必要时给多个;或 null
   - contextNote: 词典本义 / 引申义 / 本文语境义的区分,易错点,语域提示;或 null
   - category: 上面列出的中文标签
   - domain: 领域(如"法律""医疗""政府公告"),或 null
   - scenario: 应用场景(如"公告通知""信函往来""口译对话"),或 null
   - frequencyTag: "高频" / "中频" / "低频",或 null
   - literalTranslatable: 布尔或 null。true = 可照字面直译;false = 习语、比喻性表达、意思与字面差异大的短语(不可机械直译);拿不准填 null

原则:宁精勿滥——只收真正值得学习者记下来的;每条都必须真实出现在原文中、或与原文内容直接相关,不得为了凑数而杜撰。

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "referenceText": "<完整参考译文>",
  "comparisonNotes": ["<技巧/易错点笔记1>", "<技巧/易错点笔记2>"],
  "sentencePatterns": [
    {"patternName": "<句型名称>", "exampleSentence": "<原文中的例句>", "breakdownSteps": {"主干": "...", "修饰成分": "...", "语序差异": "...", "翻译要点": "..."}, "variants": "<常见变体或string或null>", "domain": "<领域,如法律/医疗/政府公告,或null>", "scenario": "<应用场景,或null>", "frequencyTag": "<高频/中频/低频,或null>"}
  ],
  "vocabExpressions": [
    {"englishExpr": "<英文表达>", "chineseEquiv": "<中文对应,或null>", "contextNote": "<词典本义/引申义/语境义区分,或null>", "category": "<上面列出的中文分类标签,或null>", "domain": "<领域,或null>", "scenario": "<应用场景,或null>", "frequencyTag": "<高频/中频/低频,或null>", "literalTranslatable": <true 或 false 或 null>}
  ]
}
$tpl$,
    4,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'deep_learning'
      AND layer = 'shared_methodology'
      AND version = 4
      AND template_content LIKE '%【以下表达此前已在其它篇目积累过】%'
);

COMMIT;

-- 验证:
-- SELECT version, is_active, left(template_content, 16)
-- FROM prompt_templates WHERE template_type = 'deep_learning' ORDER BY version;
-- -> version 1/2/3 (is_active=false) + 4 (is_active=true)
