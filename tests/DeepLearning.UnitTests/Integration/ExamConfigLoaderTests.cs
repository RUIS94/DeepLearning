using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class ExamConfigLoaderTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ExamConfigLoaderTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Builds_a_prompt_by_concatenating_shared_methodology_then_exam_specific_layers()
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

            var templateRepository = new PromptTemplateRepository(context);
            await templateRepository.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.question_gen,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = "SHARED METHODOLOGY MARKER",
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await templateRepository.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                TemplateType = AiOperationType.question_gen,
                Layer = TemplateLayer.exam_specific,
                TemplateContent = "EXAM SPECIFIC MARKER, difficulty={{ difficulty }}",
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var loader = new ExamConfigLoader(
                new ExamTypeRepository(readContext),
                new PromptTemplateRepository(readContext),
                new PromptRenderer());

            var prompt = await loader.BuildPromptAsync(examType.Id, AiOperationType.question_gen, new { Difficulty = "medium" });

            Assert.Contains("SHARED METHODOLOGY MARKER", prompt);
            Assert.Contains("EXAM SPECIFIC MARKER, difficulty=medium", prompt);
            Assert.True(
                prompt.IndexOf("SHARED METHODOLOGY MARKER", StringComparison.Ordinal)
                    < prompt.IndexOf("EXAM SPECIFIC MARKER", StringComparison.Ordinal),
                "shared_methodology content must be rendered before exam_specific content");
        }
    }
}
