using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.DeleteQuestionBankCategory
{
    public class DeleteQuestionBankCategoryCommandHandler : IRequestHandler<DeleteQuestionBankCategoryCommand>
    {
        private readonly IQuestionBankCategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteQuestionBankCategoryCommandHandler(
            IQuestionBankCategoryRepository categoryRepository, IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DeleteQuestionBankCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(QuestionBankCategory), request.Id);

            if (await _categoryRepository.HasChildrenAsync(request.Id, cancellationToken))
            {
                throw new ConflictException(
                    $"Category '{category.Id}' has child categories — reparent or delete them first.");
            }

            if (await _categoryRepository.IsReferencedByQuestionsAsync(request.Id, cancellationToken))
            {
                throw new ConflictException(
                    $"Category '{category.Id}' is still tagged on one or more questions — untag them first.");
            }

            _categoryRepository.Remove(category);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
