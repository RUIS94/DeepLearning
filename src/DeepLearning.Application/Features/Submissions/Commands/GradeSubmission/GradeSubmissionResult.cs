using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Submissions.Commands.GradeSubmission
{
    public record GradeSubmissionResult(
        Guid SubmissionId,
        SubmissionStatus Status,
        int GradingResultCount,
        int ErrorListCount);
}
