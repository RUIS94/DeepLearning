using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class ExamTypeRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ExamTypeRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Add_then_get_by_id_round_trips_through_a_real_database()
        {
            await using var context = _fixture.CreateContext();
            var repository = new ExamTypeRepository(context);

            var examType = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = $"test_{Guid.NewGuid():N}",
                Name = "Integration Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await repository.AddAsync(examType);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new ExamTypeRepository(readContext);
            var fetched = await readRepository.GetByIdAsync(examType.Id);

            Assert.NotNull(fetched);
            Assert.Equal(examType.Code, fetched!.Code);
        }

        [Fact]
        public async Task Code_uniqueness_is_enforced_by_the_database()
        {
            var code = $"test_{Guid.NewGuid():N}";

            await using var context = _fixture.CreateContext();
            var repository = new ExamTypeRepository(context);
            await repository.AddAsync(new ExamType
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = "First",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var secondContext = _fixture.CreateContext();
            var secondRepository = new ExamTypeRepository(secondContext);
            await secondRepository.AddAsync(new ExamType
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = "Second",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await Assert.ThrowsAnyAsync<Exception>(() => secondContext.SaveChangesAsync());
        }
    }
}
