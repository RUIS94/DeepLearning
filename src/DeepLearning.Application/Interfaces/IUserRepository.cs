using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string username, string email, CancellationToken cancellationToken = default);

        Task AddAsync(User user, CancellationToken cancellationToken = default);
    }
}
