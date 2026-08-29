using DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.ExamConfig
{
    public class CreatePromptTemplateValidatorTests
    {
        private readonly CreatePromptTemplateValidator _validator = new();

        [Fact]
        public void Passes_for_exam_specific_with_exam_type_id_and_no_subject_category()
        {
            var command = new CreatePromptTemplateCommand(
                Guid.NewGuid(), null, AiOperationType.grading, TemplateLayer.exam_specific, "content", 1);

            Assert.True(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Passes_for_shared_methodology_with_subject_category_and_no_exam_type_id()
        {
            var command = new CreatePromptTemplateCommand(
                null, SubjectCategory.translation, AiOperationType.grading, TemplateLayer.shared_methodology, "content", 1);

            Assert.True(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_for_exam_specific_missing_exam_type_id()
        {
            var command = new CreatePromptTemplateCommand(
                null, null, AiOperationType.grading, TemplateLayer.exam_specific, "content", 1);

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_when_both_exam_type_id_and_subject_category_are_set()
        {
            var command = new CreatePromptTemplateCommand(
                Guid.NewGuid(), SubjectCategory.translation, AiOperationType.grading, TemplateLayer.exam_specific, "content", 1);

            Assert.False(_validator.Validate(command).IsValid);
        }
    }
}
