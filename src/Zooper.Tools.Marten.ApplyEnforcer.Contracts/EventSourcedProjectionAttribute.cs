using System;

namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventSourcedProjectionAttribute : Attribute
{
    public EventSourcedProjectionAttribute(Type aggregateType)
    {
        AggregateType = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
    }

    public Type AggregateType { get; }
}