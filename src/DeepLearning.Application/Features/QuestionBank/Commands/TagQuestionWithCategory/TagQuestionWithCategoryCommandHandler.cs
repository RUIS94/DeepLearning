using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.TagQuestionWithCategory
{
    public class TagQuestionWithCategoryCommandHandler : IRequestHandler<TagQuestionWithCategoryCommand, TagQuestionWithCategoryResult>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly IQuestionBankCategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public TagQuestionWithCategoryCommandHandler(
            IQuestionRepository questionRepository,
            IQuestionBankCategoryRepository categoryRepository,
            IUnitOfWork unitOfWork)
        {
            _questionRepository = questionRepository;
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<TagQuestionWithCategoryResult> Handle(TagQuestionWithCategoryCommand request, CancellationToken cancellationToken)
        {
            var question = await _questionRepository.GetByIdAsync(request.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), request.QuestionId);
            _ = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.QuestionBankCategory), request.CategoryId);

            if (await _questionRepository.HasCategoryMapAsync(request.QuestionId, request.CategoryId, cancellationToken))
            {
                throw new ConflictException($"Question '{request.QuestionId}' is already tagged with category '{request.CategoryId}'.");
            }

            await _questionRepository.AddCategoryMapAsync(
                new QuestionCategoryMap { Id = Guid.NewGuid(), QuestionId = request.QuestionId, CategoryId = request.CategoryId },
                cancellationToken);

            question.InBank = true;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TagQuestionWithCategoryResult(question.Id, request.CategoryId, question.InBank);
        }
    }
}
