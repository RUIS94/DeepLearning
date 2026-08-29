using DeepLearning.Domain.Enums;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;

namespace DeepLearning.Infrastructure.Persistence
{
    /// <summary>
    /// 把C#枚举注册为对应的Postgres原生枚举类型,供 DependencyInjection 与设计期
    /// AppDbContextFactory 共用,避免运行时/迁移生成时的映射对不上。
    /// </summary>
    public static class NpgsqlEnumConfiguration
    {
        // Shared, stateless translators reused across every call to MapEnums.
        // AddDbContext re-invokes its configure action once per scope (i.e. once
        // per request), so `new`-ing these per call made every resulting
        // DbContextOptions fingerprint distinct in EF's internal service-provider
        // cache (name translators don't have value equality) — EF built a brand
        // new internal service provider on every single request instead of
        // reusing one, tripping ManyServiceProvidersCreatedWarning after ~20
        // requests in one process. Static singletons keep the fingerprint stable.
        private static readonly NpgsqlNullNameTranslator NullTranslator = new();
        private static readonly NpgsqlSnakeCaseNameTranslator SnakeCaseTranslator = new();

        public static void MapEnums(NpgsqlDbContextOptionsBuilder o)
        {
            var t = NullTranslator;
            var snake = SnakeCaseTranslator;

            o.MapEnum<TaskType>("task_type_enum", nameTranslator: t);
            o.MapEnum<Difficulty>("difficulty_enum", nameTranslator: t);
            o.MapEnum<QuestionOrigin>("question_origin_enum", nameTranslator: t);
            o.MapEnum<SourceType>("source_type_enum", nameTranslator: t);
            o.MapEnum<Visibility>("visibility_enum", nameTranslator: snake);
            o.MapEnum<SubmissionStatus>("submission_status_enum", nameTranslator: t);
            o.MapEnum<FollowUpVerdict>("followup_verdict_enum", nameTranslator: t);
            o.MapEnum<OverrideScope>("override_scope_enum", nameTranslator: t);
            o.MapEnum<OverrideStatus>("override_status_enum", nameTranslator: t);
            o.MapEnum<WeakPointStatus>("weak_point_status_enum", nameTranslator: t);
            o.MapEnum<Priority>("priority_enum", nameTranslator: t);
            o.MapEnum<MasteryLevel>("mastery_level_enum", nameTranslator: snake);
            o.MapEnum<CategoryType>("category_type_enum", nameTranslator: t);
            o.MapEnum<SubjectCategory>("subject_category_enum", nameTranslator: t);
            o.MapEnum<ScaleType>("scale_type_enum", nameTranslator: t);
            o.MapEnum<AiOperationType>("ai_operation_type_enum", nameTranslator: t);
            o.MapEnum<TemplateLayer>("template_layer_enum", nameTranslator: t);
            o.MapEnum<CallStatus>("call_status_enum", nameTranslator: t);
            o.MapEnum<CheckpointImportance>("checkpoint_importance_enum", nameTranslator: t);
            o.MapEnum<KnowledgeItemType>("knowledge_item_type_enum", nameTranslator: t);
        }
    }
}
