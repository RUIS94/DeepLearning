using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed
{
    public class MarkVocabReviewedCommandHandler : IRequestHandler<MarkVocabReviewedCommand, MarkVocabReviewedResult>
    {
        private readonly IReviewLibraryRepository _reviewLibraryRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public MarkVocabReviewedCommandHandler(
            IReviewLibraryRepository reviewLibraryRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _reviewLibraryRepository = reviewLibraryRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<MarkVocabReviewedResult> Handle(MarkVocabReviewedCommand request, CancellationToken cancellationToken)
        {
            await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                .EnsureFoundAsync(nameof(User), request.UserId);
            await _reviewLibraryRepository.GetGlossaryEntryByIdAsync(request.VocabId, cancellationToken)
                .EnsureFoundAsync(nameof(VocabGlossaryEntry), request.VocabId);

            var now = DateTimeOffset.UtcNow;
            var existing = await _reviewLibraryRepository.GetUserVocabReviewAsync(request.UserId, request.VocabId, cancellationToken);

            var review = await ReviewLibrarySupport.UpsertReviewAsync(
                existing,
                createNew: () => new UserVocabReview
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    VocabId = request.VocabId,
                    TimesEncountered = 1,
                    MasteryLevel = request.MasteryLevel,
                    LastReviewedAt = now,
                    CreatedAt = now,
                },
                updateExisting: r =>
                {
                    r.MasteryLevel = request.MasteryLevel;
                    r.LastReviewedAt = now;
                },
                addAsync: _reviewLibraryRepository.AddUserVocabReviewAsync,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MarkVocabReviewedResult(review.Id, review.VocabId, review.MasteryLevel, review.TimesEncountered, review.LastReviewedAt);
        }
    }
}
