using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
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
