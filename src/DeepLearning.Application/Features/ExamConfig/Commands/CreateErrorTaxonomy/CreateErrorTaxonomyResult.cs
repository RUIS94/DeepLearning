namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy
{
    public record CreateErrorTaxonomyResult(Guid Id, Guid ExamTypeId, string CategoryKey, string CategoryName);
}
