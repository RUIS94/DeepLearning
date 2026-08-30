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
            var vocab = await _reviewLibraryRepository.GetVocabByQuestionIdAsync(gradedEvent.QuestionId, cancellationToken);
            if (patterns.Count == 0 && vocab.Count == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;

            foreach (var pattern in patterns)
            {
                var review = await _reviewLibraryRepository.GetUserPatternReviewAsync(gradedEvent.UserId, pattern.Id, cancellationToken);
                if (review is null)
                {
                    await _reviewLibraryRepository.AddUserPatternReviewAsync(new UserPatternReview
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        PatternId = pattern.Id,
                        TimesEncountered = 1,
                        MasteryLevel = MasteryLevel.New,
                        LastReviewedAt = now,
                        CreatedAt = now,
                    }, cancellationToken);
                }
                else
                {
                    review.TimesEncountered += 1;
                    review.LastReviewedAt = now;
                }
            }

            foreach (var expr in vocab)
            {
                var review = await _reviewLibraryRepository.GetUserVocabReviewAsync(gradedEvent.UserId, expr.Id, cancellationToken);
                if (review is null)
                {
                    await _reviewLibraryRepository.AddUserVocabReviewAsync(new UserVocabReview
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        VocabId = expr.Id,
                        TimesEncountered = 1,
                        MasteryLevel = MasteryLevel.New,
                        LastReviewedAt = now,
                        CreatedAt = now,
                    }, cancellationToken);
                }
                else
                {
                    review.TimesEncountered += 1;
                    review.LastReviewedAt = now;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
