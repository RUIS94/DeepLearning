using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class UserRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public UserRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Add_then_get_by_username_round_trips_through_a_real_database()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            await using var context = _fixture.CreateContext();
            var repository = new UserRepository(context);
            await repository.AddAsync(new User
            {
                Id = Guid.NewGuid(),
                Username = $"user_{suffix}",
                Email = $"user_{suffix}@example.com",
                PasswordHash = "irrelevant-for-this-test",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new UserRepository(readContext);
            var fetched = await readRepository.GetByUsernameAsync($"user_{suffix}");

            Assert.NotNull(fetched);
        }

        [Fact]
        public async Task Duplicate_username_is_rejected_by_the_database()
        {
            var username = $"user_{Guid.NewGuid():N}";

            await using var context = _fixture.CreateContext();
            var repository = new UserRepository(context);
            await repository.AddAsync(new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = $"{username}@example.com",
                PasswordHash = "irrelevant-for-this-test",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var secondContext = _fixture.CreateContext();
            var secondRepository = new UserRepository(secondContext);
            await secondRepository.AddAsync(new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = $"other_{username}@example.com",
                PasswordHash = "irrelevant-for-this-test",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await Assert.ThrowsAnyAsync<Exception>(() => secondContext.SaveChangesAsync());
        }
    }
}
