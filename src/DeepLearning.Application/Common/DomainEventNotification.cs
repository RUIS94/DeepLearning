using MediatR;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Wraps a plain Domain event (Domain has no MediatR dependency, by design — see AGENTS.md's
    /// dependency-direction rules) into a MediatR INotification so UnitOfWork.SaveChangesAsync
    /// can publish it generically via reflection (MakeGenericType) without knowing the concrete
    /// event type at compile time. A handler subscribes to DomainEventNotification&lt;TEvent&gt;
    /// the same way it would to any other MediatR notification.
    /// </summary>
    public class DomainEventNotification<TDomainEvent> : INotification
        where TDomainEvent : notnull
    {
        public DomainEventNotification(TDomainEvent domainEvent)
        {
            DomainEvent = domainEvent;
        }

        public TDomainEvent DomainEvent { get; }
    }
}
