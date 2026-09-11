using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DeepLearning.Infrastructure.Persistence
{
    /// <summary>
    /// 把C#枚举注册为对应的Postgres原生枚举类型,供 DependencyInjection 与设计期
    /// AppDbContextFactory 共用,避免运行时/迁移生成时的映射对不上。枚举清单(CLR 类型/Postgres
    /// 名/name translator)单一来源见 PgEnumRegistry —— AppDbContext.OnModelCreating(EF 迁移用)
    /// 消费同一份清单。
    /// </summary>
    public static class NpgsqlEnumConfiguration
    {
        public static void MapEnums(NpgsqlDbContextOptionsBuilder o)
        {
            foreach (var e in PgEnumRegistry.Enums)
            {
                o.MapEnum(e.ClrType, e.PgName, nameTranslator: e.Translator);
            }
        }
    }
}
