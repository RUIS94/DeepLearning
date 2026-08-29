using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.Submissions
{
    public class CreateSubmissionValidatorTests
    {
        private readonly CreateSubmissionValidator _validator = new();

        private static CreateSubmissionCommand Command(TaskType taskType, string content) => new(
            QuestionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            TaskType: taskType,
            Content: content);

        [Fact]
        public void Passes_for_task_a_content_that_is_a_json_string()
        {
            Assert.True(_validator.Validate(Command(TaskType.A, "\"my translation\"")).IsValid);
        }

        [Fact]
        public void Fails_for_task_a_content_that_is_a_json_array()
        {
            Assert.False(_validator.Validate(Command(TaskType.A, "[]")).IsValid);
        }

        [Fact]
        public void Passes_for_task_b_content_that_is_a_well_formed_annotation_array()
        {
            const string content = "[{\"positionStart\":9,\"positionEnd\":17,\"errorCategory\":\"distortion\",\"correctedText\":\"had\"}]";

            Assert.True(_validator.Validate(Command(TaskType.B, content)).IsValid);
        }

        [Fact]
        public void Fails_for_task_b_content_that_is_an_empty_array()
        {
            Assert.False(_validator.Validate(Command(TaskType.B, "[]")).IsValid);
        }

        [Fact]
        public void Fails_for_task_b_content_that_is_a_plain_string()
        {
            Assert.False(_validator.Validate(Command(TaskType.B, "\"not an annotation array\"")).IsValid);
        }

        [Fact]
        public void Fails_for_task_b_content_missing_a_required_annotation_field()
        {
            const string content = "[{\"positionStart\":9,\"positionEnd\":17,\"errorCategory\":\"distortion\"}]";

            Assert.False(_validator.Validate(Command(TaskType.B, content)).IsValid);
        }

        [Fact]
        public void Fails_when_content_is_not_valid_json_at_all()
        {
            Assert.False(_validator.Validate(Command(TaskType.A, "not json")).IsValid);
        }
    }
}
