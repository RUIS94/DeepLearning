namespace DeepLearning.Application.Features.ExamConfig.Queries.GetErrorTaxonomiesByExamType
{
    public record ErrorTaxonomyResultItem(
        Guid Id,
        Guid ExamTypeId,
        string CategoryKey,
        string CategoryName,
        string? Description,
        string? ExampleCases);
}
