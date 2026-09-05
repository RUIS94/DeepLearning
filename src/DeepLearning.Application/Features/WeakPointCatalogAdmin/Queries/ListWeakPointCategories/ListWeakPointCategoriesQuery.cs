using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCategories
{
    /// <summary>The fixed 8-row top-level taxonomy — lets the admin UI populate a category picker when creating/reviewing a leaf.</summary>
    public record ListWeakPointCategoriesQuery : IRequest<List<WeakPointCategoryResultItem>>;

    public record WeakPointCategoryResultItem(Guid Id, string Code, string Name, string? Description, int DisplayOrder);
}
