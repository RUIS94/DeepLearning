-- =====================================================================
-- Finishes EF migration 20260829110847_AddLlmProviderModels: drops the now-
-- redundant llm_provider_settings.model column (its values already live in
-- llm_provider_models as is_current=true rows — see
-- migrate_llm_provider_settings_model_to_catalog.sql, which MUST be run
-- first, or this is a real, unrecoverable data loss).
--
-- Run order: (add_llm_provider_models.sql, or upgrade_llm_provider_models_schema.sql
-- if you'd already run the old version of that file) -> migrate_llm_provider_settings_model_to_catalog.sql
-- -> (optionally) seed_llm_provider_models.sql -> this file.
--
-- Also reconciles __EFMigrationsHistory if you already ran the OLD
-- add_llm_provider_models.sql: that file self-inserted a history row for
-- '20260829104952_AddLlmProviderModels', a migration id that no longer
-- exists in the codebase (it was regenerated as
-- '20260829110847_AddLlmProviderModels' once the design was fixed). This
-- deletes the stale row before inserting the current one, so
-- __EFMigrationsHistory matches what's actually in the Migrations folder.
-- Harmless to run even if that stale row was never there (DELETE of zero
-- rows is a no-op).
-- =====================================================================

START TRANSACTION;

DELETE FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829104952_AddLlmProviderModels';

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829110847_AddLlmProviderModels') THEN
    ALTER TABLE llm_provider_settings DROP COLUMN model;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829110847_AddLlmProviderModels') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260829110847_AddLlmProviderModels', '10.0.11');
    END IF;
END $EF$;

COMMIT;
