using System;
using System.Collections.Generic;
using Marten.Events.Dcb;
using Zooper.Lion.Domain.Entities;
using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests.Samples;

[EventSourcedAggregate]
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }

    public List<string> Items { get; } = [];

    public bool IsCancelled { get; private set; }

    public static Order Create(IOrderCreated domainEvent) => new();

    public void Apply(IItemAdded domainEvent)
    {
        Items.Add(domainEvent.ItemName);
    }

    public void Apply(IOrderCancelled domainEvent)
    {
        IsCancelled = true;
    }
}

public sealed record Invoice : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public interface IOrderCreated : IDomainEvent<Order>
{
    Guid OrderId { get; }

    public sealed record V1(Guid OrderId) : IOrderCreated;
}

public interface IItemAdded : IDomainEvent<Order>
{
    string ItemName { get; }

    public sealed record V1(string ItemName) : IItemAdded;
}

public interface IOrderCancelled : IDomainEvent<Order>
{
    public sealed record V1() : IOrderCancelled;
}

public interface IInvoicePaid : IDomainEvent<Invoice>
{
    Guid InvoiceId { get; }

    public sealed record V1(Guid InvoiceId) : IInvoicePaid;
}

[ApprovedAppendWrapper]
public static class OrderStreamExtensions
{
    public static void AppendOrderEvent<TEvent>(this IEventBoundary<Order> eventBoundary, TEvent domainEvent)
        where TEvent : IDomainEvent<Order>
    {
        eventBoundary.AppendOne(domainEvent);
    }
}