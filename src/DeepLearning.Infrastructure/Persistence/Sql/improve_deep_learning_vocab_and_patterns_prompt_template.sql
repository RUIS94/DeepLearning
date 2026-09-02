-- =====================================================================
-- Deep learning (design decision, 2026-09-02): the version=1 shared_methodology/
-- deep_learning row (add_deep_learning_prompt_template.sql) produced "词汇与表达"
-- and "句型拆解" material that was too thin — a token 2-3 vocab items, "长难句"
-- only, and it actively told the model to leave arrays empty ("不要牵强凑数…
-- 对应数组可以为空"). Users want the vocab list to actually cover the source's
-- lexical surface: domain terminology, fixed collocations, phrasal verbs, idioms,
-- slang/colloquialisms, set phrases, institution names/abbreviations, false
-- friends — and the sentence-pattern breakdown to be a real structural analysis,
-- not a one-line gloss.
--
-- Version=2 replacement of the existing shared_methodology/deep_learning row
-- (version=1) — same "new version, deactivate old" convention as
-- add_followup_thread_history_prompt_template.sql /
-- fix_grading_dimension_key_prompt_template.sql. Matched by (template_type,
-- layer, version) since the v1 row was inserted without an explicit id literal.
--
-- The JSON contract is byte-for-byte the same shape v1 emitted
-- (GenerateDeepLearningContentCommandHandler.DeepLearningPayload is unchanged):
-- referenceText / comparisonNotes[] / sentencePatterns[] / vocabExpressions[],
-- with the same field names. Only the instructions and the `category` guidance
-- changed. `breakdownSteps` stays "object OR string" (handler stores GetRawText()
-- either way; the frontend BreakdownSteps component renders both).
--
-- Still honours design doc §10.2's isolation guarantee: the prompt is given ONLY
-- task_type + source_text, never a submission / grading_results /
-- meaning_checkpoints.
--
-- If deep-learning generation starts failing to parse after this, check this row
-- (prompt_templates where template_type='deep_learning' AND
-- layer='shared_methodology') hasn't been deactivated or superseded incompatibly.
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'deep_learning'
  AND layer = 'shared_methodology'
  AND version = 1
  AND is_active = TRUE;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'deep_learning',
    'shared_methodology',
    $tpl$
【原文】
{{ source_text }}

任务类型:{{ task_type }}

请针对以上原文,为正在备考翻译考试的学习者生成一份尽量完整、可直接用于复习积累的"深入学习"材料,包含四部分:

1. 一份高质量的标准参考译文(仅供学习对照,不作为评分唯一依据)。

2. 若干条关于本文翻译技巧 / 常见误译陷阱的简短笔记(comparisonNotes)。

3. 句型拆解(sentencePatterns):不要只挑"最长的那句"。凡是原文中出现、且对翻译有借鉴价值的结构都值得收录,包括但不限于:
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

4. 词汇与表达(vocabExpressions):这是重点,要求覆盖面广。请系统性地扫一遍原文,把所有值得学习者积累的词汇与表达都提取出来,不要只给三五条。需要覆盖的类别(category 字段就填下面这些中文标签之一):
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

原则:宁全勿缺——句型和词汇都尽量给足。但每一条都必须真实出现在原文中、或与原文内容直接相关,不得为了凑数而杜撰。原文中若确实没有可收录的某一类,对应条目可以少或没有,但不要整体偷懒。

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "referenceText": "<完整参考译文>",
  "comparisonNotes": ["<技巧/易错点笔记1>", "<技巧/易错点笔记2>"],
  "sentencePatterns": [
    {"patternName": "<句型名称>", "exampleSentence": "<原文中的例句>", "breakdownSteps": {"主干": "...", "修饰成分": "...", "语序差异": "...", "翻译要点": "..."}, "variants": "<常见变体或string或null>", "domain": "<领域,如法律/医疗/政府公告,或null>", "scenario": "<应用场景,或null>", "frequencyTag": "<高频/中频/低频,或null>"}
  ],
  "vocabExpressions": [
    {"englishExpr": "<英文表达>", "chineseEquiv": "<中文对应,或null>", "contextNote": "<词典本义/引申义/语境义区分,或null>", "category": "<上面列出的中文分类标签,或null>", "domain": "<领域,或null>", "scenario": "<应用场景,或null>", "frequencyTag": "<高频/中频/低频,或null>"}
  ]
}
$tpl$,
    2,
    TRUE
);

COMMIT;
