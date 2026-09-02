START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829092630_AddLlmProviderSettings') THEN
    CREATE TABLE llm_provider_settings (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        provider_key character varying(50) NOT NULL,
        is_active boolean NOT NULL DEFAULT FALSE,
        model character varying(100) NOT NULL,
        thinking_enabled boolean NOT NULL DEFAULT TRUE,
        effort character varying(20),
        extra_settings jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_llm_provider_settings PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829092630_AddLlmProviderSettings') THEN
    CREATE UNIQUE INDEX ix_llm_provider_settings_provider_key ON llm_provider_settings (provider_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829092630_AddLlmProviderSettings') THEN
    CREATE UNIQUE INDEX ux_llm_provider_settings_single_active ON llm_provider_settings (is_active) WHERE is_active = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260829092630_AddLlmProviderSettings') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260829092630_AddLlmProviderSettings', '10.0.11');
    END IF;
END $EF$;
COMMIT;

