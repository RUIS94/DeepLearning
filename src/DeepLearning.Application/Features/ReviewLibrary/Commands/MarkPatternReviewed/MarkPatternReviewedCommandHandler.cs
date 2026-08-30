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
            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);
            _ = await _reviewLibraryRepository.GetPatternByIdAsync(request.PatternId, cancellationToken)
                ?? throw new NotFoundException(nameof(SentencePattern), request.PatternId);

            var now = DateTimeOffset.UtcNow;
            var review = await _reviewLibraryRepository.GetUserPatternReviewAsync(request.UserId, request.PatternId, cancellationToken);

            if (review is null)
            {
                review = new UserPatternReview
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    PatternId = request.PatternId,
                    TimesEncountered = 1,
                    MasteryLevel = request.MasteryLevel,
                    LastReviewedAt = now,
                    CreatedAt = now,
                };
                await _reviewLibraryRepository.AddUserPatternReviewAsync(review, cancellationToken);
            }
            else
            {
                review.MasteryLevel = request.MasteryLevel;
                review.LastReviewedAt = now;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MarkPatternReviewedResult(review.Id, review.PatternId, review.MasteryLevel, review.TimesEncountered, review.LastReviewedAt);
        }
    }
}
