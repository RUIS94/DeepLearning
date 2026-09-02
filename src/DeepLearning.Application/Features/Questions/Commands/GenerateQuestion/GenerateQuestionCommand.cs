using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    /// <summary>
    /// Difficulty is optional — omit it (null) to have GenerateQuestionCommandHandler pick one
    /// via generation_policy's difficulty_distribution weighted ratio instead of the caller
    /// having to decide. An explicit value always wins outright over the policy.
    ///
    /// CategoryId is optional — when supplied it must be a real question_bank_categories row
    /// (404 otherwise). It pins the generated question's domain: that category's name is sent to
    /// the AI as a hard "brief.domain must be exactly this" directive (PinnedDomain), and it
    /// becomes the question's single question_category_map link. Omitted, the AI picks a domain
    /// from the injected list of existing domain categories and the handler links whichever one
    /// brief.domain resolves to (find-or-create).
    ///
    /// SeedQuestionIds is optional — few-shot real-exam samples are strictly opt-in. When
    /// supplied (non-empty), GenerateQuestionCommandHandler uses exactly these questions as
    /// few-shot reference, in the given order. When omitted, NO few-shot samples are retrieved
    /// or injected (the handler no longer auto-selects matching seeds). Every id must resolve to
    /// an existing Question with IsSeedReference=true (404/400 otherwise) — a caller can't point
    /// generation at an arbitrary, non-seed question. Capped at
    /// GenerateQuestionValidator.MaxSeedQuestionIds.
    ///
    /// TargetWeakPoints is optional, defaults to false (design doc §10.5's "出题与薄弱点联动",
    /// deliberately opt-in per call, not a global switch — see WeakPointTargetingSelector). When
    /// true AND CreatedBy is supplied, the handler may (not always — see the selector's own doc)
    /// bias this generation toward one of that user's active weak points. Ignored if CreatedBy is
    /// null — there's no user to look weak points up for.
    /// </summary>
    public record GenerateQuestionCommand(
        Guid ExamTypeId,
        TaskType TaskType,
        Difficulty? Difficulty,
        Guid? CategoryId,
        List<Guid>? SeedQuestionIds,
        Guid? CreatedBy,
        bool TargetWeakPoints = false) : IRequest<GenerateQuestionResult>;
}
