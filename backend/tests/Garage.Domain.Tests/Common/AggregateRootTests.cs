using Garage.Domain.Common;

namespace Garage.Domain.Tests.Common;

public class AggregateRootTests
{
    private sealed record SomethingHappened : IDomainEvent;

    private sealed class SampleAggregate : AggregateRoot
    {
        public SampleAggregate() => Id = Guid.CreateVersion7();

        public void Happen(IDomainEvent e) => Raise(e);
    }

    [Fact]
    public void New_aggregate_has_id_and_no_events()
    {
        var aggregate = new SampleAggregate();

        Assert.NotEqual(Guid.Empty, aggregate.Id);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Raise_records_events_in_order()
    {
        var aggregate = new SampleAggregate();
        var first = new SomethingHappened();
        var second = new SomethingHappened();

        aggregate.Happen(first);
        aggregate.Happen(second);

        Assert.Equal(2, aggregate.DomainEvents.Count);
        Assert.Same(first, aggregate.DomainEvents[0]);
        Assert.Same(second, aggregate.DomainEvents[1]);
    }

    [Fact]
    public void ClearDomainEvents_removes_all_events()
    {
        var aggregate = new SampleAggregate();
        aggregate.Happen(new SomethingHappened());

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }
}
