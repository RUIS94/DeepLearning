-- =====================================================================
-- llm_provider_models 种子数据 —— 给每个provider补充几个已验证过、但目前不是
-- is_current的model,方便之后直接调用select接口切换,不用现敲字符串。
--
-- 运行顺序:add_llm_provider_models.sql -> migrate_llm_provider_settings_model_to_catalog.sql
-- -> 这个文件(可选) -> remove_model_column_from_llm_provider_settings.sql
--
-- 这张表现在是"当前生效model"的唯一数据来源 —— is_current=true的那一行就是
-- LlmClientResolver实际会用的model,不再有独立的llm_provider_settings.model
-- 字段(旧字段已经被上面migrate脚本搬过来,搬完就会被drop掉,不会有两份可能
-- 对不上的model字符串)。这个种子文件只追加is_current=false的新行,不会
-- 覆盖migrate脚本已经设置好的is_current=true状态(ON CONFLICT DO NOTHING)。
--
-- 下面只列了这个项目里已经真实调用验证过、或者model id本身确认过的:
--   claude-sonnet-5   — 当前Claude系列除了claude-opus-5之外的另一个可用型号
--                        (model id确认过,不是猜的;is_current=true的那行
--                        应该已经是claude-opus-5,来自migrate脚本)
--
-- 没有列 deepseek-v4-pro 之类的型号 —— 没有对着DeepSeek自己的文档核实过具体的
-- model id 字符串,不要瞎猜着塞进去,确认好了再用下面这个模板加:
--   INSERT INTO llm_provider_models (provider_key, model, label)
--   VALUES ('deepseek', 'deepseek-v4-pro', 'DeepSeek V4 Pro')
--   ON CONFLICT (provider_key, model) DO NOTHING;
-- 或者直接调用 POST /api/v1/llm-provider-settings/{providerKey}/models。
-- =====================================================================

BEGIN;

INSERT INTO llm_provider_models (provider_key, model, label)
VALUES
    ('claude', 'claude-sonnet-5', 'Claude Sonnet 5')
ON CONFLICT (provider_key, model) DO NOTHING;

COMMIT;

-- =====================================================================
-- 常用查询
-- =====================================================================

-- 某个provider目前记录了哪些model,哪个是is_current
-- SELECT model, label, is_current, created_at FROM llm_provider_models WHERE provider_key = 'claude' ORDER BY created_at;

-- 把Claude当前生效的model从opus-5切到sonnet-5(catalog里两行都还在,不会丢)
-- 等价于 POST /api/v1/llm-provider-settings/claude/models/claude-sonnet-5/select
-- UPDATE llm_provider_models SET is_current = false WHERE provider_key = 'claude' AND is_current = true;
-- UPDATE llm_provider_models SET is_current = true WHERE provider_key = 'claude' AND model = 'claude-sonnet-5';
