using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType
{
    public class CreateExamTypeCommandHandler : IRequestHandler<CreateExamTypeCommand, CreateExamTypeResult>
    {
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateExamTypeCommandHandler(IExamTypeRepository examTypeRepository, IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateExamTypeResult> Handle(CreateExamTypeCommand request, CancellationToken cancellationToken)
        {
            var existing = await _examTypeRepository.GetByCodeAsync(request.Code, cancellationToken);
            if (existing is not null)
            {
                throw new ConflictException($"An exam type with code '{request.Code}' already exists.");
            }

            var examType = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = request.Code,
                Name = request.Name,
                SubjectCategory = request.SubjectCategory,
                SourceLanguage = request.SourceLanguage,
                TargetLanguage = request.TargetLanguage,
                GradeLevel = request.GradeLevel,
                Description = request.Description,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _examTypeRepository.AddAsync(examType, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateExamTypeResult(
                examType.Id,
                examType.Code,
                examType.Name,
                examType.SubjectCategory,
                examType.IsActive,
                examType.CreatedAt);
        }
    }
}
