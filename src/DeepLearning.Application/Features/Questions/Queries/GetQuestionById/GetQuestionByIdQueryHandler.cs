using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetQuestionById
{
    public class GetQuestionByIdQueryHandler : IRequestHandler<GetQuestionByIdQuery, GetQuestionByIdResult>
    {
        private readonly IQuestionRepository _questionRepository;

        public GetQuestionByIdQueryHandler(IQuestionRepository questionRepository)
        {
            _questionRepository = questionRepository;
        }

        public async Task<GetQuestionByIdResult> Handle(GetQuestionByIdQuery request, CancellationToken cancellationToken)
        {
            var question = await _questionRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.Question), request.Id);

            var checkpoints = await _questionRepository.GetMeaningCheckpointsAsync(question.Id, cancellationToken);
            var categoryIds = await _questionRepository.ListCategoryIdsAsync(question.Id, cancellationToken);
            var checkpointItems = checkpoints
                .Select(c => new MeaningCheckpointItem(c.Id, c.CheckpointText, c.CheckpointType, c.Importance))
                .ToList();

            TaskBDetails? taskBDetails = null;
            if (question.TaskType == TaskType.B)
            {
                var seededErrors = await _questionRepository.GetSeededErrorsAsync(question.Id, cancellationToken);
                taskBDetails = new TaskBDetails(
                    question.FlawedTranslationText ?? string.Empty,
                    seededErrors.Select(e => new SeededErrorItem(
                        e.Id,
                        e.PositionStart,
                        e.PositionEnd,
                        e.ErrorTaxonomyId,
                        e.ErrorTaxonomy?.CategoryKey ?? string.Empty,
                        e.CorrectReferenceText,
                        e.Note)).ToList());
            }

            return new GetQuestionByIdResult(
                question.Id,
                question.TaskType,
                question.Difficulty,
                question.Title,
                question.Brief,
                question.BriefDomain,
                question.BriefTextType,
                question.BriefPurpose,
                question.BriefAudience,
                question.SourceText,
                question.WordCount,
                question.Origin,
                question.SourceType,
                question.IsSeedReference,
                question.InBank,
                question.Visibility,
                question.CreatedBy,
                question.CreatedAt,
                question.IsActive,
                checkpointItems,
                taskBDetails,
                categoryIds);
        }
    }
}
