-- =====================================================================
-- M8 — NAATI CT 英译中:规范薄弱点清单种子数据
-- 来源:NAATI_CT_陪练评卷提示词.md 第十二节「我个人需要重点追踪的薄弱点」
--
-- 依赖:先跑 EF 迁移 20260902041221_AddWeakPointCatalog(建 weak_point_catalog
--       表 + weak_points/standard_overrides 增列)。
--
-- 作用:让 UpdateWeakPointsOnGraded 把每处错误按 default_dimension_key
--       (+ 可选 default_error_category) 归到一条规范清单,而不是只按
--       "{维度名} - {错误类别名}" 粗分桶。匹配不到的错误仍回退旧字符串桶。
--
-- 已知限制(B5 再解决):纯规则映射无法区分同属 (meaning_transfer, distortion)
--       的「数字陷阱 / 逻辑关系还原 / 形近词混淆」——它们会命中列表中第一条匹配。
--       精确归类留待 weak_point_classification 那次独立 AI 调用(B5)。
--
-- 手动执行(Supabase SQL Editor 或 psql)。幂等:ON CONFLICT DO NOTHING。
-- exam_type_id 沿用 seed_naati_ct_en_zh.sql 的固定字面量 UUID。
-- =====================================================================

BEGIN;

INSERT INTO weak_point_catalog
    (exam_type_id, code, name, description, default_dimension_key, default_error_category)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'omission_hedging',
     '省略保留性/让步语气词',
     '倾向把 some/roughly/likely/a few/up to/well before/closer to/more than 等不确定或程度限定表述译得过于肯定,或整体丢失。',
     'meaning_transfer', 'unjustified_omission'),

    ('11111111-1111-1111-1111-111111111111', 'passive_voice_unstable',
     '被动语态处理不稳',
     '要么漏掉被动标记导致主宾颠倒,要么反过来生硬直译"被要求/被建议"这类翻译腔。',
     'language_proficiency', 'grammar_syntax_error'),

    ('11111111-1111-1111-1111-111111111111', 'numeric_statistical_traps',
     '数字/统计类陷阱',
     '倍数换算(double≠翻两倍;tripled=三倍)、百分比修饰对象(幅度还是人群占比)、程度副词精确对应(several≠many,ease≠abolish)、近似值精确化(closer to→接近,不可处理为确定值)、时间点被误区间化、over+时间两种含义混淆。',
     'meaning_transfer', 'distortion'),

    ('11111111-1111-1111-1111-111111111111', 'logical_relation_distortion',
     '逻辑关系还原',
     '因果方向颠倒、修饰语错位、同位说明被误处理成因果关系、并列结构被合并、共享修饰语被误判、让步转折重心弄反;不要给原文单纯的并列关系(and/when/while)擅自加因果标记词。',
     'meaning_transfer', 'distortion'),

    ('11111111-1111-1111-1111-111111111111', 'terminology_level_confusion',
     '专业/职业名词层级混淆',
     'paralegal≠lawyer、bookkeeper≠accountant、linguistics department≠语言系;固定商业/学术术语被泛化(compliance issue≠承诺问题、the bottom line≠泛指收益)。',
     'textual_norms', NULL),

    ('11111111-1111-1111-1111-111111111111', 'completion_hard_errors',
     '完成度硬伤',
     '每篇容易出现至少一处孤立但严重的硬伤:未完成翻译痕迹、语法杂糅读不通、同音错字/重复字导致句子不通。提交前需专门自查。',
     'language_proficiency', NULL),

    ('11111111-1111-1111-1111-111111111111', 'terminology_inconsistency',
     '术语前后不一致',
     '同一英文词在同一篇译文中用了不同中文对应词(assessor 一会"审核员"一会"理算师"),或同一误译在全篇重复出现(massive 反复译为"大量")。须标注为"系统性重复错误"。',
     'textual_norms', NULL),

    ('11111111-1111-1111-1111-111111111111', 'hard_long_sentence_stall',
     '困难档长难句卡壳',
     '面对困难档长难句容易卡壳、不知从何下手,需先做结构拆解(主干/从句/同位语/插入语分层),而非直接尝试整句翻译。',
     'meaning_transfer', NULL),

    ('11111111-1111-1111-1111-111111111111', 'fabricated_specificity',
     '无中生有具体化',
     '把比较/程度结构具体化为捏造的比例数字,或将泛指表达替换为具体专有名词、历史事件。',
     'meaning_transfer', 'unjustified_addition'),

    ('11111111-1111-1111-1111-111111111111', 'lookalike_word_confusion',
     '形近/义近词混淆',
     '把形近或义近词混淆造成方向性误判(at times≠it''s time,enable≠help),需通过词典本义核实,不能凭语感联想。',
     'meaning_transfer', 'distortion')
ON CONFLICT (exam_type_id, code) DO NOTHING;

COMMIT;

-- 验证:
-- SELECT code, default_dimension_key, default_error_category
-- FROM weak_point_catalog
-- WHERE exam_type_id = '11111111-1111-1111-1111-111111111111'
-- ORDER BY code;   -- 应为 10 行
