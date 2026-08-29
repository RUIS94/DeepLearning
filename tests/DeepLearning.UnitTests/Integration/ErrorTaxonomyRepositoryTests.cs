using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class ErrorTaxonomyRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ErrorTaxonomyRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Add_then_list_by_exam_type_round_trips_through_a_real_database()
        {
            await using var context = _fixture.CreateContext();
            var examType = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = $"test_{Guid.NewGuid():N}",
                Name = "Integration Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.ExamTypes.AddAsync(examType);

            var repository = new ErrorTaxonomyRepository(context);
            await repository.AddAsync(new ErrorTaxonomy
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                CategoryKey = "distortion",
                CategoryName = "Distortion",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new ErrorTaxonomyRepository(readContext);
            var results = await readRepository.ListByExamTypeAsync(examType.Id);

            Assert.Single(results);
            Assert.Equal("distortion", results[0].CategoryKey);
        }
    }
}
