-- =====================================================================
-- B5 (M13) — NAATI CT 背景补充
--
-- 现状:question_gen 的 exam_specific 行只有一句「NAATI CT ... 出题员 + 三档难度」,
-- deep_learning 完全没有 exam_specific 行(只有通用 shared_methodology)。这里各加
-- 一条 exam_specific 行,补上考试结构 / 文本定位 / 题源风格等背景,让出题和深入
-- 学习都更贴近真实 NAATI CT。
--
-- 加法:IExamConfigLoader 会把每条 active 行按 shared→specific 顺序拼接,不动现有行。
-- 手动执行(Supabase)。幂等:各自 NOT EXISTS 守卫。
-- =====================================================================

BEGIN;

-- ---------- exam_specific / question_gen:出题背景补充 ----------
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'question_gen',
    'exam_specific',
    $tpl$
【NAATI CT 背景补充】
- 整场考试 = 2 篇 Task A(英译中翻译)+ 1 篇 Task B(译文审校),三项全过才算通过。
- 官方对文本难度的唯一书面定位是 "complex but non-specialised"(复杂但非专业)。
- 题源风格:澳大利亚政府 / 议会 / 地方机构的公告与通知,社区服务、健康、教育、移民、消费者权益、环境等领域面向公众的说明性文章,以及非专业新闻报道。避免文学作品、诗歌、高度专业的法律条文或学术论文。
- 语言特征:以陈述句、信息线性推进为主,可含机构名、职位名、法案 / 表格 / 项目名称、日期与数字、直接引语及说话人身份。
- 生成的原文应当是「一名受过教育的普通读者能读懂、但翻译时需要处理若干长难句与规范术语」的水平。
$tpl$,
    1,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'question_gen'
      AND layer = 'exam_specific'
      AND template_content LIKE '%【NAATI CT 背景补充】%'
);

-- ---------- exam_specific / deep_learning:深入学习定位 ----------
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
SELECT
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'deep_learning',
    'exam_specific',
    $tpl$
【NAATI CT 深入学习定位】
- 学习者备考英译中方向。参考译文须符合中文公文 / 通知 / 说明文的表达习惯:简洁、少欧化、术语规范、语气与原文一致;不使用破折号,插入说明改用括号或「逗号 + 同位语」。
- 句型与词汇积累优先覆盖澳大利亚政务 / 健康 / 移民 / 教育 / 消费者权益等场景的高频结构与规范译名(如机构名、职位名、法案 / 表格名称的通行译法)。
- 易被直译错的介词 / 时间结构 / 比较结构、习语与转喻表达,要重点标注并说明是否可直译。
$tpl$,
    1,
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM prompt_templates
    WHERE template_type = 'deep_learning'
      AND layer = 'exam_specific'
      AND template_content LIKE '%【NAATI CT 深入学习定位】%'
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, version, is_active, left(template_content, 22)
-- FROM prompt_templates
-- WHERE template_type IN ('question_gen','deep_learning') AND layer = 'exam_specific'
-- ORDER BY template_type, version;
