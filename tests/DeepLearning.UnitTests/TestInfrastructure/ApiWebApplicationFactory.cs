using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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

        // Every controller action requires authentication now (Program.cs's global AuthorizeFilter,
        // see ref/管理员与用户权限隔离_策划书.md Phase 1). Real Program.cs validates a Supabase-issued
        // JWT against its JWKS endpoint over the network — unreachable and undesirable from a test
        // run — so every test in this fixture gets a locally-verifiable symmetric signing key
        // instead, swapped in once here rather than per test class. This still exercises the real
        // JwtBearer handler + CurrentUserService + EnsureUserProfileMiddleware pipeline exactly as
        // Program.cs wires it; only where the signature gets checked differs.
        public const string TestIssuer = "https://test-project.supabase.co/auth/v1";

        private static readonly SymmetricSecurityKey TestSigningKey =
            new(Encoding.UTF8.GetBytes("test-only-hmac-signing-key-at-least-32-bytes-long"));

        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("deeplearning_api_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = "authenticated",
                        ValidateLifetime = true,
                        IssuerSigningKey = TestSigningKey,
                    };
                });
            });

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

                // Deep-learning generation enqueues vocab-glossary drift analysis on a recurring
                // expression. That analysis is a background LLM call with its own handler test —
                // an API test only needs the enqueue recorded, not run.
                services.AddSingleton<RecordingVocabGlossaryQueue>();
                services.AddScoped<IVocabGlossaryQueue>(sp => sp.GetRequiredService<RecordingVocabGlossaryQueue>());

                // Feature-flag reads are cached for 15s in production; across a shared-DB test
                // collection that would let one test's "flag absent -> default on" result mask a
                // later test that inserts a disabled row. Zero TTL = always read fresh.
                services.AddScoped<IFeatureFlagService>(sp => new DeepLearning.Api.Services.FeatureFlagService(
                    sp.GetRequiredService<IFeatureFlagRepository>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    TimeSpan.Zero));
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
        public async Task<Guid> SeedUserAsync(UserRole role = UserRole.user)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"test_{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid():N}@test.local",
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
            return user.Id;
        }

        /// <summary>Mints a locally-signed JWT for <paramref name="userId"/> against <see cref="TestSigningKey"/>.</summary>
        public static string CreateJwt(Guid userId, string? email = null)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken(
                issuer: TestIssuer,
                audience: "authenticated",
                claims: [new Claim("sub", userId.ToString()), new Claim("email", email ?? $"{userId:N}@test.local")],
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256));
            return handler.WriteToken(token);
        }

        /// <summary>
        /// A plain HttpClient carrying a valid JWT — the default for any test hitting an endpoint,
        /// now that every controller action requires authentication. EnsureUserProfileMiddleware
        /// creates the `public.users` row for <paramref name="userId"/> on first use if it doesn't
        /// already exist (as UserRole.user), so callers only need SeedUserAsync when they need the
        /// row to exist *before* the first authenticated call (e.g. to pre-set a role or FK a
        /// related row to it).
        /// </summary>
        public HttpClient CreateAuthenticatedClient(Guid? userId = null, string? email = null)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", CreateJwt(userId ?? Guid.NewGuid(), email));
            return client;
        }

        /// <summary>Seeds an admin user and returns an authenticated client for them.</summary>
        public async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
        {
            var userId = await SeedUserAsync(UserRole.admin);
            return CreateAuthenticatedClient(userId);
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
            await _container.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
