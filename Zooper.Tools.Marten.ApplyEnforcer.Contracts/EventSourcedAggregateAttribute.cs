using System;

namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventSourcedAggregateAttribute : Attribute
{
}
