using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.AnalyzeVocabSemanticDrift
{
    public class AnalyzeVocabSemanticDriftForQuestionCommandHandler : IRequestHandler<AnalyzeVocabSemanticDriftForQuestionCommand>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly IReviewLibraryRepository _reviewLibraryRepository;
        private readonly IVocabSemanticDriftService _driftService;
        private readonly IUnitOfWork _unitOfWork;

        public AnalyzeVocabSemanticDriftForQuestionCommandHandler(
            IQuestionRepository questionRepository,
            IReviewLibraryRepository reviewLibraryRepository,
            IVocabSemanticDriftService driftService,
            IUnitOfWork unitOfWork)
        {
            _questionRepository = questionRepository;
            _reviewLibraryRepository = reviewLibraryRepository;
            _driftService = driftService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AnalyzeVocabSemanticDriftForQuestionCommand request, CancellationToken cancellationToken)
        {
            var question = await _questionRepository.GetByIdAsync(request.QuestionId, cancellationToken);
            if (question is null)
            {
                return;
            }

            var recurring = await _reviewLibraryRepository.ListRecurringVocabForQuestionAsync(request.QuestionId, cancellationToken);
            if (recurring.Count == 0)
            {
                return;
            }

            var keys = recurring
                .Where(v => v.CanonicalKey is not null)
                .Select(v => v.CanonicalKey!)
                .Distinct()
                .ToList();

            var glossaryByKey = (await _reviewLibraryRepository.ListGlossaryEntriesByCanonicalKeysAsync(keys, cancellationToken))
                .ToDictionary(g => g.CanonicalKey);

            var items = new List<VocabDriftItem>();
            var seen = new HashSet<string>();
            foreach (var v in recurring)
            {
                if (v.CanonicalKey is null || !seen.Add(v.CanonicalKey))
                {
                    continue;
                }

                if (!glossaryByKey.TryGetValue(v.CanonicalKey, out var entry))
                {
                    continue;
                }

                items.Add(new VocabDriftItem(v.CanonicalKey, v.EnglishExpr, v.ChineseEquiv, v.ContextNote, entry.AccumulatedSemantics));
            }

            if (items.Count == 0)
            {
                return;
            }

            var updates = await _driftService.AnalyzeAsync(
                request.ExamTypeId, items, question.SourceText, question.TaskType.ToString(), cancellationToken);
            if (updates.Count == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var (key, updatedSemantics) in updates)
            {
                if (string.IsNullOrWhiteSpace(updatedSemantics) || !glossaryByKey.TryGetValue(key, out var entry))
                {
                    continue;
                }

                entry.AccumulatedSemantics = updatedSemantics;
                entry.SenseCount += 1;
                entry.UpdatedAt = now;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
