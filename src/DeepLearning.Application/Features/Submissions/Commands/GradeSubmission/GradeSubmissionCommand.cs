using MediatR;

namespace DeepLearning.Application.Features.Submissions.Commands.GradeSubmission
{
    /// <summary>
    /// ExamTypeId is caller-supplied rather than derived from the Submission/Question, mirroring
    /// GenerateQuestionCommand's own precedent — Question has no exam_type_id column yet (design
    /// doc §9.4 defers that FK until a second exam type actually exists), and the MVP only has
    /// one exam type anyway, so there's nothing to derive it from.
    /// </summary>
    public record GradeSubmissionCommand(Guid SubmissionId, Guid ExamTypeId) : IRequest<GradeSubmissionResult>;
}
