using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;

namespace DeepLearning.UnitTests.Domain
{
    public class SubmissionTests
    {
        private static Submission NewSubmission(SubmissionStatus status) => new()
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TaskType = TaskType.A,
            Content = "\"some translation\"",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        [Theory]
        [InlineData(SubmissionStatus.draft, SubmissionStatus.submitted)]
        [InlineData(SubmissionStatus.submitted, SubmissionStatus.grading)]
        [InlineData(SubmissionStatus.grading, SubmissionStatus.grading_failed)]
        [InlineData(SubmissionStatus.grading, SubmissionStatus.graded)]
        [InlineData(SubmissionStatus.grading_failed, SubmissionStatus.grading)]
        [InlineData(SubmissionStatus.grading_failed, SubmissionStatus.grading_abandoned)]
        [InlineData(SubmissionStatus.graded, SubmissionStatus.under_dispute)]
        [InlineData(SubmissionStatus.graded, SubmissionStatus.archived)]
        [InlineData(SubmissionStatus.under_dispute, SubmissionStatus.standard_revised)]
        [InlineData(SubmissionStatus.under_dispute, SubmissionStatus.graded)]
        [InlineData(SubmissionStatus.standard_revised, SubmissionStatus.graded)]
        public void Allows_every_legal_transition(SubmissionStatus from, SubmissionStatus to)
        {
            var submission = NewSubmission(from);

            submission.TransitionTo(to);

            Assert.Equal(to, submission.Status);
        }

        [Theory]
        [InlineData(SubmissionStatus.draft, SubmissionStatus.grading)]
        [InlineData(SubmissionStatus.draft, SubmissionStatus.graded)]
        [InlineData(SubmissionStatus.submitted, SubmissionStatus.graded)]
        [InlineData(SubmissionStatus.submitted, SubmissionStatus.submitted)]
        [InlineData(SubmissionStatus.graded, SubmissionStatus.grading)]
        [InlineData(SubmissionStatus.grading_abandoned, SubmissionStatus.grading)]
        [InlineData(SubmissionStatus.archived, SubmissionStatus.graded)]
        public void Rejects_every_illegal_transition(SubmissionStatus from, SubmissionStatus to)
        {
            var submission = NewSubmission(from);

            var ex = Assert.Throws<InvalidSubmissionStateException>(() => submission.TransitionTo(to));
            Assert.Equal(from, submission.Status);
            Assert.Contains(from.ToString(), ex.Message);
            Assert.Contains(to.ToString(), ex.Message);
        }

        [Fact]
        public void A_concurrent_second_grade_attempt_is_rejected_while_the_first_is_still_grading()
        {
            var submission = NewSubmission(SubmissionStatus.submitted);
            submission.TransitionTo(SubmissionStatus.grading);

            Assert.Throws<InvalidSubmissionStateException>(() => submission.TransitionTo(SubmissionStatus.grading));
        }
    }
}
