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

        private static ExamType NewExamType() => new()
        {
            Id = Guid.NewGuid(),
            Code = $"et_{Guid.NewGuid():N}",
            Name = "Test Exam Type",
            SubjectCategory = SubjectCategory.translation,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task ListAsync_includeGlobalScope_returns_the_exam_types_own_rows_plus_shared_ones_but_not_another_exam_types()
        {
            await using var context = _fixture.CreateContext();
            var repository = new PromptTemplateRepository(context);

            var examType = NewExamType();
            var otherExamType = NewExamType();
            await context.ExamTypes.AddRangeAsync(examType, otherExamType);

            var mine = new PromptTemplate
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                TemplateType = AiOperationType.followup,
                Layer = TemplateLayer.exam_specific,
                TemplateContent = $"mine_{examType.Id:N}",
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var shared = new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.followup,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = $"shared_{examType.Id:N}",
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var theirs = new PromptTemplate
            {
                Id = Guid.NewGuid(),
                ExamTypeId = otherExamType.Id,
                TemplateType = AiOperationType.followup,
                Layer = TemplateLayer.exam_specific,
                TemplateContent = $"theirs_{examType.Id:N}",
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await repository.AddAsync(mine);
            await repository.AddAsync(shared);
            await repository.AddAsync(theirs);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new PromptTemplateRepository(readContext);

            var withGlobal = await readRepository.ListAsync(
                examType.Id, subjectCategory: null, templateType: AiOperationType.followup, isActive: null,
                includeGlobalScope: true);
            Assert.Contains(withGlobal, x => x.Id == mine.Id);
            Assert.Contains(withGlobal, x => x.Id == shared.Id);
            Assert.DoesNotContain(withGlobal, x => x.Id == theirs.Id);

            // Default (what ExamConfigLoader relies on): exact match only, no NULL rows.
            var exactOnly = await readRepository.ListAsync(
                examType.Id, subjectCategory: null, templateType: AiOperationType.followup, isActive: null);
            Assert.Contains(exactOnly, x => x.Id == mine.Id);
            Assert.DoesNotContain(exactOnly, x => x.Id == shared.Id);
            Assert.DoesNotContain(exactOnly, x => x.Id == theirs.Id);
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
            var results = await readRepository.ListAsync(null, SubjectCategory.translation, AiOperationType.grading, null);

            Assert.Contains(results, x => x.TemplateContent == "test content");
        }
    }
}
