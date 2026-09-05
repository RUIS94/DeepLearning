-- =====================================================================
-- 薄弱点两级分类体系 — 一级分类种子数据
-- 来源:薄弱点分类与生命周期管理_策划书.md §1.1
--
-- 依赖:先跑 EF 迁移 20260905083908_AddWeakPointCategoriesAndTrackingStatus
--       (建 weak_point_categories 表 + weak_point_catalog.category_id 列)。
--
-- 这 8 条是固定闭集,不随考试类型变化,也不在运行时新增——AI 归类找不到贴切
-- 叶子时只会新建 weak_point_catalog 的 proposed 行(挂到这 8 条之一或留空待审),
-- 不会新增一级分类。
--
-- 手动执行(Supabase SQL Editor 或 psql)。幂等:ON CONFLICT DO NOTHING。
-- =====================================================================

BEGIN;

INSERT INTO weak_point_categories (code, name, description, display_order)
VALUES
    ('semantic_errors', 'Semantic Errors / 语义错误',
     '词面之外的深层语义关系(主客体、否定、情态、因果、时间、空间、比较、范围、指代)被误传达。', 1),

    ('lexical_errors', 'Lexical Errors / 词汇错误',
     '选词、术语、实体名称、多义词辨析或固定搭配层面的用词错误。', 2),

    ('grammatical_syntactic_errors', 'Grammatical / Syntactic Errors / 语法句法错误',
     '时态体、语态、单复数、人称、语序或修饰关系等结构层面的错误。', 3),

    ('information_errors', 'Information Errors / 信息错误',
     '相对原文增删或扭曲了实际传达的信息量:漏译、增译、误译、语义偏移。', 4),

    ('discourse_logic_errors', 'Discourse / Logic Errors / 篇章逻辑错误',
     '跨句/跨段的连接关系、衔接手段、连贯性或前后一致性出现问题。', 5),

    ('pragmatic_stylistic_errors', 'Pragmatic / Stylistic Errors / 语用风格错误',
     '语体、语气、语用含义或文化本地化处理与场合/受众不符。', 6),

    ('fluency_naturalness', 'Fluency / Naturalness / 表达质量',
     '译文本身是否通顺自然,不涉及是否忠实原文。', 7),

    ('mechanics', 'Mechanics / 语言形式',
     '标点、格式、数字日期单位等书写规范层面的问题。', 8)
ON CONFLICT (code) DO NOTHING;

COMMIT;

-- 验证:SELECT code, name FROM weak_point_categories ORDER BY display_order;  -- 应为 8 行
