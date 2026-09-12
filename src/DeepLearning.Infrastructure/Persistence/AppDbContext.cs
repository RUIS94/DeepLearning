using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ExamType> ExamTypes => Set<ExamType>();
        public DbSet<AssessmentDimension> AssessmentDimensions => Set<AssessmentDimension>();
        public DbSet<ErrorTaxonomy> ErrorTaxonomies => Set<ErrorTaxonomy>();
        public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
        public DbSet<GenerationPolicy> GenerationPolicies => Set<GenerationPolicy>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<TaskBSeededError> TaskBSeededErrors => Set<TaskBSeededError>();
        public DbSet<MeaningCheckpoint> MeaningCheckpoints => Set<MeaningCheckpoint>();
        public DbSet<ReferenceTranslation> ReferenceTranslations => Set<ReferenceTranslation>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<GradingResult> GradingResults => Set<GradingResult>();
        public DbSet<GradingResultRevision> GradingResultRevisions => Set<GradingResultRevision>();
        public DbSet<GradingSummary> GradingSummaries => Set<GradingSummary>();
        public DbSet<ErrorListItem> ErrorList => Set<ErrorListItem>();
        public DbSet<FollowUpQuestion> FollowUpQuestions => Set<FollowUpQuestion>();
        public DbSet<FollowUpThread> FollowUpThreads => Set<FollowUpThread>();
        public DbSet<FollowUpMessage> FollowUpMessages => Set<FollowUpMessage>();
        public DbSet<StandardOverride> StandardOverrides => Set<StandardOverride>();
        public DbSet<SentencePattern> SentencePatterns => Set<SentencePattern>();
        public DbSet<VocabExpression> VocabExpressions => Set<VocabExpression>();
        public DbSet<VocabGlossaryEntry> VocabGlossary => Set<VocabGlossaryEntry>();
        public DbSet<WeakPoint> WeakPoints => Set<WeakPoint>();
        public DbSet<WeakPointOccurrence> WeakPointOccurrences => Set<WeakPointOccurrence>();
        public DbSet<WeakPointCatalog> WeakPointCatalog => Set<WeakPointCatalog>();
        public DbSet<WeakPointCategory> WeakPointCategories => Set<WeakPointCategory>();
        public DbSet<ProgressSnapshot> ProgressSnapshots => Set<ProgressSnapshot>();
        public DbSet<AiCallLog> AiCallLogs => Set<AiCallLog>();
        public DbSet<QuestionBankCategory> QuestionBankCategories => Set<QuestionBankCategory>();
        public DbSet<QuestionCategoryMap> QuestionCategoryMap => Set<QuestionCategoryMap>();
        public DbSet<SeedReferenceLink> SeedReferenceLinks => Set<SeedReferenceLink>();
        public DbSet<UserPatternReview> UserPatternReview => Set<UserPatternReview>();
        public DbSet<UserVocabReview> UserVocabReview => Set<UserVocabReview>();
        public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
        public DbSet<KnowledgePoint> KnowledgePoints => Set<KnowledgePoint>();
        public DbSet<UserKnowledgePointReview> UserKnowledgePointReview => Set<UserKnowledgePointReview>();
        public DbSet<LlmProviderSettings> LlmProviderSettings => Set<LlmProviderSettings>();
        public DbSet<LlmProviderModel> LlmProviderModels => Set<LlmProviderModel>();
        public DbSet<AiOperationProviderOverride> AiOperationProviderOverrides => Set<AiOperationProviderOverride>();
        public DbSet<UserFeatureOverride> UserFeatureOverrides => Set<UserFeatureOverride>();
        public DbSet<UserExamTypeActivation> UserExamTypeActivations => Set<UserExamTypeActivation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pgcrypto");
            modelBuilder.HasPostgresExtension("vector");

            // 枚举清单(CLR 类型/Postgres 名/name translator)单一来源见 PgEnumRegistry —— 运行时
            // UseNpgsql 的 NpgsqlEnumConfiguration.MapEnums 消费同一份清单,两边不会再各写各的。
            PgEnumRegistry.ApplyTo(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
