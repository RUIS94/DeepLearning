using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // AiCallRetryExecutor's default 2s/4s/8s backoff (design doc §7) is correct for
            // production but would make every existing "AI response is invalid" test sit through
            // real multi-second sleeps for no reason — override with a near-instant delay so
            // retry behavior (attempt counting, eventual success/failure) is still exercised for
            // real, just fast.
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAiCallRetryExecutor>(new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1)));

                // Grading is queued to Hangfire in production so the HTTP request can return in
                // milliseconds instead of minutes. A test that had to wait for a background
                // worker would have to poll, which is slow and flaky for no gain — the thing
                // under test is the handler, not the queue. Running it inline keeps every
                // grading assertion deterministic and still exercises the real command.
                //
                // One deliberate divergence: inline, a grading failure surfaces on the POST as
                // 503/409, whereas in production the request has already returned 202 and the
                // failure shows up as the submission's own GradingFailed status. The tests that
                // assert those status codes are asserting the handler's error policy, which is
                // identical either way.
                services.AddScoped<IGradingJobQueue, InlineGradingJobQueue>();

                // Same reasoning for the weak-point extraction that follows a grading: a
                // test asserting on the weak points a submission produced should not have to
                // wait on a background worker to get there.
                services.AddScoped<IWeakPointGenerationQueue, InlineWeakPointGenerationQueue>();
            });
        }

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _container.GetConnectionString());

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
        }

        /// <summary>
        /// Registration/login moved entirely to Supabase Auth (see AGENTS.md's Auth section) — the
        /// backend no longer exposes a POST /users endpoint, so tests that just need a real FK-able
        /// `User` row (not exercising auth itself) seed one directly via DbContext, same convention
        /// as ReviewLibraryControllerTests.NewUser()/ExtractKnowledgePointsOnGradedTests.
        /// </summary>
        public async Task<Guid> SeedUserAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"test_{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid():N}@test.local",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
            return user.Id;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
            await _container.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
