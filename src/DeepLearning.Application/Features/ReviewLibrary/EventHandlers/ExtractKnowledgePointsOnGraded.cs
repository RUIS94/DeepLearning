using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.EventHandlers
{
    /// <summary>
    /// Step 6's minimal closed loop for the "知识点抽取" subscriber: sentence_patterns/
    /// vocab_expressions AI extraction from a submission's errors is Step 7's "深入学习模块"
    /// scope, not this handler's. What this handler does now — mark any SentencePattern/
    /// VocabExpression already linked to the graded Question (QuestionId FK) as encountered for
    /// this user, upserting user_pattern_review/user_vocab_review — is real, not a stub: it's a
    /// no-op until a question actually has linked patterns/vocab, and once Step 7's extraction
    /// starts populating those tables, this handler starts doing real work with no changes here.
    /// </summary>
    public class ExtractKnowledgePointsOnGraded : INotificationHandler<DomainEventNotification<SubmissionGradedEvent>>
    {
        private readonly IReviewLibraryRepository _reviewLibraryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ExtractKnowledgePointsOnGraded(IReviewLibraryRepository reviewLibraryRepository, IUnitOfWork unitOfWork)
        {
            _reviewLibraryRepository = reviewLibraryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<SubmissionGradedEvent> notification, CancellationToken cancellationToken)
        {
            var gradedEvent = notification.DomainEvent;

            var patterns = await _reviewLibraryRepository.GetPatternsByQuestionIdAsync(gradedEvent.QuestionId, cancellationToken);

            // vocab_expressions are per-question snapshots; mastery (user_vocab_review) is tracked
            // against the canonical vocab_glossary entry, so map this question's snapshots to
            // their glossary rows via canonical_key.
            var questionVocab = await _reviewLibraryRepository.GetVocabByQuestionIdAsync(gradedEvent.QuestionId, cancellationToken);
            var canonicalKeys = questionVocab
                .Where(v => v.CanonicalKey is not null)
                .Select(v => v.CanonicalKey!);
            var glossaryEntries = await _reviewLibraryRepository.ListGlossaryEntriesByCanonicalKeysAsync(canonicalKeys, cancellationToken);

            if (patterns.Count == 0 && glossaryEntries.Count == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;

            foreach (var pattern in patterns)
            {
                var review = await _reviewLibraryRepository.GetUserPatternReviewAsync(gradedEvent.UserId, pattern.Id, cancellationToken);
                await ReviewLibrarySupport.UpsertReviewAsync(
                    review,
                    createNew: () => new UserPatternReview
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        PatternId = pattern.Id,
                        TimesEncountered = 1,
                        MasteryLevel = MasteryLevel.New,
                        LastReviewedAt = now,
                        CreatedAt = now,
                    },
                    updateExisting: r =>
                    {
                        r.TimesEncountered += 1;
                        r.LastReviewedAt = now;
                    },
                    addAsync: _reviewLibraryRepository.AddUserPatternReviewAsync,
                    cancellationToken);
            }

            foreach (var entry in glossaryEntries)
            {
                var review = await _reviewLibraryRepository.GetUserVocabReviewAsync(gradedEvent.UserId, entry.Id, cancellationToken);
                await ReviewLibrarySupport.UpsertReviewAsync(
                    review,
                    createNew: () => new UserVocabReview
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        VocabId = entry.Id,
                        TimesEncountered = 1,
                        MasteryLevel = MasteryLevel.New,
                        LastReviewedAt = now,
                        CreatedAt = now,
                    },
                    updateExisting: r =>
                    {
                        r.TimesEncountered += 1;
                        r.LastReviewedAt = now;
                    },
                    addAsync: _reviewLibraryRepository.AddUserVocabReviewAsync,
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
