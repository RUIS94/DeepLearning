-- =====================================================================
-- 2026-09-06: deep_learning 提示词整表重置为唯一一行 production v1(NAATI CT 英译中专属)。
--
-- 重置前 prompt_templates 里 template_type='deep_learning' 有 5 行、2 行 active:
--   A  exam_specific  v1  active  —— 只有一段 NAATI 定位说明,无 source_text / 无 JSON 契约
--   B  shared_methodology(translation) v4  active —— 完整模板(原文 + 四部分 + JSON)
--   C  shared_methodology v1  inactive(最初版,叫模型可留空数组)
--   D  shared_methodology v2  inactive
--   E  shared_methodology v3  inactive(中间手改版,未收敛)
-- ExamConfigLoader.BuildPromptAsync 把 active 的 shared + exam_specific 拼接,所以实际提示词
-- = B + "\n\n" + A。本脚本把这套历史整体收敛成一行。
--
-- 与 reset_weak_point_*_v1_exam_specific.sql / reset_followup_prompts_v1_production.sql 同一套路:
-- DELETE 掉该 template_type 的所有历史行,重新以 version=1、layer='exam_specific'、
-- exam_type_id 固定字面量(与 fix_weak_point_prompts_exam_specific.sql 一致)、subject_category=NULL
-- 写入唯一一行。删掉 B 之后不再有 shared_methodology 行,所以这一行必须自包含:A 的 NAATI 定位
-- 内容折进开头【定位】段。
--
-- 相对 B(v4)的内容改动:
--   1. 折入 NAATI 定位段(A 的内容),并把「不使用破折号 / 插入语改括号」明确只约束 referenceText。
--   2. referenceText 补 3 条达标线:完整传达信息点(不漏译 / 不增译)、术语规范、符合中文公文
--      通知语域且断句按中文习惯重组。
--   3. comparisonNotes 收紧为「本篇实际最易译错的 3–6 处,一句话点破」,明令禁止空泛套话。
--   4. breakdownSteps 键名固定为 主干 / 修饰成分 / 语序差异 / 翻译要点(v4 正文说自拟、skeleton 又
--      给固定四键,自相矛盾;固定后复习库 UI 一致)。
--   5. frequencyTag 补一句「高频 / 中频 / 低频」的判定说明,否则标注随机。
--   6. category 长清单只在正文列一次,JSON skeleton 里不再重复。
--   7. 输出格式强化 JSON 卫生:mimo 经常在 comparisonNotes / contextNote / breakdownSteps 里
--      用未转义的英文双引号引用原词(如 把 "green IT" 译作…),导致
--      「'e' is invalid after a value … Path: $.comparisonNotes」这类解析失败、触发重试循环。
--      明令字符串内引用英文一律用中文引号「」,禁裸英文双引号 / 裸换行 / 尾随逗号。
--   8. 去掉 B(v4)的 prior_vocab 跨题去重段 —— 去重是后端的事(按 canonical_key 合并),
--      「结合新语境补充说明某个复现词」要另做独立的小 AI 调用,不和这次四部分生成挤在一起、
--      也不在库变大后拿一堆前置内容挤预算 / 分散注意力。handler 相应不再取 PriorVocab。
--   9. 补入 source_title(=questions.title,原文自带的英文标题)—— v4 只给了正文,参考译文里
--      的标题译文因此是盲生成,句型 / 词汇材料也用不到标题。与 grading 模板同一 branch 写法
--      ({{ if source_title != null && source_title != "" }});为空则整段不渲染。handler 补传 SourceTitle。
--  10. 标题译文单独出 referenceTitle 字段(不再塞进 referenceText 首行,避免标题与正文混在一起)。
--      新增 DB 列 reference_translations.reference_title(迁移 AddReferenceTranslationTitle,nullable);
--      DeepLearningPayload / 两个 Result DTO / 前端 DeepLearningContent 都加了 referenceTitle。
--   保留 v4 的产出上限(句型 4–8 / 词汇 12–20,宁精勿滥)—— JSON 截断的既有修复,不回退到 E 的
--   「宁全勿缺」;保留 literalTranslatable 字段。
--
-- DeepLearningPayload 字段(除新增 referenceTitle 外不变):referenceTitle? / referenceText / comparisonNotes[] /
-- sentencePatterns[{patternName,exampleSentence,breakdownSteps,variants,domain,scenario,
-- frequencyTag}] / vocabExpressions[{englishExpr,chineseEquiv,contextNote,category,domain,
-- scenario,frequencyTag,literalTranslatable}]。breakdownSteps 仍是 "object OR string"
-- (handler 存 GetRawText(),前端 BreakdownSteps 组件两种都能渲染)。
--
-- 设计文档 §10.2 隔离保证不变(现在更纯):本调用只拿 task_type + source_text,
-- 从不接触某一份 submission / grading_results / meaning_checkpoints。
--
-- template model 字段(GenerateDeepLearningContentCommandHandler 提供):
--   task_type    : "A" | "B"
--   source_title : 原文自带的英文标题(questions.title;可能为空,模板 branch)
--   source_text  : 本题原文全文
--
-- 手动执行(Supabase)。幂等:DELETE + INSERT,重复执行结果一致。
-- 必须保持在 deep_learning 相关脚本的最后一条。
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'deep_learning';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'deep_learning',
    'exam_specific',
    $tpl$
【定位】NAATI CT 英译中「深入学习」材料
- 面向备考 NAATI CT 英译中方向的学习者。这份材料只依据下面的【原文】生成,不针对任何一份具体译文,也不作为评分唯一依据。
- 参考译文须符合中文公文 / 通知 / 说明文的表达习惯:完整传达原文信息点(不漏译、不擅自增补),术语规范,语气与原文一致,断句与语序按中文习惯重组而非照搬英文;不使用破折号,插入说明改用括号或「逗号 + 同位语」。
- 句型与词汇积累优先覆盖澳大利亚政务 / 健康 / 移民 / 教育 / 消费者权益等场景的高频结构与规范译名(机构名、职位名、法案 / 表格 / 项目名称的通行译法)。
- 易被直译带偏的介词搭配 / 时间与范围结构 / 比较结构、习语与转喻表达,要重点标注并说明能否直译。

{{ if source_title != null && source_title != "" }}【原文标题】(原文自带的英文标题,不是用户添加的)
{{ source_title }}

{{ end }}【原文】
{{ source_text }}

任务类型:{{ task_type }}

请针对以上原文{{ if source_title != null && source_title != "" }}(含标题){{ end }},为学习者生成一份精炼、可直接用于复习积累的「深入学习」材料:

{{ if source_title != null && source_title != "" }}0. referenceTitle —— 上面【原文标题】的中文译文,单独输出;不要把标题并进 referenceText。原文无标题时填 null。
{{ end }}1. referenceText —— 一份高质量的标准参考译文(仅供学习对照,不作为评分唯一依据),只放正文译文、不含标题。达标线:完整传达全部信息点,不漏译、不增译;术语与专名用通行规范译法;符合中文公文 / 通知语域,断句与语序按中文习惯重组;不使用破折号。

2. comparisonNotes —— 针对本篇原文实际最容易译错的 3–6 处,每条一句话:错在哪、为什么、正确怎么处理。只写这篇原文里真实存在的难点,不要写「要注意语境」「结合上下文」这类放之四海皆准的空话。条目里提到英文原词时用「」括起,不要用英文双引号(见【输出格式】)。

3. sentencePatterns —— 挑 4–8 条对翻译最有借鉴价值的结构,不要只挑「最长的那句」,覆盖不同类型:
   - 多重修饰 / 定语从句叠加 / 同位语 / 插入语 / 并列宾语
   - 被动语态、名词化结构、there be、形式主语 it、强调句
   - 条件 / 让步 / 目的状语从句,非谓语动词作状语或后置定语
   - 公文 / 法律 / 医疗文体的框架句(如 "X will do Y from + 日期"、"Those who … should …")
   每条:
   - patternName:用简洁的中英混合命名点出结构骨架
   - exampleSentence:原文中的原句(不要改写)
   - breakdownSteps:一个对象,固定四个键 ——「主干」「修饰成分」「语序差异」「翻译要点」,把这句难在哪、中文该怎么落地讲透
   - variants:同结构的常见变体说法(字符串或 null)
   - domain / scenario / frequencyTag:见下

4. vocabExpressions —— 从原文里挑 12–20 条最值得积累的词汇与表达,优先高频、易误译、有迁移价值的;宁精勿滥,不要凑数。尽量覆盖多种类别(category 字段填下列中文标签之一):
   - 专业术语:某一领域(法律 / 医疗 / 政务 / 金融 / 教育 / 移民等)的行话、规范译名
   - 机构与专名:机构名、职位名、法案 / 项目 / 表格名称、缩略语及其全称
   - 固定搭配:动词 + 名词 / 形容词 + 名词等词典级搭配
   - 短语动词:phrasal verb(如 carry out、opt in)
   - 介词搭配:与特定介词绑定的用法(如 eligible for、subject to)
   - 习语与比喻:idiom、比喻性表达、谚语
   - 俚语与口语:非正式、口语化或地区性说法,以及它在正式译文里应如何处理
   - 常用短语 / 句式碎片:高频功能性表达(如 as soon as possible、in the event that)
   - 易混词 / 假朋友:形近或看似对应、实则译法不同的词(false friend)
   - 数量与范围表述:and over、up to、within、no later than 等端点 / 范围词
   每条:
   - englishExpr:原文中的英文表达(可含少量上下文,便于定位)
   - chineseEquiv:推荐中文译法,必要时给多个;或 null
   - contextNote:词典本义 / 引申义 / 本文语境义的区分,易错点,语域提示;或 null
   - category:上面列出的某个中文标签;或 null
   - domain:领域(如「法律」「医疗」「政府公告」);或 null
   - scenario:应用场景(如「公告通知」「信函往来」「口译对话」);或 null
   - frequencyTag:「高频」= 考试与实务中反复出现、值得长期记忆;「中频」= 特定领域内常见;「低频」= 本篇特有或较生僻;拿不准填 null
   - literalTranslatable:true = 可照字面直译;false = 习语 / 比喻 / 意思与字面差异大的短语(不可机械直译);拿不准填 null

原则:宁精勿滥——每一条句型和词汇都必须真实出现在原文中、或与原文内容直接相关,不得为了凑数而杜撰;某一类原文里确实没有,对应条目可以少或没有。

【输出格式】
严格只输出以下 JSON,不要输出 markdown 代码块围栏,不要输出 JSON 之外的任何文字。必须是能被标准解析器一次读通的合法 JSON,逐条遵守:
- 所有 key 和字符串值用英文双引号 " " 包裹;不得有尾随逗号;每个 [ { 都要正确闭合。
- 字符串**内部**绝对不要再出现未转义的英文双引号 "。要在中文里引用某个英文单词 / 短语 / 例句时,一律用中文引号「」或直接写出,例如:把「green IT」译作……。只有确实必须保留英文引号时才写成 \"。
- 字符串内不要出现真实换行;需要换行写 \n。
- referenceText 用一段连续文字,段落之间用 \n。
- 数值字段 literalTranslatable 只写 true / false / null(不加引号);其它"或 null"的字段,没有内容就写 null,不要写空字符串。
以下是结构模板:
{
  "referenceTitle": "<标题的中文译文;原文无标题时为 null>",
  "referenceText": "<正文参考译文,不含标题>",
  "comparisonNotes": ["<易错点1>", "<易错点2>"],
  "sentencePatterns": [
    {"patternName": "<句型名称>", "exampleSentence": "<原文中的例句>", "breakdownSteps": {"主干": "...", "修饰成分": "...", "语序差异": "...", "翻译要点": "..."}, "variants": "<常见变体或 null>", "domain": "<领域,如法律 / 医疗 / 政府公告,或 null>", "scenario": "<应用场景,或 null>", "frequencyTag": "<高频 / 中频 / 低频,或 null>"}
  ],
  "vocabExpressions": [
    {"englishExpr": "<英文表达>", "chineseEquiv": "<中文对应,或 null>", "contextNote": "<本义 / 引申义 / 语境义区分,或 null>", "category": "<上面列出的中文分类标签,或 null>", "domain": "<领域,或 null>", "scenario": "<应用场景,或 null>", "frequencyTag": "<高频 / 中频 / 低频,或 null>", "literalTranslatable": <true / false / null>}
  ]
}
$tpl$,
    1,
    TRUE
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates WHERE template_type = 'deep_learning';
-- -> 唯一一行:layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111',
--    subject_category=NULL, version=1, is_active=true
