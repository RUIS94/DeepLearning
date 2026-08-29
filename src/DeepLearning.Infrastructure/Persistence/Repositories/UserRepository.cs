using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => _context.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        public Task<bool> ExistsAsync(string username, string email, CancellationToken cancellationToken = default)
            => _context.Users.AnyAsync(x => x.Username == username || x.Email == email, cancellationToken);

        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
            => await _context.Users.AddAsync(user, cancellationToken);
    }
}
