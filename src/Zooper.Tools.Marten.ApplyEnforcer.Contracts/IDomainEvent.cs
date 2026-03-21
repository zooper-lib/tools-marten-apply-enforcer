using Zooper.Lion.Domain.Events;

namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

public interface IDomainEvent<TAggregate> : IDomainEvent
    where TAggregate : class
{
}