using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory
{
    public class UpdateQuestionBankCategoryCommandHandler
        : IRequestHandler<UpdateQuestionBankCategoryCommand, UpdateQuestionBankCategoryResult>
    {
        private readonly IQuestionBankCategoryRepository _categoryRepository;
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateQuestionBankCategoryCommandHandler(
            IQuestionBankCategoryRepository categoryRepository,
            IExamTypeRepository examTypeRepository,
            IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UpdateQuestionBankCategoryResult> Handle(
            UpdateQuestionBankCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(QuestionBankCategory), request.Id);

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

            category.Name = request.Name;
            category.ParentId = request.ParentId;
            category.Description = request.Description;
            category.ExamTypeId = request.ExamTypeId;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new UpdateQuestionBankCategoryResult(
                category.Id, category.CategoryType, category.Name, category.ParentId, category.Description, category.ExamTypeId);
        }
    }
}
