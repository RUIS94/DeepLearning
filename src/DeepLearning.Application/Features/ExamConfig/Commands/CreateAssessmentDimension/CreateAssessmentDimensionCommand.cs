using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension
{
    public record CreateAssessmentDimensionCommand(
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
        string? SourceReference) : IRequest<CreateAssessmentDimensionResult>;
}
