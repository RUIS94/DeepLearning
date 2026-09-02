-- =====================================================================
-- Data migration, hand-written (not from `dotnet ef migrations script`):
-- copies each provider's existing llm_provider_settings.model value into
-- llm_provider_models as that provider's is_current=true row, so nothing
-- is lost before remove_model_column_from_llm_provider_settings.sql drops
-- the old column. Safe to re-run (ON CONFLICT DO UPDATE keeps is_current
-- true rather than erroring).
--
-- Run this AFTER add_llm_provider_models.sql and BEFORE
-- remove_model_column_from_llm_provider_settings.sql.
-- =====================================================================

BEGIN;

INSERT INTO llm_provider_models (provider_key, model, is_current)
SELECT provider_key, model, true
FROM llm_provider_settings
ON CONFLICT (provider_key, model) DO UPDATE SET is_current = true;

COMMIT;

-- Sanity check before proceeding to the DROP COLUMN step — every provider_key
-- in llm_provider_settings should now have exactly one is_current=true row here:
-- SELECT s.provider_key, m.model AS current_model
-- FROM llm_provider_settings s
-- LEFT JOIN llm_provider_models m ON m.provider_key = s.provider_key AND m.is_current
-- ORDER BY s.provider_key;
