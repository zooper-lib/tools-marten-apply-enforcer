using System;

namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventSourcedProjectionAttribute(Type aggregateType) : Attribute
{
    public Type AggregateType { get; } = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
}