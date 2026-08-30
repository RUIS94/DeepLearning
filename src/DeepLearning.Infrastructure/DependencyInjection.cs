using DeepLearning.Application.Interfaces;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Ai.GradingResultInterpreters;
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
            services.AddScoped<ILlmProviderModelRepository, LlmProviderModelRepository>();
            services.AddScoped<ISubmissionRepository, SubmissionRepository>();
            services.AddScoped<IGenerationPolicyRepository, GenerationPolicyRepository>();
            services.AddScoped<IFollowUpQuestionRepository, FollowUpQuestionRepository>();
            services.AddScoped<IStandardOverrideRepository, StandardOverrideRepository>();
            services.AddScoped<IWeakPointRepository, WeakPointRepository>();
            services.AddScoped<IProgressRepository, ProgressRepository>();
            services.AddScoped<IReviewLibraryRepository, ReviewLibraryRepository>();
            services.AddScoped<IReferenceTranslationRepository, ReferenceTranslationRepository>();
            services.AddScoped<IQuestionBankCategoryRepository, QuestionBankCategoryRepository>();
            services.AddScoped<ISeedReferenceLinkRepository, SeedReferenceLinkRepository>();

            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

            // One IGradingResultInterpreter per assessment_dimensions.scale_type — GradeSubmissionCommandHandler
            // picks the matching one via DI's IEnumerable<IGradingResultInterpreter>.
            services.AddScoped<IGradingResultInterpreter, Band15Interpreter>();
            services.AddScoped<IGradingResultInterpreter, Score100Interpreter>();
            services.AddScoped<IGradingResultInterpreter, RubricLevelInterpreter>();

            // --- AI / LLM: provider-neutral ILlmClient resolved via keyed DI --------------
            // Adding a new provider later = one more AddKeyedScoped<ILlmClient, XxxLlmClient>
            // line + its adapter class. No handler ever depends on a concrete provider.
            services.AddOptions<ClaudeApiOptions>().Bind(configuration.GetSection(ClaudeApiOptions.SectionName));

            // Development-only request/response tracing (see AiTracingHandler's own doc comment)
            // — must be Transient, matching HttpClientFactory's requirement for message handlers.
            // Added AFTER AddStandardResilienceHandler below so it's the INNER handler and sees
            // each Polly retry as its own logged attempt, not just the final outcome.
            services.AddTransient<AiTracingHandler>();

            // AddStandardResilienceHandler returns IHttpStandardResiliencePipelineBuilder (for
            // configuring ITS OWN internal Polly pipeline), not IHttpClientBuilder, so it can't
            // be chained into AddHttpMessageHandler directly — capture the original
            // IHttpClientBuilder and call both extension methods on it instead. Both still append
            // to the same underlying handler list in call order, so AiTracingHandler still ends
            // up as the inner (closer to network) handler, same effect as chaining.
            var claudeHttpClientBuilder = services.AddHttpClient<ClaudeLlmClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClaudeApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", options.ApiVersion);
                if (!string.IsNullOrEmpty(options.WorkspaceId))
                {
                    client.DefaultRequestHeaders.Add("anthropic-workspace-id", options.WorkspaceId);
                }
            });
            claudeHttpClientBuilder.AddStandardResilienceHandler(LlmResiliencePipeline.Configure);
            claudeHttpClientBuilder.AddHttpMessageHandler<AiTracingHandler>();

            services.AddKeyedTransient<ILlmClient>("claude", (sp, _) => sp.GetRequiredService<ClaudeLlmClient>());

            // OpenAI-compatible providers (OpenAI, DeepSeek, Mimo) share one adapter class —
            // only the config differs per provider (named options + a keyed registration each).
            // One shared named HttpClient carries the same resilience policy as Claude's.
            var openAiCompatibleHttpClientBuilder = services.AddHttpClient("llm-openai-compatible");
            openAiCompatibleHttpClientBuilder.AddStandardResilienceHandler(LlmResiliencePipeline.Configure);
            openAiCompatibleHttpClientBuilder.AddHttpMessageHandler<AiTracingHandler>();

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
