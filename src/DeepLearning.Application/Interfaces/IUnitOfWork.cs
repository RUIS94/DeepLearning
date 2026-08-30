namespace DeepLearning.Application.Interfaces
{
    public interface IUnitOfWork
    {
        /// <summary>
        /// Throws <see cref="DeepLearning.Domain.Exceptions.ConflictException"/> (not EF Core's
        /// own DbUpdateConcurrencyException, which Application can't reference) if any tracked
        /// entity using an optimistic concurrency token (currently just Submission, via
        /// UseXminAsConcurrencyToken) was changed by someone else since it was loaded.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
