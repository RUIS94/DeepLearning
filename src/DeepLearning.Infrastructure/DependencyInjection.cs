using DeepLearning.Application.Interfaces;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Ai.GradingResultInterpreters;
using DeepLearning.Infrastructure.Ai.Options;
using DeepLearning.Infrastructure.BackgroundJobs;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
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

            // appsettings.Development.json carries the non-secret half (host/port/database); the Supabase
            // username/password live in User Secrets next to the LLM API keys. A string that already names
            // them — the LocalDocker profile's postgres/postgres — passes through unchanged.
            connectionString = ConnectionStringCredentials.Apply(connectionString, configuration);

            // Cross-check the declared DB_PROFILE against the host this connection string actually
            // resolves to, and hard-fail on a mismatch — see DatabaseTargetResolver for why silence
            // here is the expensive outcome. Registered as a singleton so Program.cs can log it and
            // GET /health/db can report it.
            var databaseTarget = DatabaseTargetResolver.Resolve(
                configuration[DatabaseTargetResolver.ProfileConfigKey], connectionString);
            services.AddSingleton(databaseTarget);

            // Pulls the reference tables out of Supabase into a local throwaway DB — only ever invoked
            // by the `db pull-reference` CLI verb, never during request handling.
            services.AddScoped(sp => new ReferenceDataSync(
                databaseTarget,
                connectionString,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReferenceDataSync>>()));

            services.AddDbContext<AppDbContext>(options => options
                .UseNpgsql(connectionString, NpgsqlEnumConfiguration.MapEnums)
                .UseSnakeCaseNamingConvention());

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Hand-authored Persistence/Sql/*.sql runner — only invoked by the `sql` CLI verb in
            // Program.cs, never during normal request handling.
            services.AddSingleton<Persistence.Sql.ISqlScriptSource, Persistence.Sql.EmbeddedSqlScriptSource>();
            services.AddScoped(sp => new Persistence.Sql.SqlScriptRunner(
                connectionString,
                sp.GetRequiredService<Persistence.Sql.ISqlScriptSource>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Persistence.Sql.SqlScriptRunner>>()));

            services.AddScoped<IExamTypeRepository, ExamTypeRepository>();
            services.AddScoped<IAssessmentDimensionRepository, AssessmentDimensionRepository>();
            services.AddScoped<IErrorTaxonomyRepository, ErrorTaxonomyRepository>();
            services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IQuestionRepository, QuestionRepository>();
            services.AddScoped<IAiCallLogRepository, AiCallLogRepository>();
            services.AddScoped<ILlmProviderSettingsRepository, LlmProviderSettingsRepository>();
            services.AddScoped<ILlmProviderModelRepository, LlmProviderModelRepository>();
            services.AddScoped<IAiOperationProviderOverrideRepository, AiOperationProviderOverrideRepository>();
            services.AddScoped<ISubmissionRepository, SubmissionRepository>();
            services.AddScoped<IGradingSummaryRepository, GradingSummaryRepository>();
            services.AddScoped<IGenerationPolicyRepository, GenerationPolicyRepository>();
            services.AddScoped<IFollowUpQuestionRepository, FollowUpQuestionRepository>();
            services.AddScoped<IFollowUpThreadRepository, FollowUpThreadRepository>();
            services.AddScoped<IStandardOverrideRepository, StandardOverrideRepository>();
            services.AddScoped<IWeakPointRepository, WeakPointRepository>();
            services.AddScoped<IWeakPointCatalogRepository, WeakPointCatalogRepository>();
            services.AddScoped<IWeakPointCategoryRepository, WeakPointCategoryRepository>();
            services.AddScoped<IProgressRepository, ProgressRepository>();
            services.AddScoped<IReviewLibraryRepository, ReviewLibraryRepository>();
            services.AddScoped<IReferenceTranslationRepository, ReferenceTranslationRepository>();
            services.AddScoped<IQuestionBankCategoryRepository, QuestionBankCategoryRepository>();
            services.AddScoped<ISeedReferenceLinkRepository, SeedReferenceLinkRepository>();

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
            services.AddScoped<IWeakPointClassifier, WeakPointClassifier>();
            services.AddScoped<IWeakPointDetectionCriteriaGenerator, WeakPointDetectionCriteriaGenerator>();
            services.AddScoped<IWeakPointRecheckService, WeakPointRecheckService>();

            // design doc §4.2's retry sub-state-machine for a 200-OK-but-invalid-content AI
            // response — distinct from Polly's transport-level retries above.
            services.AddSingleton<IAiCallRetryExecutor>(new AiCallRetryExecutor());

            // --- Hangfire: design doc §7's "后台任务与定时重试,存储用PostgreSQL" ---------------
            // Same connection string/database as everything else — no separate infra to stand up.
            // Program.cs registers the actual recurring jobs (RecurringJob.AddOrUpdate); this is
            // just the storage + worker wiring, same layering as AddDbContext above.
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));
            services.AddHangfireServer();

            services.AddScoped<ProgressSnapshotJob>();
            services.AddScoped<StrandedGradingReclaimJob>();
            services.AddScoped<GradeSubmissionJob>();
            // Grading is queued rather than run on the request thread — see IGradingJobQueue.
            services.AddScoped<IGradingJobQueue, HangfireGradingJobQueue>();
            services.AddScoped<GenerateWeakPointsJob>();
            // Weak-point extraction runs after grading, not inside it — see IWeakPointGenerationQueue.
            services.AddScoped<IWeakPointGenerationQueue, HangfireWeakPointGenerationQueue>();

            return services;
        }
    }
}
