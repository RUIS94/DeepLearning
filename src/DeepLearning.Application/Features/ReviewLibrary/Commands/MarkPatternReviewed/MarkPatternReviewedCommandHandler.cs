using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed
{
    public class MarkPatternReviewedCommandHandler : IRequestHandler<MarkPatternReviewedCommand, MarkPatternReviewedResult>
    {
        private readonly IReviewLibraryRepository _reviewLibraryRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public MarkPatternReviewedCommandHandler(
            IReviewLibraryRepository reviewLibraryRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _reviewLibraryRepository = reviewLibraryRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<MarkPatternReviewedResult> Handle(MarkPatternReviewedCommand request, CancellationToken cancellationToken)
        {
            await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                .EnsureFoundAsync(nameof(User), request.UserId);
            await _reviewLibraryRepository.GetPatternByIdAsync(request.PatternId, cancellationToken)
                .EnsureFoundAsync(nameof(SentencePattern), request.PatternId);

            var now = DateTimeOffset.UtcNow;
            var existing = await _reviewLibraryRepository.GetUserPatternReviewAsync(request.UserId, request.PatternId, cancellationToken);

            var review = await ReviewLibrarySupport.UpsertReviewAsync(
                existing,
                createNew: () => new UserPatternReview
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    PatternId = request.PatternId,
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
                addAsync: _reviewLibraryRepository.AddUserPatternReviewAsync,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MarkPatternReviewedResult(review.Id, review.PatternId, review.MasteryLevel, review.TimesEncountered, review.LastReviewedAt);
        }
    }
}
