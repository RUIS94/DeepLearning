using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Commands.CreateSubmission
{
    /// <summary>
    /// Content is raw JSON text, matching submissions.content's jsonb column (same convention
    /// as Question.Brief): a JSON-encoded string for TaskA's translation text, or a JSON array
    /// of annotation objects for TaskB (design doc §6.4) — the caller is responsible for
    /// shaping it correctly for TaskType, this command only validates it parses as JSON.
    /// </summary>
    public record CreateSubmissionCommand(
        Guid QuestionId,
        Guid UserId,
        TaskType TaskType,
        string Content) : IRequest<CreateSubmissionResult>;
}
