-- =====================================================================
-- feature_flags 初始行 — 设计文档 §六「功能开关」/ §11.2 Step 10
--
-- 2026-09-09 Phase 5:把题库 / 复习库两个功能挂到 feature_flags 上,可按环境灰度
-- 开关而不必重新部署后端。后端 [FeatureGate("...")] 过滤器读这张表;某个 key 没有行
-- 时按 Application/Common/FeatureFlags.Defaults 兜底(两者都默认 true),所以在这张
-- 表被填之前,行为与接入前完全一致。
--
-- 手动执行(Supabase SQL Editor / psql / `dotnet run -- sql apply`)。
-- 幂等:ON CONFLICT (key) DO NOTHING —— 不覆盖运维后来手动改过的 enabled 值。
-- =====================================================================
BEGIN;

INSERT INTO feature_flags (key, enabled, scope, updated_at) VALUES
    ('question_bank_enabled', TRUE, 'global', now()),
    ('review_library_enabled', TRUE, 'global', now())
ON CONFLICT (key) DO NOTHING;

COMMIT;
