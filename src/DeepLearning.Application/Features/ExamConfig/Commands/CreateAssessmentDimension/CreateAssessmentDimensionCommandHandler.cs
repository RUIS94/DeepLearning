using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension
{
    public class CreateAssessmentDimensionCommandHandler
        : IRequestHandler<CreateAssessmentDimensionCommand, CreateAssessmentDimensionResult>
    {
        private readonly IAssessmentDimensionRepository _dimensionRepository;
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateAssessmentDimensionCommandHandler(
            IAssessmentDimensionRepository dimensionRepository,
            IExamTypeRepository examTypeRepository,
            IUnitOfWork unitOfWork)
        {
            _dimensionRepository = dimensionRepository;
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateAssessmentDimensionResult> Handle(
            CreateAssessmentDimensionCommand request,
            CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            var exists = await _dimensionRepository.ExistsAsync(
                request.ExamTypeId, request.DimensionKey, request.RubricVersion, cancellationToken);
            if (exists)
            {
                throw new ConflictException(
                    $"Assessment dimension '{request.DimensionKey}' at rubric version '{request.RubricVersion}' already exists for this exam type.");
            }

            // Design doc §10.1: "官方修订rubric时新增一版而非覆盖旧版" — but exactly one version of
            // a given dimension_key must ever be effective at a time, or ListByExamTypeAsync would
            // load two rows for the same key and GradeSubmissionCommandHandler's
            // dimensionsByKey = dimensions.ToDictionary(x => x.DimensionKey) would throw. Whatever
            // is still open-ended (EffectiveTo == null) for this dimension_key gets closed out
            // right at the moment the new version starts, keeping the effective windows
            // contiguous and non-overlapping rather than leaving a gap or an overlap.
            var priorOpenVersions = await _dimensionRepository.ListOpenEndedByKeyAsync(
                request.ExamTypeId, request.DimensionKey, cancellationToken);

            foreach (var prior in priorOpenVersions)
            {
                if (prior.EffectiveFrom >= request.EffectiveFrom)
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(
                            nameof(CreateAssessmentDimensionCommand.EffectiveFrom),
                            $"A new version of '{request.DimensionKey}' must take effect after the currently-effective version " +
                            $"(rubric_version '{prior.RubricVersion}', effective from {prior.EffectiveFrom:O})."),
                    });
                }

                prior.EffectiveTo = request.EffectiveFrom;
            }

            var dimension = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = request.ExamTypeId,
                DimensionKey = request.DimensionKey,
                DimensionName = request.DimensionName,
                ScaleType = request.ScaleType,
                PassThreshold = request.PassThreshold,
                ApplicableTaskType = request.ApplicableTaskType,
                LevelDescriptions = request.LevelDescriptions,
                RubricVersion = request.RubricVersion,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                SourceReference = request.SourceReference,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _dimensionRepository.AddAsync(dimension, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateAssessmentDimensionResult(
                dimension.Id, dimension.ExamTypeId, dimension.DimensionKey, dimension.RubricVersion, dimension.EffectiveFrom);
        }
    }
}
