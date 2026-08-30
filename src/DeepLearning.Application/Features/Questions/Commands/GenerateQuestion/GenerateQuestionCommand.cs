using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    /// <summary>
    /// Difficulty is optional — omit it (null) to have GenerateQuestionCommandHandler pick one
    /// via generation_policy's difficulty_distribution weighted ratio instead of the caller
    /// having to decide. An explicit value always wins outright over the policy.
    ///
    /// CategoryId is optional (design doc §11.2 Step 8: "先按领域/难度过滤") — when supplied, it
    /// narrows the real-exam samples used as few-shot generation reference to that
    /// question_bank_categories entry; omitted, the pool is just task type + difficulty. Ignored
    /// when SeedQuestionIds is supplied (see below).
    ///
    /// SeedQuestionIds is optional — when supplied (non-empty), it replaces the automatic
    /// task-type/difficulty/category filtering entirely: GenerateQuestionCommandHandler uses
    /// exactly these questions as few-shot reference, in the given order, instead of querying
    /// ListSeedReferenceCandidatesAsync. Every id must resolve to an existing Question with
    /// IsSeedReference=true (404/400 otherwise) — a caller can't point generation at an arbitrary,
    /// non-seed question. Capped at GenerateQuestionValidator.MaxSeedQuestionIds.
    /// </summary>
    public record GenerateQuestionCommand(
        Guid ExamTypeId,
        TaskType TaskType,
        Difficulty? Difficulty,
        Guid? CategoryId,
        List<Guid>? SeedQuestionIds,
        Guid? CreatedBy) : IRequest<GenerateQuestionResult>;
}
