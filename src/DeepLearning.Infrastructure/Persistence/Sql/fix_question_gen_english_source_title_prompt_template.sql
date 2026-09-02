-- =====================================================================
-- B1.1 热修 — question_gen 模板:强制英文原文标题 + brief 不写难度
--
-- 现象(端到端测试发现,非 B1 引入):
--   1. AI 出题返回中文 title(英译中任务的原文是英文,标题也应为英文)
--   2. brief 里混入了「难度档位」,答题页把它当作 label:value 渲染,
--      于是标题下方多出一行难度 —— 而难度本身已有 DifficultyBadge。
--
-- 影响:B1 让评判 prompt 消费 source_title(=questions.title),标称「原文自带、
--   非用户添加」;title 若是中文,这段说明就自相矛盾。故一并修掉。
--
-- 做法:沿用「新版本、停旧版」约定,按内容标记 + is_active 命中当前活跃行:
--   - question_gen / shared_methodology(标记【输出格式 — 严格遵守】):改 title 字段说明,
--     追加「title 与 sourceText 均须英文;brief 不写难度」
--   - question_gen / exam_specific (标记「你是NAATI CT ... 出题员」):删掉
--     「在Translation Brief中明确标注本次难度档位。」,换成标题/语言约束
--
-- 手动执行(Supabase)。幂等:靠 version=2 + 新标记的 NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

-- ---------- 1) shared_methodology / question_gen 输出格式行 ----------
UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'question_gen'
  AND layer = 'shared_methodology'
  AND is_active = TRUE
  AND template_content LIKE '%【输出格式 — 严格遵守】%';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    NULL,
    'translation',
    'question_gen',
    'shared_methodology',
    $tpl$【输出格式 — 严格遵守】
你的回复必须是且只能是一个JSON对象，不要有任何markdown代码块标记（不要```json），不要有任何JSON之外的说明文字。JSON结构必须精确符合：
{
  "title": "英文原文文章自带的标题：必须是英文，逐字取自真实原文，禁止翻译成中文、禁止自行编造中文标题；原文确实无标题时填空字符串",
  "sourceText": "约250词的英文原文正文，字符串；不要把标题重复拼进正文",
  "brief": {"domain": "领域，如法律/医疗/政府公告", "textType": "文本类型", "purpose": "翻译目的", "audience": "目标受众"},
  "wordCount": 原文实际词数，整数,
  "meaningCheckpoints": [
    {"checkpointText": "原文中必须被准确传达的一个具体信息点", "checkpointType": "如数值/让步语气词/因果方向/立场语气/专业术语层级，可为null", "importance": "core或peripheral之一"}
  ]
}
title 与 sourceText 都是待用户翻译的英文原文材料，两者都必须是英文。brief 只描述领域/文本类型/目的/受众四项，不要写入难度档位（难度由系统单独记录）。
本次难度档位：{{ difficulty }}。meaningCheckpoints至少包含3条，覆盖原文中最关键、最容易被译错的信息点。$tpl$,
    2,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'question_gen'
      AND layer = 'shared_methodology'
      AND version = 2
      AND template_content LIKE '%title 与 sourceText 都是待用户翻译的英文原文材料%'
);

-- ---------- 2) exam_specific / question_gen 出题员行 ----------
UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'question_gen'
  AND layer = 'exam_specific'
  AND is_active = TRUE
  AND template_content LIKE '%你是NAATI CT(Certified Translator,英译中方向)考试的出题员%';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'question_gen',
    'exam_specific',
    $tpl$
你是NAATI CT(Certified Translator,英译中方向)考试的出题员。

【任务模式】
- 出一篇英译中翻译任务(约250词),含Translation Brief(领域/文本类型/目的/受众)和标题
- 尽可能使用真实存在的文章,不要凭空捏造
- 出题应符合NAATI出题风格,多偏向澳大利亚各类场景
- 专业词汇要准确但不刁钻;论证以线性事实/数据推进为主;可适当包含机构名、期刊名、人名

【难度分层系统】(官方定位为"complex but non-specialised")
- 简单档:长难句1-2处,多为单层分词状语或简单定语从句;论证结构线性;术语密度低
- 中等档:长难句2-3处,允许1-2层嵌套;可能含直接引语及说话人身份的复杂修饰;含1-3个需精确处理的专业/政策术语;论证可能含一次转折/对比
- 困难档:长难句3-4处,允许多层嵌套(3层以上);抽象名词堆叠句式;术语密度较高;篇章结构更依赖上下文推理;主句成分可能被大幅后置

本次出题难度档位:{{ difficulty }}

标题(title)与正文(sourceText)都是待用户翻译的英文原文材料:
- title 必须是英文,逐字取自真实原文文章自带的标题;禁止翻译成中文,禁止自行编造中文标题;原文无标题则留空字符串。
- 不要把标题重复写进 sourceText。
- Translation Brief 只写领域/文本类型/目的/受众;难度档位由系统单独记录,不要写进 brief。
$tpl$,
    2,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'question_gen'
      AND layer = 'exam_specific'
      AND version = 2
      AND template_content LIKE '%title 必须是英文,逐字取自真实原文文章自带的标题%'
);

COMMIT;

-- 验证:
-- SELECT layer, version, is_active, left(template_content, 26)
-- FROM prompt_templates WHERE template_type = 'question_gen' ORDER BY layer, version;
-- -> shared_methodology 输出格式行 + exam_specific 出题员行:各 version 1(false) + 2(true)
