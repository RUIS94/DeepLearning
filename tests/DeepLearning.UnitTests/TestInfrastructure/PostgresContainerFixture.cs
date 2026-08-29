using DeepLearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// 每个测试类共享一个真实的、临时的Postgres容器(而非内存数据库),
    /// 用于验证EF迁移历史、原生枚举注册和外键约束在一个真正的空白数据库上确实能跑通。
    /// </summary>
    public class PostgresContainerFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("deeplearning_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        // Built once and reused for every CreateContext() call. Building a fresh
        // DbContextOptions per call makes EF Core treat each one as a distinct
        // configuration and spin up a brand new internal service provider for it;
        // past ~20 of those in one process EF trips its "ManyServiceProvidersCreatedWarning"
        // safety net and starts throwing.
        private DbContextOptions<AppDbContext> _options = null!;

        public string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString, NpgsqlEnumConfiguration.MapEnums)
                .UseSnakeCaseNamingConvention()
                .Options;

            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public AppDbContext CreateContext() => new(_options);

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    }
}
