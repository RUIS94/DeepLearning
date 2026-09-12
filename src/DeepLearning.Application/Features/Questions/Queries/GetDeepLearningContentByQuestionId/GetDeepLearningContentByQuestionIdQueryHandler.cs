using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetDeepLearningContentByQuestionId
{
    public class GetDeepLearningContentByQuestionIdQueryHandler : IRequestHandler<GetDeepLearningContentByQuestionIdQuery, GetDeepLearningContentByQuestionIdResult>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly IReferenceTranslationRepository _referenceTranslationRepository;
        private readonly IReviewLibraryRepository _reviewLibraryRepository;

        public GetDeepLearningContentByQuestionIdQueryHandler(
            IQuestionRepository questionRepository,
            IReferenceTranslationRepository referenceTranslationRepository,
            IReviewLibraryRepository reviewLibraryRepository)
        {
            _questionRepository = questionRepository;
            _referenceTranslationRepository = referenceTranslationRepository;
            _reviewLibraryRepository = reviewLibraryRepository;
        }

        public async Task<GetDeepLearningContentByQuestionIdResult> Handle(GetDeepLearningContentByQuestionIdQuery request, CancellationToken cancellationToken)
        {
            var question = await _questionRepository.GetByIdAsync(request.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), request.QuestionId);

            if (question.Visibility != Visibility.Shared && question.CreatedBy != request.RequesterId)
            {
                throw new NotFoundException(nameof(Question), request.QuestionId);
            }

            var referenceTranslation = await _referenceTranslationRepository.GetByQuestionIdAsync(request.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(ReferenceTranslation), request.QuestionId);

            var patterns = await _reviewLibraryRepository.GetPatternsByQuestionIdAsync(request.QuestionId, cancellationToken);
            var vocab = await _reviewLibraryRepository.GetVocabByQuestionIdAsync(request.QuestionId, cancellationToken);

            return new GetDeepLearningContentByQuestionIdResult(
                referenceTranslation.QuestionId,
                referenceTranslation.ReferenceTitle,
                referenceTranslation.ReferenceText,
                referenceTranslation.ComparisonNotes,
                patterns.Select(p => new SentencePatternResultItem(p.Id, p.PatternName, p.ExampleSentence, p.BreakdownSteps, p.Variants, p.Domain, p.Scenario, p.FrequencyTag)).ToList(),
                vocab.Select(v => new VocabExpressionResultItem(v.Id, v.EnglishExpr, v.ChineseEquiv, v.ContextNote, v.Category, v.Domain, v.Scenario, v.FrequencyTag, v.LiteralTranslatable)).ToList());
        }
    }
}
