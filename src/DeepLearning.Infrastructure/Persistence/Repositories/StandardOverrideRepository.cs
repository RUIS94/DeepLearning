using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class StandardOverrideRepository : IStandardOverrideRepository
    {
        private readonly AppDbContext _context;

        public StandardOverrideRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<StandardOverride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.StandardOverrides.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<StandardOverride?> GetActiveByRuleAsync(OverrideScope scope, string dimensionOrRule, CancellationToken cancellationToken = default)
            => _context.StandardOverrides.FirstOrDefaultAsync(
                x => x.Scope == scope && x.DimensionOrRule == dimensionOrRule && x.Status == OverrideStatus.active,
                cancellationToken);

        public async Task<int> CountDistinctQuestionsPendingAsync(
            OverrideScope scope,
            string dimensionOrRule,
            Guid? baselineOverrideId,
            CancellationToken cancellationToken = default)
        {
            // Two independent confirmation sources, unioned by distinct Question id: the legacy
            // single-shot FollowUpQuestion path (TriggeredByFollowupId — historical rows only,
            // CreateFollowUpQuestionCommand no longer exists) and the current FollowUpThread path
            // (TriggeredByFollowUpThreadId — CloseFollowUpThreadCommandHandler). Without the
            // union, the activation threshold would silently forget every confirmation recorded
            // before the thread model existed.
            var fromLegacyFollowUps =
                from o in _context.StandardOverrides
                join f in _context.FollowUpQuestions on o.TriggeredByFollowupId equals f.Id
                join s in _context.Submissions on f.SubmissionId equals s.Id
                where o.Scope == scope
                    && o.DimensionOrRule == dimensionOrRule
                    && o.Status == OverrideStatus.observing
                    && o.PreviousOverrideId == baselineOverrideId
                select s.QuestionId;

            var fromThreads =
                from o in _context.StandardOverrides
                join t in _context.FollowUpThreads on o.TriggeredByFollowUpThreadId equals t.Id
                join s in _context.Submissions on t.SubmissionId equals s.Id
                where o.Scope == scope
                    && o.DimensionOrRule == dimensionOrRule
                    && o.Status == OverrideStatus.observing
                    && o.PreviousOverrideId == baselineOverrideId
                select s.QuestionId;

            return await fromLegacyFollowUps.Union(fromThreads).Distinct().CountAsync(cancellationToken);
        }

        public Task<List<StandardOverride>> ListAsync(OverrideStatus? status, CancellationToken cancellationToken = default)
        {
            var query = _context.StandardOverrides.AsQueryable();
            if (status is { } s)
            {
                query = query.Where(x => x.Status == s);
            }

            return query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        }

        public async Task AddAsync(StandardOverride standardOverride, CancellationToken cancellationToken = default)
            => await _context.StandardOverrides.AddAsync(standardOverride, cancellationToken);
    }
}
