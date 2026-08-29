using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById
{
    public record GradingResultItem(
        Guid Id,
        string DimensionKey,
        string DimensionName,
        string RubricVersion,
        int Band,
        bool PassBool,
        string Rationale,
        bool CumulativeDensityFlag,
        string? CumulativeDensityNote,
        decimal? EstimatedPassProbability);

    public record ErrorListResultItem(
        Guid Id,
        string? PositionRef,
        string? SourceTextSnippet,
        string? UserTextSnippet,
        string ErrorCategory,
        string DimensionKey,
        bool ImpactsCore,
        string? Explanation,
        string? Suggestion);

    public record GetSubmissionByIdResult(
        Guid Id,
        Guid QuestionId,
        Guid UserId,
        TaskType TaskType,
        string Content,
        SubmissionStatus Status,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset CreatedAt,
        List<GradingResultItem> GradingResults,
        List<ErrorListResultItem> ErrorList);
}
