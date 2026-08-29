using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetAssessmentDimensionsByExamType
{
    public class GetAssessmentDimensionsByExamTypeQueryHandler
        : IRequestHandler<GetAssessmentDimensionsByExamTypeQuery, List<AssessmentDimensionResultItem>>
    {
        private readonly IAssessmentDimensionRepository _dimensionRepository;

        public GetAssessmentDimensionsByExamTypeQueryHandler(IAssessmentDimensionRepository dimensionRepository)
        {
            _dimensionRepository = dimensionRepository;
        }

        public async Task<List<AssessmentDimensionResultItem>> Handle(
            GetAssessmentDimensionsByExamTypeQuery request,
            CancellationToken cancellationToken)
        {
            var dimensions = await _dimensionRepository.ListByExamTypeAsync(
                request.ExamTypeId, request.ApplicableTaskType, cancellationToken);

            return dimensions.Select(x => new AssessmentDimensionResultItem(
                x.Id,
                x.ExamTypeId,
                x.DimensionKey,
                x.DimensionName,
                x.ScaleType,
                x.PassThreshold,
                x.ApplicableTaskType,
                x.LevelDescriptions,
                x.RubricVersion,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.SourceReference,
                x.VerifiedAt)).ToList();
        }
    }
}
