using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory
{
    public class CreateQuestionBankCategoryCommandHandler : IRequestHandler<CreateQuestionBankCategoryCommand, CreateQuestionBankCategoryResult>
    {
        private readonly IQuestionBankCategoryRepository _categoryRepository;
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateQuestionBankCategoryCommandHandler(
            IQuestionBankCategoryRepository categoryRepository,
            IExamTypeRepository examTypeRepository,
            IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateQuestionBankCategoryResult> Handle(CreateQuestionBankCategoryCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentId is { } parentId)
            {
                _ = await _categoryRepository.GetByIdAsync(parentId, cancellationToken)
                    ?? throw new NotFoundException(nameof(QuestionBankCategory), parentId);
            }

            if (request.ExamTypeId is { } examTypeId)
            {
                _ = await _examTypeRepository.GetByIdAsync(examTypeId, cancellationToken)
                    ?? throw new NotFoundException(nameof(ExamType), examTypeId);
            }

            var category = new QuestionBankCategory
            {
                Id = Guid.NewGuid(),
                CategoryType = request.CategoryType,
                Name = request.Name,
                ParentId = request.ParentId,
                Description = request.Description,
                ExamTypeId = request.ExamTypeId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _categoryRepository.AddAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateQuestionBankCategoryResult(
                category.Id, category.CategoryType, category.Name, category.ParentId, category.ExamTypeId, category.CreatedAt);
        }
    }
}
