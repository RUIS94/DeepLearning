using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById
{
    public class GetExamTypeByIdQueryHandler : IRequestHandler<GetExamTypeByIdQuery, GetExamTypeByIdResult>
    {
        private readonly IExamTypeRepository _examTypeRepository;

        public GetExamTypeByIdQueryHandler(IExamTypeRepository examTypeRepository)
        {
            _examTypeRepository = examTypeRepository;
        }

        public async Task<GetExamTypeByIdResult> Handle(GetExamTypeByIdQuery request, CancellationToken cancellationToken)
        {
            var examType = await _examTypeRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.ExamType), request.Id);

            return new GetExamTypeByIdResult(
                examType.Id,
                examType.Code,
                examType.Name,
                examType.SubjectCategory,
                examType.SourceLanguage,
                examType.TargetLanguage,
                examType.GradeLevel,
                examType.Description,
                examType.IsActive,
                examType.CreatedAt);
        }
    }
}
