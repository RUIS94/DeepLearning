using System.Reflection;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;

namespace DeepLearning.Infrastructure.Persistence
{
    /// <summary>
    /// 单一来源的 C# 枚举 ↔ Postgres 原生枚举类型注册清单。AppDbContext.OnModelCreating(EF 迁移用)
    /// 和 NpgsqlEnumConfiguration.MapEnums(运行时 UseNpgsql 用)各自调各自的 API 消费同一份清单——
    /// 之前两处各手写一份,曾经漏了 FollowUpThreadKind/WeakPointCatalogStatus/ErrorSeverity 3 个
    /// 类型没在 AppDbContext 里注册(R-I-15)。以后新增枚举只改这一处。
    ///
    /// Shared, stateless translators —— name translator 没有值相等语义,per-call new 会让每个
    /// DbContextOptions 指纹不稳定,导致 EF 在每个请求都新建内部 service provider,几十个请求后
    /// 触发 ManyServiceProvidersCreatedWarning(NpgsqlEnumConfiguration 原有注释里记录过这个事故)。
    /// </summary>
    public static class PgEnumRegistry
    {
        private static readonly NpgsqlNullNameTranslator NullTranslator = new();
        private static readonly NpgsqlSnakeCaseNameTranslator SnakeCaseTranslator = new();

        public static readonly IReadOnlyList<PgEnumRegistration> Enums = new PgEnumRegistration[]
        {
            // 大多数枚举成员名与 schema.sql 里的 label 逐字一致,用 NullTranslator 关闭名称转换,
            // 避免 snake_case 转换器对 'A'/'B'、'band_1_5' 这类 label 做出错误猜测。
            // MasteryLevel/Visibility 的 label 里有 'new'/'private' 这两个 C# 保留字,枚举成员
            // 改用 PascalCase(New/Private),靠 snake_case 转换器换回 label。
            new(typeof(TaskType), "task_type_enum", NullTranslator),
            new(typeof(Difficulty), "difficulty_enum", NullTranslator),
            new(typeof(QuestionOrigin), "question_origin_enum", NullTranslator),
            new(typeof(SourceType), "source_type_enum", NullTranslator),
            new(typeof(Visibility), "visibility_enum", SnakeCaseTranslator),
            new(typeof(SubmissionStatus), "submission_status_enum", NullTranslator),
            new(typeof(FollowUpVerdict), "followup_verdict_enum", NullTranslator),
            new(typeof(FollowUpThreadStatus), "follow_up_thread_status_enum", NullTranslator),
            new(typeof(FollowUpThreadKind), "follow_up_thread_kind_enum", NullTranslator),
            new(typeof(FollowUpMessageRole), "follow_up_message_role_enum", NullTranslator),
            new(typeof(OverrideScope), "override_scope_enum", NullTranslator),
            new(typeof(OverrideStatus), "override_status_enum", NullTranslator),
            new(typeof(WeakPointStatus), "weak_point_status_enum", NullTranslator),
            new(typeof(WeakPointCatalogStatus), "weak_point_catalog_status_enum", NullTranslator),
            new(typeof(Priority), "priority_enum", NullTranslator),
            new(typeof(MasteryLevel), "mastery_level_enum", SnakeCaseTranslator),
            new(typeof(CategoryType), "category_type_enum", NullTranslator),
            new(typeof(SubjectCategory), "subject_category_enum", NullTranslator),
            new(typeof(ScaleType), "scale_type_enum", NullTranslator),
            new(typeof(AiOperationType), "ai_operation_type_enum", NullTranslator),
            new(typeof(TemplateLayer), "template_layer_enum", NullTranslator),
            new(typeof(CallStatus), "call_status_enum", NullTranslator),
            new(typeof(CheckpointImportance), "checkpoint_importance_enum", NullTranslator),
            new(typeof(ErrorSeverity), "error_severity_enum", NullTranslator),
            new(typeof(KnowledgeItemType), "knowledge_item_type_enum", NullTranslator),
        };

        // ModelBuilder.HasPostgresEnum only exposes a generic <TEnum> overload — unlike
        // NpgsqlDbContextOptionsBuilder.MapEnum, there is no Type-parameter version — so
        // reflection is what lets AppDbContext iterate this same list instead of hand-writing
        // one generic call site per enum.
        private static readonly MethodInfo HasPostgresEnumMethod = typeof(NpgsqlModelBuilderExtensions)
            .GetMethods()
            .Single(m => m.Name == nameof(NpgsqlModelBuilderExtensions.HasPostgresEnum) && m.IsGenericMethodDefinition);

        public static void ApplyTo(ModelBuilder modelBuilder)
        {
            foreach (var e in Enums)
            {
                HasPostgresEnumMethod.MakeGenericMethod(e.ClrType).Invoke(null, [modelBuilder, null, e.PgName, e.Translator]);
            }
        }
    }

    public sealed record PgEnumRegistration(Type ClrType, string PgName, INpgsqlNameTranslator Translator);
}
