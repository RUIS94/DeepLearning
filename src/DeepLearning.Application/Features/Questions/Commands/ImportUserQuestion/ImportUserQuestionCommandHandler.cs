using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion
{
    public class ImportUserQuestionCommandHandler : IRequestHandler<ImportUserQuestionCommand, ImportUserQuestionResult>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ImportUserQuestionCommandHandler(
            IQuestionRepository questionRepository,
            IUserRepository userRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IUnitOfWork unitOfWork)
        {
            _questionRepository = questionRepository;
            _userRepository = userRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ImportUserQuestionResult> Handle(ImportUserQuestionCommand request, CancellationToken cancellationToken)
        {
            if (request.CreatedBy is { } createdBy)
            {
                _ = await _userRepository.GetByIdAsync(createdBy, cancellationToken)
                    ?? throw new NotFoundException(nameof(User), createdBy);
            }

            foreach (var errorTaxonomyId in request.SeededErrors.Select(e => e.ErrorTaxonomyId).Distinct())
            {
                _ = await _errorTaxonomyRepository.GetByIdAsync(errorTaxonomyId, cancellationToken)
                    ?? throw new NotFoundException(nameof(ErrorTaxonomy), errorTaxonomyId);
            }

            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = request.TaskType,
                Difficulty = request.Difficulty,
                Title = request.Title,
                // brief is a jsonb column: a blank/whitespace string is not valid JSON and
                // makes SaveChanges throw 22P02. The validator deliberately lets an empty
                // Brief through (treats it as "no brief"), so normalize it to null here.
                Brief = string.IsNullOrWhiteSpace(request.Brief) ? null : request.Brief,
                SourceText = request.SourceText,
                FlawedTranslationText = request.FlawedTranslationText,
                // word_count is always derived from the source passage (title/brief excluded),
                // never taken from the request — a hand-entered question shouldn't require the
                // importer to count words. TaskB's FlawedTranslationText is not counted: this
                // field measures the passage to be translated.
                WordCount = WordCountCalculator.Count(request.SourceText),
                Origin = request.IsSeedReference ? QuestionOrigin.real_exam_seed : QuestionOrigin.user_uploaded,
                SourceType = request.IsSeedReference ? SourceType.real_exam : SourceType.user_generated,
                IsSeedReference = request.IsSeedReference,
                InBank = false,
                Visibility = request.Visibility,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true,
            };

            await _questionRepository.AddAsync(question, cancellationToken);

            var checkpoints = request.MeaningCheckpoints.Select(c => new MeaningCheckpoint
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                CheckpointText = c.CheckpointText,
                CheckpointType = c.CheckpointType,
                Importance = c.Importance,
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList();
            await _questionRepository.AddMeaningCheckpointsAsync(checkpoints, cancellationToken);

            var seededErrors = request.SeededErrors.Select(e => new TaskBSeededError
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                PositionStart = e.PositionStart,
                PositionEnd = e.PositionEnd,
                ErrorTaxonomyId = e.ErrorTaxonomyId,
                CorrectReferenceText = e.CorrectReferenceText,
                Note = e.Note,
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList();
            await _questionRepository.AddSeededErrorsAsync(seededErrors, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ImportUserQuestionResult(question.Id, question.TaskType, question.Difficulty, question.Title, question.CreatedAt);
        }
    }
}
