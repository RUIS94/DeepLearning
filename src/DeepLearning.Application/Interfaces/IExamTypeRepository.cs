using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IExamTypeRepository
    {
        Task<ExamType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<ExamType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<List<ExamType>> ListAsync(bool? isActive, CancellationToken cancellationToken = default);

        Task AddAsync(ExamType examType, CancellationToken cancellationToken = default);
    }
}
