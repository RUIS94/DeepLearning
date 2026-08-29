using DeepLearning.Application.Interfaces;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Ai.Options;
using DeepLearning.Infrastructure.Common;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

            services.AddDbContext<AppDbContext>(options => options
                .UseNpgsql(connectionString, NpgsqlEnumConfiguration.MapEnums)
                .UseSnakeCaseNamingConvention());

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<IExamTypeRepository, ExamTypeRepository>();
            services.AddScoped<IAssessmentDimensionRepository, AssessmentDimensionRepository>();
            services.AddScoped<IErrorTaxonomyRepository, ErrorTaxonomyRepository>();
            services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IQuestionRepository, QuestionRepository>();
            services.AddScoped<IAiCallLogRepository, AiCallLogRepository>();
            services.AddScoped<ILlmProviderSettingsRepository, LlmProviderSettingsRepository>();

            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

            // --- AI / LLM: provider-neutral ILlmClient resolved via keyed DI --------------
            // Adding a new provider later = one more AddKeyedScoped<ILlmClient, XxxLlmClient>
            // line + its adapter class. No handler ever depends on a concrete provider.
            services.AddOptions<ClaudeApiOptions>().Bind(configuration.GetSection(ClaudeApiOptions.SectionName));

            services.AddHttpClient<ClaudeLlmClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClaudeApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", options.ApiVersion);
                if (!string.IsNullOrEmpty(options.WorkspaceId))
                {
                    client.DefaultRequestHeaders.Add("anthropic-workspace-id", options.WorkspaceId);
                }
            }).AddStandardResilienceHandler(LlmResiliencePipeline.Configure);

            services.AddKeyedTransient<ILlmClient>("claude", (sp, _) => sp.GetRequiredService<ClaudeLlmClient>());

            // OpenAI-compatible providers (OpenAI, DeepSeek, Mimo) share one adapter class —
            // only the config differs per provider (named options + a keyed registration each).
            // One shared named HttpClient carries the same resilience policy as Claude's.
            services.AddHttpClient("llm-openai-compatible").AddStandardResilienceHandler(LlmResiliencePipeline.Configure);

            services.AddOptions<OpenAiCompatibleOptions>("openai").Bind(configuration.GetSection("Llm:OpenAi"));
            services.AddOptions<OpenAiCompatibleOptions>("deepseek").Bind(configuration.GetSection("Llm:DeepSeek"));
            services.AddOptions<OpenAiCompatibleOptions>("mimo").Bind(configuration.GetSection("Llm:Mimo"));

            services.AddKeyedTransient<ILlmClient>("openai", (sp, _) => new OpenAiCompatibleLlmClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("llm-openai-compatible"),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenAiCompatibleOptions>>().Get("openai"),
                "OpenAI"));
            services.AddKeyedTransient<ILlmClient>("deepseek", (sp, _) => new OpenAiCompatibleLlmClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("llm-openai-compatible"),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenAiCompatibleOptions>>().Get("deepseek"),
                "DeepSeek"));
            services.AddKeyedTransient<ILlmClient>("mimo", (sp, _) => new OpenAiCompatibleLlmClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("llm-openai-compatible"),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenAiCompatibleOptions>>().Get("mimo"),
                "Mimo"));

            // Which provider is active lives in the database (llm_provider_settings.is_active),
            // not config — switching providers/models is a data change, not a redeploy.
            // Callers ask ILlmClientResolver for the active client instead of injecting
            // ILlmClient directly.
            services.AddScoped<ILlmClientResolver, LlmClientResolver>();

            services.AddSingleton<PromptRenderer>();
            services.AddScoped<IExamConfigLoader, ExamConfigLoader>();

            return services;
        }
    }
}
