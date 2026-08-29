namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension
{
    public record CreateAssessmentDimensionResult(
        Guid Id,
        Guid ExamTypeId,
        string DimensionKey,
        string RubricVersion,
        DateTimeOffset EffectiveFrom);
}
