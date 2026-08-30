using DeepLearning.Domain.Common;

namespace DeepLearning.UnitTests.Domain
{
    public class AggregateRootDomainEventsTests
    {
        private class TestAggregate : AggregateRoot
        {
        }

        private record TestEvent(string Payload);

        [Fact]
        public void AddDomainEvent_appends_to_DomainEvents_in_order()
        {
            var aggregate = new TestAggregate();

            aggregate.AddDomainEvent(new TestEvent("first"));
            aggregate.AddDomainEvent(new TestEvent("second"));

            Assert.Equal(2, aggregate.DomainEvents.Count);
            Assert.Equal("first", ((TestEvent)aggregate.DomainEvents.ElementAt(0)).Payload);
            Assert.Equal("second", ((TestEvent)aggregate.DomainEvents.ElementAt(1)).Payload);
        }

        [Fact]
        public void ClearDomainEvents_empties_the_collection()
        {
            var aggregate = new TestAggregate();
            aggregate.AddDomainEvent(new TestEvent("first"));

            aggregate.ClearDomainEvents();

            Assert.Empty(aggregate.DomainEvents);
        }
    }
}
