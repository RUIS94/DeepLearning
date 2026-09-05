using DeepLearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// One Postgres container, but a brand-new EMPTY database per test — the other fixtures hand out
    /// an already-migrated, already-written-to database, which is precisely what a fresh-install test
    /// must not have. Creating a database inside a running container costs milliseconds, so this is
    /// per-test isolation without a container per test.
    ///
    /// Deliberately does NOT migrate: what a bootstrap does to an empty database is the thing under
    /// test, so the fixture must leave it empty.
    /// </summary>
    public class FreshDatabaseFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        /// <summary>Creates an empty database and returns a connection string pointing at it.</summary>
        public async Task<string> CreateEmptyDatabaseAsync()
        {
            var name = $"fresh_{Guid.NewGuid():N}";

            await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
            {
                await admin.OpenAsync();
                // No parameters possible in CREATE DATABASE; the name is a Guid we just generated,
                // not caller input, so there is nothing to inject.
                await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
                await command.ExecuteNonQueryAsync();
            }

            return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ToString();
        }

        public static AppDbContext CreateContext(string connectionString)
            => new(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, NpgsqlEnumConfiguration.MapEnums)
                .UseSnakeCaseNamingConvention()
                .Options);

        public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var value = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(value!, typeof(T))!;
        }
    }

    [CollectionDefinition(Name)]
    public class FreshDatabaseCollection : ICollectionFixture<FreshDatabaseFixture>
    {
        public const string Name = "FreshDatabase";
    }
}
