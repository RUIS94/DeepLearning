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
            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);
            _ = await _reviewLibraryRepository.GetVocabByIdAsync(request.VocabId, cancellationToken)
                ?? throw new NotFoundException(nameof(VocabExpression), request.VocabId);

            var now = DateTimeOffset.UtcNow;
            var review = await _reviewLibraryRepository.GetUserVocabReviewAsync(request.UserId, request.VocabId, cancellationToken);

            if (review is null)
            {
                review = new UserVocabReview
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    VocabId = request.VocabId,
                    TimesEncountered = 1,
                    MasteryLevel = request.MasteryLevel,
                    LastReviewedAt = now,
                    CreatedAt = now,
                };
                await _reviewLibraryRepository.AddUserVocabReviewAsync(review, cancellationToken);
            }
            else
            {
                review.MasteryLevel = request.MasteryLevel;
                review.LastReviewedAt = now;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MarkVocabReviewedResult(review.Id, review.VocabId, review.MasteryLevel, review.TimesEncountered, review.LastReviewedAt);
        }
    }
}
