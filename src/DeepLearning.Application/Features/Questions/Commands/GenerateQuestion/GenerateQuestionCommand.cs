using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    /// <summary>
    /// Difficulty is optional — omit it (null) to have GenerateQuestionCommandHandler pick one
    /// via generation_policy's difficulty_distribution weighted ratio instead of the caller
    /// having to decide. An explicit value always wins outright over the policy.
    /// </summary>
    public record GenerateQuestionCommand(
        Guid ExamTypeId,
        TaskType TaskType,
        Difficulty? Difficulty,
        Guid? CreatedBy) : IRequest<GenerateQuestionResult>;
}
