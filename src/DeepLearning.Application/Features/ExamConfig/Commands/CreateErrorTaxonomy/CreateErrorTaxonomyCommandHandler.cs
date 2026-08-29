using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy
{
    public class CreateErrorTaxonomyCommandHandler : IRequestHandler<CreateErrorTaxonomyCommand, CreateErrorTaxonomyResult>
    {
        private readonly IErrorTaxonomyRepository _taxonomyRepository;
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateErrorTaxonomyCommandHandler(
            IErrorTaxonomyRepository taxonomyRepository,
            IExamTypeRepository examTypeRepository,
            IUnitOfWork unitOfWork)
        {
            _taxonomyRepository = taxonomyRepository;
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateErrorTaxonomyResult> Handle(CreateErrorTaxonomyCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            var exists = await _taxonomyRepository.ExistsAsync(request.ExamTypeId, request.CategoryKey, cancellationToken);
            if (exists)
            {
                throw new ConflictException($"Error category '{request.CategoryKey}' already exists for this exam type.");
            }

            var taxonomy = new ErrorTaxonomy
            {
                Id = Guid.NewGuid(),
                ExamTypeId = request.ExamTypeId,
                CategoryKey = request.CategoryKey,
                CategoryName = request.CategoryName,
                Description = request.Description,
                ExampleCases = request.ExampleCases,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _taxonomyRepository.AddAsync(taxonomy, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateErrorTaxonomyResult(taxonomy.Id, taxonomy.ExamTypeId, taxonomy.CategoryKey, taxonomy.CategoryName);
        }
    }
}
