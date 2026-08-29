using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class PromptTemplateRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public PromptTemplateRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Add_then_list_shared_methodology_template_round_trips_through_a_real_database()
        {
            await using var context = _fixture.CreateContext();
            var repository = new PromptTemplateRepository(context);

            await repository.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.grading,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = "test content",
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new PromptTemplateRepository(readContext);
            var results = await readRepository.ListAsync(null, SubjectCategory.translation, AiOperationType.grading);

            Assert.Contains(results, x => x.TemplateContent == "test content");
        }
    }
}
