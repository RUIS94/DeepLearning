-- =====================================================================
-- Reset ALL question_gen prompt_templates rows down to ONE exam_specific row.
--
-- WHY ONE ROW (2026-09-02, user-requested — append-only convention waived
-- for this file only):
--   ExamConfigLoader.BuildPromptAsync concatenates Q1 (shared_methodology,
--   filtered by subject_category) then Q2 (exam_specific, filtered by
--   exam_type_id), each ordered by `version DESC` with NO other sort key.
--   Any query that returns >=2 rows has an undefined, drift-prone order.
--   The only way to guarantee a fixed prompt section order is to have each
--   query return at most one row. This project has exactly one exam type
--   and every question_gen instruction is NAATI-CT-specific, so:
--     - Q1 (shared_methodology) -> intentionally empty
--     - Q2 (exam_specific)      -> this single row
--   The assembled prompt is then this template verbatim, top to bottom,
--   with section order controlled entirely inside the template text.
--
-- Replaces all 9 prior rows (7 active + 2 inactive). Content is a dedup'd
-- merge of what they contained, reordered into a standard prompt structure
-- (role -> task -> background -> difficulty -> domain -> formatting ->
-- conditionals -> output contract).
--
-- Handler-side changes this template pairs with (GenerateQuestionCommandHandler):
--   * {{ if weak_point_hint }} reconnected — handler already passed it, no
--     prior row referenced it.
--   * {{ domain_categories }} / {{ pinned_domain }} are NEW model fields: the
--     list of existing question_bank_categories(domain) names, so the AI's
--     brief.domain reuses one instead of inventing near-duplicates. A pinned
--     CategoryId sends its name as a hard directive and is the question's sole
--     category link (no second link derived from brief.domain).
--   * {{ if seed_samples.size > 0 }} is now strictly opt-in — the handler only
--     fills seed_samples from an explicit SeedQuestionIds; with none, the block
--     stays empty (it used to auto-select matching seeds every time).
--
-- On a fresh DB this file must run LAST (after seed_naati_ct_en_zh.sql and
-- every add_*/fix_* question_gen file); it deletes whatever they created.
-- Those incremental files are now stale for question_gen — kept only as
-- migration history.
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'question_gen';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'question_gen',
    'exam_specific',
    $tpl$
你是 NAATI CT(Certified Translator,英译中方向)考试的出题员。

【本次任务】
- 出一篇英译中翻译任务的英文原文材料,约 250 词,配套 Translation Brief(领域/文本类型/目的/受众)和标题。
- 按下方【难度分层】中本次指定的档位控制难度。
- 尽量取材于真实存在的文章,不要凭空捏造;题材、句式复杂度、篇幅向真实考试看齐。

【NAATI CT 背景】
- 整场考试 = 2 篇 Task A(英译中翻译)+ 1 篇 Task B(译文审校),三项全过才算通过。
- 官方对文本难度的唯一书面定位是 "complex but non-specialised"(复杂但非专业)。
- 题源风格:澳大利亚政府/议会/地方机构的公告与通知,社区服务、健康、教育、移民、消费者权益、环境等领域面向公众的说明性文章,以及非专业新闻报道。避免文学作品、诗歌、高度专业的法律条文或学术论文。
- 语言特征:以陈述句、信息线性推进为主,可含机构名、职位名、法案/表格/项目名称、日期与数字、直接引语及说话人身份。
- 专业词汇要准确但不刁钻;论证以线性事实/数据推进为主;可适当包含机构名、期刊名、人名。
- 成文水平:一名受过教育的普通读者能读懂,但翻译时需要处理若干长难句与规范术语。

【难度分层】(官方定位 "complex but non-specialised")
- 简单档:长难句 1-2 处,多为单层分词状语或简单定语从句;论证结构线性;术语密度低。
- 中等档:长难句 2-3 处,允许 1-2 层嵌套;可能含直接引语及说话人身份的复杂修饰;含 1-3 个需精确处理的专业/政策术语;论证可能含一次转折/对比。
- 困难档:长难句 3-4 处,允许多层嵌套(3 层以上);抽象名词堆叠句式;术语密度较高;篇章结构更依赖上下文推理;主句成分可能被大幅后置。
- 本次出题难度档位:{{ difficulty }}。请严格落在该档位。

{{ if pinned_domain }}
【领域(已指定)】本次领域固定为「{{ pinned_domain }}」。brief.domain 必须逐字填写「{{ pinned_domain }}」,不要改写、翻译或另拟。
{{ else if domain_categories.size > 0 }}
【领域】brief.domain 必须从下列已有领域中逐字选取一个最贴切的;只有确实都不契合时,才可另拟一个简短领域名(2-8 字):
{{ for d in domain_categories }}
- {{ d.name }}
{{ end }}
{{ if topic_hint }}本次可优先考虑「{{ topic_hint }}」。{{ end }}
{{ end }}

【原文排版】
- sourceText 必须分段:按自然语义层次分成 3-5 段,段落之间用一个空行分隔(即 JSON 字符串里的 \n\n)。
- 段内不加换行,一段是连续的一段文字;首段点题/给背景,中间段展开,末段收束。
- TaskB 的 flawedTranslationText 分段方式与原文一致。

{{ if weak_point_hint }}
【薄弱点关联】该学员近期在「{{ weak_point_hint }}」上被反复标记为薄弱点。若与本次领域/难度自然契合,可在原文或(TaskB 场景)植入的错误中适度体现相关语言特征或错误类型;但不要为强行覆盖而扭曲原文自然度,也不必每次都围绕它出题。
{{ end }}
{{ if seed_samples.size > 0 }}
【真题参考样本】以下是调用方指定的真题原文,仅供参考题材、语言难度、句式复杂度与篇幅"手感":
{{ for s in seed_samples }}
---
标题:{{ s.title }}
{{ s.source_text }}
{{ end }}
---
上述样本仅作风格参照,禁止直接复制、逐句改写或大段挪用——你生成的原文必须是全新的、与样本不同的具体内容。
{{ end }}
{{ if task_type == "B" }}
【Task B 专属:审校任务】本次不是普通翻译,而是审校任务,需在生成的原文基础上额外产出:
1. 一份完整的"含错译文"全文——对原文做正常翻译,但故意植入若干处错误。
2. 每处错误标注:在"含错译文全文"中的字符起止偏移量(positionStart/positionEnd,从 0 计,均为该含错译文字符串内的下标,不是原文位置)、错误类别、正确译法(correctReferenceText)。
3. 错误数量与难度匹配:简单档 3-4 处,中等档 5-6 处,困难档 7-8 处(参考,不强制)。
4. 错误类型覆盖多个不同类别,不要全部集中在一类。
5. positionStart/positionEnd 必须精确对应含错译文全文中该片段的实际字符位置,不允许估算,区间之间不得重叠。
错误类别只能取下列 category_key 之一:
{{ for cat in error_taxonomies }}
- {{ cat.category_key }}({{ cat.category_name }})
{{ end }}
{{ end }}

【输出格式 — 严格遵守】
你的回复必须是且只能是一个 JSON 对象:不要任何 markdown 代码块标记(不要 ```json),不要任何 JSON 之外的说明文字。结构必须精确为:
{
  "title": "英文标题;取自真实文章则逐字照抄其原标题,否则自拟一个不超过 12 个英文单词的简短标题;禁止中文;原文确无标题时填空字符串",
  "sourceText": "约 250 词的英文原文正文;不要把标题重复拼进正文",
  "brief": {
    "domain": "英文;按上方【领域】要求:已指定则逐字用指定值,否则从领域清单中逐字选取",
    "textType": "英文;从下列固定选项中选一个最贴切的,或给最接近的英文等价说法,不超过 6 个单词 —— government notice / public information leaflet / news report / factsheet / policy statement / community announcement / official correspondence / report extract",
    "purpose": "英文;一句话,不超过 12 个单词;只说翻译用途,不得透露原文的具体论点、结论或关键数据",
    "audience": "英文;不超过 8 个单词的简短说明"
  },
  "wordCount": 原文实际词数(整数),
  "meaningCheckpoints": [
    {"checkpointText": "中文;原文中必须被准确传达的一个具体信息点", "checkpointType": "中文;如 数值/让步语气词/因果方向/立场语气/专业术语层级,可为 null", "importance": "core 或 peripheral"}
  ]
}
- title 与 sourceText 都是待用户翻译的英文原文材料,两者都必须是英文。
- brief 四项(domain / textType / purpose / audience)一律用英文且都要简短;不写难度档位(难度由系统单独记录)。
- purpose / audience 只做泛泛定位,不得复述原文内容,以免提前暴露题目主旨。
- meaningCheckpoints 的 checkpointText 与 checkpointType 一律用中文;至少 3 条,覆盖原文中最关键、最易译错的信息点。
{{ if task_type == "B" }}
- TaskB 场景下,JSON 在上述字段基础上额外包含:
  "flawedTranslationText": "<含错译文全文>",
  "seededErrors": [
    {"positionStart": <int>, "positionEnd": <int>, "errorCategory": "<category_key>", "correctReferenceText": "<string>", "note": "<string 或 null>"}
  ]
{{ end }}
$tpl$,
    1,
    TRUE
);

-- ---------------------------------------------------------------------
-- Rename the 21 domain question_bank_categories from Chinese to their
-- standard NAATI English names, so brief.domain (now required to be
-- English and picked verbatim from the injected {{ domain_categories }}
-- list) stays consistent with the catalogue.
--
-- question_category_map links by category id, so renames do NOT touch any
-- existing question's tagging. The denormalised questions.brief_domain
-- text column on older rows keeps its Chinese value (display-only copy) —
-- not worth a data-fix pass.
-- ---------------------------------------------------------------------
UPDATE question_bank_categories SET name = 'Housing'                  WHERE category_type = 'domain' AND name = '住房';
UPDATE question_bank_categories SET name = 'Insurance'                WHERE category_type = 'domain' AND name = '保险';
UPDATE question_bank_categories SET name = 'Health'                   WHERE category_type = 'domain' AND name = '健康';
UPDATE question_bank_categories SET name = 'Business'                 WHERE category_type = 'domain' AND name = '商业';
UPDATE question_bank_categories SET name = 'Employment'               WHERE category_type = 'domain' AND name = '就业';
UPDATE question_bank_categories SET name = 'Government'               WHERE category_type = 'domain' AND name = '政府';
UPDATE question_bank_categories SET name = 'Education'                WHERE category_type = 'domain' AND name = '教育';
UPDATE question_bank_categories SET name = 'Culture'                  WHERE category_type = 'domain' AND name = '文化';
UPDATE question_bank_categories SET name = 'Tourism'                  WHERE category_type = 'domain' AND name = '旅游';
UPDATE question_bank_categories SET name = 'Services & Manufacturing' WHERE category_type = 'domain' AND name = '服务/制造业';
UPDATE question_bank_categories SET name = 'Law'                      WHERE category_type = 'domain' AND name = '法律';
UPDATE question_bank_categories SET name = 'Consumer Affairs'        WHERE category_type = 'domain' AND name = '消费者事务';
UPDATE question_bank_categories SET name = 'Environment'              WHERE category_type = 'domain' AND name = '环境';
UPDATE question_bank_categories SET name = 'Society'                  WHERE category_type = 'domain' AND name = '社会';
UPDATE question_bank_categories SET name = 'Social Services'          WHERE category_type = 'domain' AND name = '社会服务';
UPDATE question_bank_categories SET name = 'Community'                WHERE category_type = 'domain' AND name = '社区';
UPDATE question_bank_categories SET name = 'Science'                  WHERE category_type = 'domain' AND name = '科学';
UPDATE question_bank_categories SET name = 'Technology'               WHERE category_type = 'domain' AND name = '科技';
UPDATE question_bank_categories SET name = 'Immigration & Settlement' WHERE category_type = 'domain' AND name = '移民与定居';
UPDATE question_bank_categories SET name = 'Economy'                  WHERE category_type = 'domain' AND name = '经济';
UPDATE question_bank_categories SET name = 'Finance'                  WHERE category_type = 'domain' AND name = '金融';

COMMIT;

-- Verify: exactly 1 question_gen template row, and every domain category now ASCII.
--   SELECT count(*), min(layer), min(version) FROM prompt_templates WHERE template_type = 'question_gen';
--   SELECT name FROM question_bank_categories WHERE category_type = 'domain' AND name ~ '[^\x00-\x7F]';  -- expect 0 rows
