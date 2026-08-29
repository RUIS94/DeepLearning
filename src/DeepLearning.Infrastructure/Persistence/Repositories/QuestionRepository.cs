using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        private readonly AppDbContext _context;

        public QuestionRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Question?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Questions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<Question>> ListAsync(
            TaskType? taskType,
            Difficulty? difficulty,
            bool? inBank,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Questions.Where(x => x.IsActive);

            if (taskType.HasValue)
            {
                query = query.Where(x => x.TaskType == taskType.Value);
            }

            if (difficulty.HasValue)
            {
                query = query.Where(x => x.Difficulty == difficulty.Value);
            }

            if (inBank.HasValue)
            {
                query = query.Where(x => x.InBank == inBank.Value);
            }

            return query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        }

        public Task<List<MeaningCheckpoint>> GetMeaningCheckpointsAsync(Guid questionId, CancellationToken cancellationToken = default)
            => _context.MeaningCheckpoints.Where(x => x.QuestionId == questionId).ToListAsync(cancellationToken);

        public Task<List<TaskBSeededError>> GetSeededErrorsAsync(Guid questionId, CancellationToken cancellationToken = default)
            => _context.TaskBSeededErrors
                .Where(x => x.QuestionId == questionId)
                .Include(x => x.ErrorTaxonomy)
                .OrderBy(x => x.PositionStart)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Question question, CancellationToken cancellationToken = default)
            => await _context.Questions.AddAsync(question, cancellationToken);

        public async Task AddMeaningCheckpointsAsync(IEnumerable<MeaningCheckpoint> checkpoints, CancellationToken cancellationToken = default)
            => await _context.MeaningCheckpoints.AddRangeAsync(checkpoints, cancellationToken);

        public async Task AddSeededErrorsAsync(IEnumerable<TaskBSeededError> seededErrors, CancellationToken cancellationToken = default)
            => await _context.TaskBSeededErrors.AddRangeAsync(seededErrors, cancellationToken);
    }
}
