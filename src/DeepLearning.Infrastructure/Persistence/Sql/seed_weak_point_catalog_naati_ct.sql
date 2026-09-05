-- =====================================================================
-- SUPERSEDED — kept as a historical record only, now a no-op.
--
-- This script originally seeded 10 NAATI-CT-specific weak_point_catalog rows
-- keyed by exam_type_id. 薄弱点分类与生命周期管理_策划书.md §1 replaced that
-- exam-scoped model with a global two-level taxonomy: exam_type_id no longer
-- exists on weak_point_catalog (dropped by EF migration
-- 20260905083908_AddWeakPointCategoriesAndTrackingStatus), so this script's
-- original INSERT would fail on a fresh install applying every script in
-- manifest order. See seed_weak_point_categories.sql,
-- seed_weak_point_catalog_v2_taxonomy.sql and
-- remove_legacy_naati_weak_point_catalog.sql for the replacement.
--
-- On the shared database this script is already recorded as applied via
-- `sql baseline` and is never re-run, so leaving it a no-op here does not
-- lose anything there either.
-- =====================================================================

SELECT 1;
