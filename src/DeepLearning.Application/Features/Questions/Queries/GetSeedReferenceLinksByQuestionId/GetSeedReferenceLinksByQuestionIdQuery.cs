using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetSeedReferenceLinksByQuestionId
{
    /// <summary>
    /// Traceability read (design doc §11.2 Step 8: "记录了每次出题参考了哪些真题") — which
    /// real-exam samples were used as few-shot reference when this (AI-generated) question was
    /// created. Empty for a question that wasn't AI-generated, or generated with no matching
    /// seed samples on file.
    /// </summary>
    public record GetSeedReferenceLinksByQuestionIdQuery(Guid GeneratedQuestionId) : IRequest<List<SeedReferenceLinkResultItem>>;
}
