-- =====================================================================
-- Creates llm_provider_models — the catalog of known models per provider,
-- and (via is_current) the single source of truth for which model each
-- provider is currently using. Run this BEFORE
-- migrate_llm_provider_settings_model_to_catalog.sql and
-- remove_model_column_from_llm_provider_settings.sql — those two finish
-- the same EF migration (20260829110847_AddLlmProviderModels) by moving
-- the existing model values here and then dropping the old column.
-- Run order: this file -> migrate_llm_provider_settings_model_to_catalog.sql
-- -> (optionally) seed_llm_provider_models.sql -> remove_model_column_from_llm_provider_settings.sql
--
-- ALREADY RAN AN EARLIER VERSION OF THIS FILE? If llm_provider_models
-- already exists on your DB (an earlier version of this table had
-- thinking_enabled/effort/extra_settings columns and no is_current),
-- do NOT run this file again — it will fail with "relation already
-- exists". Run upgrade_llm_provider_models_schema.sql instead, then
-- continue with the rest of the order above.
-- =====================================================================

START TRANSACTION;

CREATE TABLE llm_provider_models (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    provider_key character varying(50) NOT NULL,
    model character varying(100) NOT NULL,
    label character varying(100),
    is_current boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_llm_provider_models PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_llm_provider_models_provider_key_model ON llm_provider_models (provider_key, model);

-- At most one is_current=true row PER provider_key (not one globally, unlike
-- llm_provider_settings.is_active — each provider tracks its own current model).
CREATE UNIQUE INDEX ux_llm_provider_models_single_current_per_provider ON llm_provider_models (provider_key) WHERE is_current = true;

COMMIT;
