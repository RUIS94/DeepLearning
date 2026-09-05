-- =====================================================================
-- llm_provider_settings / llm_provider_models 的最终状态种子 —— 为「全新数据库」补齐。
--
-- 为什么需要这个文件（而不是直接改老脚本）：
--   线上库是按历史顺序长出来的：add_llm_provider_settings.sql 建表(带 model 列)
--   -> seed_llm_provider_settings.sql 灌 4 行(含 model)
--   -> add_llm_provider_models.sql 建 catalog 表
--   -> migrate_llm_provider_settings_model_to_catalog.sql 把 model 搬过去并置
--      is_current=true -> remove_model_column_from_llm_provider_settings.sql 删列。
--   而一个全新的库是由 EF 迁移一次性建成【最终形态】的：llm_provider_settings 从一开始
--   就没有 model 列，llm_provider_models 从一开始就是最终结构。于是上面那条链里有 4 个
--   脚本在新库上必然失败或无意义（见 _bootstrap_skip.txt），结果就是两张表全空 ——
--   LlmClientResolver 只能靠 FallbackProviderKey 兜底，且没有任何 is_current 的 model。
--
--   老脚本已经在线上库里 baseline 过了，改它们既救不了线上（不会重跑），又会让 git 历史
--   和实际执行过的东西对不上。所以按本目录的既定纪律：追加一个新脚本，描述【期望的最终
--   状态】，且对已经处于该状态的库完全是 no-op。
--
-- 幂等性 / 线上安全性：
--   * settings 靠 provider_key 唯一键 ON CONFLICT DO NOTHING。
--   * models 靠 (provider_key, model) 唯一键 ON CONFLICT DO NOTHING，且【一律插
--     is_current = false】。直接插 is_current=true 会踩 ux_llm_provider_models_single_current_per_provider
--     这个部分唯一索引 —— 如果线上早就把某个 provider 切到了别的 model，插一行新的
--     is_current=true 不会命中 (provider_key, model) 冲突目标，而是会直接违反部分索引
--     报错，把线上的 `sql apply` 卡死。
--   * is_current 只在「该 provider 目前一行 current 都没有」时才补一个默认值，
--     所以线上任何已经生效的选择都不会被改动。
--
-- 默认 model 与 appsettings.Development.template.json 的 Llm:* 配置保持一致；
-- 换 model 走 POST /api/v1/llm-provider-settings/{providerKey}/models/{model}/select，
-- 不要改这个文件。
-- =====================================================================

BEGIN;

INSERT INTO llm_provider_settings (provider_key, is_active, thinking_enabled, effort, extra_settings)
VALUES
    ('mimo',     true,  true, NULL, NULL),
    ('claude',   false, true, NULL, NULL),
    ('openai',   false, true, NULL, NULL),
    ('deepseek', false, true, NULL, NULL)
ON CONFLICT (provider_key) DO NOTHING;

INSERT INTO llm_provider_models (provider_key, model, label, is_current)
VALUES
    ('mimo',     'mimo-v2.5-pro',     'MiMo V2.5 Pro',     false),
    ('claude',   'claude-opus-5',     'Claude Opus 5',     false),
    ('claude',   'claude-sonnet-5',   'Claude Sonnet 5',   false),
    ('openai',   'gpt-4o-mini',       'GPT-4o mini',       false),
    ('deepseek', 'deepseek-v4-flash', 'DeepSeek V4 Flash', false)
ON CONFLICT (provider_key, model) DO NOTHING;

-- 只给「完全没有 current 行」的 provider 补一个默认，绝不改动已有选择。
UPDATE llm_provider_models m
SET is_current = true
WHERE (m.provider_key, m.model) IN (
        ('mimo',     'mimo-v2.5-pro'),
        ('claude',   'claude-opus-5'),
        ('openai',   'gpt-4o-mini'),
        ('deepseek', 'deepseek-v4-flash')
      )
  AND NOT EXISTS (
        SELECT 1 FROM llm_provider_models other
        WHERE other.provider_key = m.provider_key AND other.is_current
      );

COMMIT;

-- =====================================================================
-- 核对：每个 provider 应当恰好一行 is_current
-- SELECT provider_key, count(*) FILTER (WHERE is_current) AS current_rows
-- FROM llm_provider_models GROUP BY provider_key ORDER BY provider_key;
-- =====================================================================
