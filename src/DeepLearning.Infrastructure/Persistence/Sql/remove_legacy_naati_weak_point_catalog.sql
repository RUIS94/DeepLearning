-- =====================================================================
-- 移除旧版 NAATI CT 专属薄弱点 code(seed_weak_point_catalog_naati_ct.sql 的 10 条)。
-- 来源:薄弱点分类与生命周期管理_策划书.md §1.3
--
-- weak_points / weak_point_occurrences 当前均无数据,不存在需要重新指向的历史
-- 记录,因此直接删除旧 10 条种子行,不做"改 catalog_id + 标记 deprecated"的
-- 数据迁移(那套逻辑是为已有历史数据准备的,当前用不上)。
-- 新体系见 seed_weak_point_categories.sql + seed_weak_point_catalog_v2_taxonomy.sql。
--
-- 手动执行(Supabase SQL Editor 或 psql)。幂等:DELETE 对已删除的行是 no-op。
-- =====================================================================

BEGIN;

DELETE FROM weak_point_catalog
WHERE code IN (
    'omission_hedging', 'passive_voice_unstable', 'numeric_statistical_traps',
    'logical_relation_distortion', 'terminology_level_confusion', 'completion_hard_errors',
    'terminology_inconsistency', 'hard_long_sentence_stall', 'fabricated_specificity',
    'lookalike_word_confusion'
);

COMMIT;

-- 验证:SELECT count(*) FROM weak_point_catalog WHERE origin = 'seed' AND category_id IS NULL;  -- 应为 0
