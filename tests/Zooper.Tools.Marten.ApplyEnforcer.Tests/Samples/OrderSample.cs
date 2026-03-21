using Marten.Events.Dcb;
using Zooper.Lion.Domain.Entities;
using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests.Samples;

public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed class Invoice : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;

public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;

public sealed record OrderCancelled() : IDomainEvent<Order>;

public sealed record InvoicePaid(Guid InvoiceId) : IDomainEvent<Invoice>;

[EventSourcedProjection(typeof(Order))]
public sealed class OrderProjection :
    ICreate<OrderCreated, OrderProjection>,
    IApply<ItemAdded>,
    IApply<OrderCancelled>
{
    public Guid Id { get; private set; }

    public List<string> Items { get; } = new();

    public bool IsCancelled { get; private set; }

    public static OrderProjection Create(OrderCreated domainEvent)
    {
        return new OrderProjection
        {
            Id = domainEvent.OrderId,
        };
    }

    public void Apply(ItemAdded domainEvent)
    {
        Items.Add(domainEvent.ItemName);
    }

    public void Apply(OrderCancelled domainEvent)
    {
        IsCancelled = true;
    }
}

[ApprovedAppendWrapper]
public static class OrderStreamExtensions
{
    public static void AppendOrderEvent<TEvent>(this IEventBoundary<OrderProjection> eventBoundary, TEvent domainEvent)
        where TEvent : IDomainEvent<Order>
    {
        eventBoundary.AppendOne(domainEvent);
    }
}