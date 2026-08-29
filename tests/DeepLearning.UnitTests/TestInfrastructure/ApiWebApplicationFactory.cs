using DeepLearning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// 起一个真实的Testcontainers Postgres,把它接到真实的ASP.NET Core host上
    /// (而不是mock DbContext),用于API层的端到端契约测试。
    ///
    /// 连接串通过环境变量注入,而不是WebApplicationFactory.ConfigureWebHost里的
    /// ConfigureAppConfiguration:Program.cs的AddInfrastructure(builder.Configuration)
    /// 在builder.Build()之前就已经把连接串读出来捕获成局部变量了,而ConfigureWebHost
    /// 的配置覆盖要等到Build()内部才生效——对这种"启动时立即读取并捕获"的写法来说太晚了。
    /// 环境变量则是builder.Configuration在最早期构建时就会读取的源,时机对得上。
    /// </summary>
    public class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private const string ConnectionStringEnvVar = "ConnectionStrings__DefaultConnection";

        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("deeplearning_api_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _container.GetConnectionString());

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
            await _container.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
