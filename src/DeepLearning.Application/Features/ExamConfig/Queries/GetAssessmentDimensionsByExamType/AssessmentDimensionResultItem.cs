using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetAssessmentDimensionsByExamType
{
    public record AssessmentDimensionResultItem(
        Guid Id,
        Guid ExamTypeId,
        string DimensionKey,
        string DimensionName,
        ScaleType ScaleType,
        string? PassThreshold,
        TaskType? ApplicableTaskType,
        string LevelDescriptions,
        string RubricVersion,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo,
        string? SourceReference,
        DateTimeOffset? VerifiedAt);
}
