using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly IPublisher _publisher;

        public UnitOfWork(AppDbContext context, IPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        /// <summary>
        /// Domain events (design doc §5's "BE4 evaluation completion publishes an event, BE7/BE8/BE12
        /// each subscribe independently" — see AggregateRoot.DomainEvents) are dispatched AFTER a
        /// successful save, not before: events are collected and cleared from tracked aggregates
        /// first so a failed SaveChangesAsync never publishes anything for a transaction that
        /// didn't commit, then published once the save has actually succeeded. A handler that
        /// itself calls SaveChangesAsync to persist its own new aggregate (e.g. a WeakPoint
        /// raising WeakPointRecurredEvent) goes through this same method, so its own events are
        /// collected and dispatched the same way — no special-casing needed for nested saves.
        /// </summary>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var aggregatesWithEvents = _context.ChangeTracker.Entries<AggregateRoot>()
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.DomainEvents.Count > 0)
                .ToList();

            var domainEvents = aggregatesWithEvents.SelectMany(aggregate => aggregate.DomainEvents).ToList();
            foreach (var aggregate in aggregatesWithEvents)
            {
                aggregate.ClearDomainEvents();
            }

            var result = await _context.SaveChangesAsync(cancellationToken);

            foreach (var domainEvent in domainEvents)
            {
                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
                var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
                await _publisher.Publish(notification, cancellationToken);
            }

            return result;
        }
    }
}
