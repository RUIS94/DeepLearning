using MediatR;

namespace DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion
{
    /// <summary>
    /// ExamTypeId is caller-supplied, same precedent as GradeSubmissionCommand/GenerateQuestionCommand
    /// (Question has no exam_type_id column yet — design doc §9.4). ContextRef optionally points at
    /// the specific grading_result/error_list id the user is disputing (design doc §6.7) — opaque to
    /// this handler, just persisted and passed to the AI as extra context.
    /// </summary>
    public record CreateFollowUpQuestionCommand(
        Guid SubmissionId,
        Guid UserId,
        Guid ExamTypeId,
        string? ContextRef,
        string QuestionText) : IRequest<CreateFollowUpQuestionResult>;
}
