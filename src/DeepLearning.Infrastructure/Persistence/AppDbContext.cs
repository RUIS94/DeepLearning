using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

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
        public DbSet<ErrorListItem> ErrorList => Set<ErrorListItem>();
        public DbSet<FollowUpQuestion> FollowUpQuestions => Set<FollowUpQuestion>();
        public DbSet<StandardOverride> StandardOverrides => Set<StandardOverride>();
        public DbSet<SentencePattern> SentencePatterns => Set<SentencePattern>();
        public DbSet<VocabExpression> VocabExpressions => Set<VocabExpression>();
        public DbSet<WeakPoint> WeakPoints => Set<WeakPoint>();
        public DbSet<WeakPointOccurrence> WeakPointOccurrences => Set<WeakPointOccurrence>();
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pgcrypto");
            modelBuilder.HasPostgresExtension("vector");

            // Postgres 原生枚举类型:大多数枚举成员名与schema.sql里的label逐字一致,
            // 用NullNameTranslator关闭名称转换,避免snake_case转换器对'A'/'B'、
            // 'band_1_5'这类label做出错误猜测。MasteryLevel/Visibility的label里
            // 有'new'/'private'这两个C#保留字,枚举成员改用PascalCase(New/Private),
            // 靠snake_case转换器换回label。
            modelBuilder.HasPostgresEnum<TaskType>(name: "task_type_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<Difficulty>(name: "difficulty_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<QuestionOrigin>(name: "question_origin_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<SourceType>(name: "source_type_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<Visibility>(name: "visibility_enum", nameTranslator: new NpgsqlSnakeCaseNameTranslator());
            modelBuilder.HasPostgresEnum<SubmissionStatus>(name: "submission_status_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<FollowUpVerdict>(name: "followup_verdict_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<OverrideScope>(name: "override_scope_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<OverrideStatus>(name: "override_status_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<WeakPointStatus>(name: "weak_point_status_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<Priority>(name: "priority_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<MasteryLevel>(name: "mastery_level_enum", nameTranslator: new NpgsqlSnakeCaseNameTranslator());
            modelBuilder.HasPostgresEnum<CategoryType>(name: "category_type_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<SubjectCategory>(name: "subject_category_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<ScaleType>(name: "scale_type_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<AiOperationType>(name: "ai_operation_type_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<TemplateLayer>(name: "template_layer_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<CallStatus>(name: "call_status_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<CheckpointImportance>(name: "checkpoint_importance_enum", nameTranslator: new NpgsqlNullNameTranslator());
            modelBuilder.HasPostgresEnum<KnowledgeItemType>(name: "knowledge_item_type_enum", nameTranslator: new NpgsqlNullNameTranslator());

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
