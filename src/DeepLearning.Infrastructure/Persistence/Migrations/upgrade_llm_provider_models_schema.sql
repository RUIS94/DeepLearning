-- =====================================================================
-- Use this INSTEAD OF add_llm_provider_models.sql if you already ran the
-- FIRST version of that file (the one with thinking_enabled/effort/
-- extra_settings columns and no is_current — before the design was fixed
-- to make llm_provider_models the single source of truth for "which model
-- is currently in effect," see AGENTS.md's "AI integration" section).
--
-- Brings an already-created llm_provider_models table from that old shape
-- to the current one. Safe: thinking_enabled/effort/extra_settings are all
-- NULL on every existing row today (verified against the real DB before
-- writing this), so dropping them loses nothing.
--
-- Run order for someone in this situation:
--   this file -> migrate_llm_provider_settings_model_to_catalog.sql
--   -> (optionally) seed_llm_provider_models.sql
--   -> remove_model_column_from_llm_provider_settings.sql
-- (Same as the normal order, just with this file replacing
-- add_llm_provider_models.sql as step 1 — migrate_llm_provider_settings_model_to_catalog.sql
-- already handles "rows for these models already exist" correctly via
-- ON CONFLICT ... DO UPDATE, no changes needed there.)
-- =====================================================================

BEGIN;

ALTER TABLE llm_provider_models
    ADD COLUMN is_current boolean NOT NULL DEFAULT false;

ALTER TABLE llm_provider_models
    DROP COLUMN thinking_enabled,
    DROP COLUMN effort,
    DROP COLUMN extra_settings;

CREATE UNIQUE INDEX ux_llm_provider_models_single_current_per_provider
    ON llm_provider_models (provider_key) WHERE is_current = true;

COMMIT;
