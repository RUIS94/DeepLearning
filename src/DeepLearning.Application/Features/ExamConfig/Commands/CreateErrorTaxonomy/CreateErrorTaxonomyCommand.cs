using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy
{
    public record CreateErrorTaxonomyCommand(
        Guid ExamTypeId,
        string CategoryKey,
        string CategoryName,
        string? Description,
        string? ExampleCases) : IRequest<CreateErrorTaxonomyResult>;
}
