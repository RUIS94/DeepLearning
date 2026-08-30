using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Queries.GetQuestionBankCategoryById
{
    public class GetQuestionBankCategoryByIdQueryHandler : IRequestHandler<GetQuestionBankCategoryByIdQuery, GetQuestionBankCategoryByIdResult>
    {
        private readonly IQuestionBankCategoryRepository _categoryRepository;

        public GetQuestionBankCategoryByIdQueryHandler(IQuestionBankCategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<GetQuestionBankCategoryByIdResult> Handle(GetQuestionBankCategoryByIdQuery request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.QuestionBankCategory), request.Id);

            return new GetQuestionBankCategoryByIdResult(
                category.Id, category.CategoryType, category.Name, category.ParentId, category.Description, category.CreatedAt);
        }
    }
}
