-- =====================================================================
-- llm_provider_settings 种子数据 —— 4个已接入的provider各一行
-- 运行前提:先跑完 add_llm_provider_settings.sql 建好表。
--
-- 只有 claude 一行 is_active = true(部分唯一索引
-- ux_llm_provider_settings_single_active 保证至多一行is_active=true)。
-- 之后要切换provider,直接：
--   UPDATE llm_provider_settings SET is_active = false WHERE provider_key = 'claude';
--   UPDATE llm_provider_settings SET is_active = true  WHERE provider_key = 'openai';
-- 下一次AI调用立即生效,不需要重新部署。
--
-- effort/thinking_enabled目前只有Claude的adapter真正读取并发送给API
-- (见ClaudeLlmClient.cs);其余provider的"思考模式"机制尚未逐一核实,
-- 暂时留空,真要控制就通过extra_settings按各自文档自行传参
-- (例如OpenAI o系列的{"reasoning_effort":"high"})。
-- =====================================================================

BEGIN;

INSERT INTO llm_provider_settings (provider_key, is_active, model, thinking_enabled, effort, extra_settings)
VALUES
    ('claude',   true,  'claude-opus-5',      true, NULL, NULL),
    ('openai',   false, 'gpt-4o-mini',        true, NULL, NULL),
    ('deepseek', false, 'deepseek-v4-flash',  true, NULL, NULL),
    ('mimo',     false, 'mimo-v2.5-pro',      true, NULL, NULL)
ON CONFLICT (provider_key) DO NOTHING;

COMMIT;

-- =====================================================================
-- 常用查询
-- =====================================================================

-- 当前生效的provider(ILlmClientResolver在每次AI调用时都会查这一行)
-- SELECT * FROM llm_provider_settings WHERE is_active = true;

-- 全部provider当前配置一览
-- SELECT provider_key, is_active, model, thinking_enabled, effort, extra_settings FROM llm_provider_settings ORDER BY provider_key;

-- 把Claude的effort调到xhigh(仅示例,按需修改)
-- UPDATE llm_provider_settings SET effort = 'xhigh', updated_at = now() WHERE provider_key = 'claude';

-- 给某个OpenAI兼容provider塞一个provider专属参数(通用扩展口子)
-- UPDATE llm_provider_settings SET extra_settings = '{"reasoning_effort":"high"}'::jsonb, updated_at = now() WHERE provider_key = 'openai';
