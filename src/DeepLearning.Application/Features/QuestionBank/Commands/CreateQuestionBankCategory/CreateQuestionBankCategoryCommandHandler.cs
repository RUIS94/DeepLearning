using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory
{
    public class CreateQuestionBankCategoryCommandHandler : IRequestHandler<CreateQuestionBankCategoryCommand, CreateQuestionBankCategoryResult>
    {
        private readonly IQuestionBankCategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateQuestionBankCategoryCommandHandler(IQuestionBankCategoryRepository categoryRepository, IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateQuestionBankCategoryResult> Handle(CreateQuestionBankCategoryCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentId is { } parentId)
            {
                _ = await _categoryRepository.GetByIdAsync(parentId, cancellationToken)
                    ?? throw new NotFoundException(nameof(QuestionBankCategory), parentId);
            }

            var category = new QuestionBankCategory
            {
                Id = Guid.NewGuid(),
                CategoryType = request.CategoryType,
                Name = request.Name,
                ParentId = request.ParentId,
                Description = request.Description,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _categoryRepository.AddAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateQuestionBankCategoryResult(category.Id, category.CategoryType, category.Name, category.ParentId, category.CreatedAt);
        }
    }
}
