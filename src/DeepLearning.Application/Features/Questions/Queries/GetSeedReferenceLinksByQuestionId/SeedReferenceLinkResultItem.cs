namespace DeepLearning.Application.Features.Questions.Queries.GetSeedReferenceLinksByQuestionId
{
    public record SeedReferenceLinkResultItem(
        Guid Id,
        Guid SeedQuestionId,
        string SeedQuestionTitle,
        string? SimilarityReason,
        DateTimeOffset CreatedAt);
}
