namespace DeepLearning.Domain.Common
{
    public abstract class AggregateRoot : Entity
    {
        private readonly List<object> _domainEvents = [];

        public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
