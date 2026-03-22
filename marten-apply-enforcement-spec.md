# Marten Event-Sourcing Apply Enforcement Specification

## Purpose

Define a strict, compile-time enforced approach for handling Marten aggregate state evolution so that:

- every domain event belonging to an aggregate stream must have a matching enforced Marten convention handler such as `Create`, `Apply`, or another approved convention method
- no developer has to maintain a manual annotation list of events
- no developer can forget to handle a new event without the build failing
- Marten-specific state rebuilding logic stays outside the pure domain aggregate
- Marten convention methods are used only for rebuilding, evolving, or deleting projection state and never for creating new domain events

This document is intended as an implementation brief for an AI coding agent.

---

## Problem Statement

Marten supports conventional `Create(...)`, `Apply(...)`, and other lifecycle methods for rebuilding aggregate state from event streams. The problem is that this is convention-based, so forgetting to add the correct handler for a newly introduced event is easy.

A manual attribute such as `[AggregateEvents(...)]` does not solve the problem, because developers can forget to update the attribute list as well.

We need a solution where:

1. the source of truth for which events belong to an aggregate is discovered automatically
2. the compiler fails when a new event is introduced without a matching Marten convention handler
3. raw Marten event appends cannot silently bypass the rule

---

## Core Design Decisions

### 1. Marten convention methods do not create or record domain events

Marten convention methods such as `Create`, `Apply`, and `ShouldDelete` are for rebuilding, evolving, or deleting projection state from events that already exist.

These methods must never:

- produce new domain events
- append to Marten
- publish to MassTransit
- trigger side effects

`Create` constructs projection state from an existing event.

`Apply` mutates existing projection state.

Other convention methods only perform the projection-lifecycle behavior implied by their Marten contract.

### 2. Use cases produce domain events

The use case or command handler is responsible for:

- loading the current stream state
- validating the command against current state
- creating the new domain event
- appending the new domain event to the Marten stream
- saving changes

This means we do **not** use an `UncommittedEvents` list inside the aggregate when the use case is the producer.

### 3. The pure domain aggregate should stay Marten-agnostic

We do **not** want Marten-specific `Apply` plumbing inside the domain aggregate if we can avoid it.

Instead, state rebuilding should live in a dedicated projection / state class that Marten uses.

### 4. Event membership must be automatic, not manually listed

We do **not** want a manually maintained list of events.

The codebase must have an automatic way to determine which events belong to a given aggregate stream.

If the solution uses `IDomainEvent` and `IAggregateRoot` from `Zooper.Lion`, those interfaces should be treated as first-class domain concepts and may participate in that discovery mechanism.

Example:

```csharp
public sealed class Order : IAggregateRoot;

public sealed record OrderCreated(Guid OrderId) : IDomainEvent;
public sealed record ItemAdded(string ItemName) : IDomainEvent;
public sealed record OrderCancelled() : IDomainEvent;
```

Those events belong to the Order stream only if they match the configured aggregate event-discovery rule for `Order`.

That rule may be based on `IDomainEvent` plus `IAggregateRoot` conventions or on another automatic aggregate-membership convention used consistently by the codebase.

### 5. Compile-time enforcement must cover the Marten convention surface we use

There is no known ready-made NuGet package that gives strict compile-time Marten convention coverage enforcement exactly the way we need it.

We will implement our own internal solution using:

- a Roslyn source generator
- a Roslyn analyzer
- a typed Marten append wrapper

---

## Target Architecture

### High-Level Flow

1. A use case loads the current Marten stream state.
2. The use case validates the command using the current state.
3. The use case creates a new domain event.
4. The use case appends the event through a typed wrapper method.
5. Marten later replays the event stream into the projection / state class.
6. The type that defines the Marten convention methods handles each event through the appropriate handler such as `Create`, `Apply`, or another approved convention method.

---

## Separation of Responsibilities

### Domain Aggregate

The domain aggregate should remain focused on:

- business invariants
- business rules
- decision support
- non-persistence-specific behavior

It should not contain Marten replay plumbing if we want to keep it pure.

### Projection / Convention Host Type

The Marten projection state and the type that hosts the Marten convention methods are the enforcement targets.

In some cases they are the same type. In other cases, the convention methods may live on:

- the projection / state type itself
- a `SingleStreamProjection` subclass
- a partial class whose methods are split across multiple files

The relevant host type is whichever type actually defines the Marten convention methods.

That host is responsible for:

- being rebuilt from events
- containing the Marten convention methods used to rebuild that state, such as `Create`, `Apply`, and any approved deletion handlers
- representing current event-sourced state for the stream

This is the type against which the compile-time enforcement will be applied.

### Use Case / Command Handler

The use case is responsible for:

- loading the current projection / state
- checking invariants through explicit methods
- creating domain events
- appending domain events to Marten through a safe typed API
- saving changes

---

## Enforcement Strategy

## 1. Define an automatic aggregate event-discovery rule per stream

Each aggregate stream must have an automatic way to discover all concrete events that belong to that aggregate.

This discovery rule must not depend on a manual per-event list.

Acceptable discovery models include:

- a `Zooper.Lion`-based convention using `IDomainEvent`, `IAggregateRoot`, and aggregate-boundary discovery rules
- another automatic convention that unambiguously maps concrete events to one aggregate stream

Example:

```csharp
public sealed class Order : IAggregateRoot;

public sealed record OrderCreated(Guid OrderId) : IDomainEvent;
public sealed record ItemAdded(string ItemName) : IDomainEvent;
public sealed record OrderCancelled() : IDomainEvent;
```

The exact discovery rule is a design choice, but it must be automatic, compile-time analyzable, and consistent across the codebase.

This spec does not use or require an aggregate-specific base event type.

---

## 2. Create explicit contracts for Marten convention handlers

```csharp
public interface IApply<in TEvent>
{
    void Apply(TEvent domainEvent);
}
```

`IApply<TEvent>` provides compile-time enforceable method signatures for instance-based state evolution.

If the codebase wants direct compile-time enforcement for `Create`, we should also add a static-abstract contract for it:

```csharp
public interface ICreate<TEvent, TProjection>
    where TProjection : ICreate<TEvent, TProjection>
{
    static abstract TProjection Create(TEvent domainEvent);
}
```

Other Marten convention methods used by the codebase should either get analogous contracts or be validated by analyzer rules against the discovered aggregate event set.

These contracts are useful, but the enforcement design must not depend on the host type being `partial`.

---

## 3. Convention methods may live on the projection type, a `SingleStreamProjection`, or a partial class

Example:

```csharp
public class OrderProjection
{
    private bool _isCancelled;
    private readonly List<string> _itemNames = [];

    public static OrderProjection Create(OrderCreated domainEvent)
    {
        return new OrderProjection();
    }

    public void Apply(ItemAdded domainEvent)
    {
        _itemNames.Add(domainEvent.ItemName);
    }

    public void Apply(OrderCancelled domainEvent)
    {
        _isCancelled = true;
    }
}
```

The analyzer must inspect whichever type defines the Marten convention methods.

If those methods are defined on a `SingleStreamProjection` subclass, that subclass is the enforcement target.

If those methods are defined across multiple partial declarations, the combined type is the enforcement target.

Whether a type is declared `partial` is an implementation detail. We do not require it, and we do not enforce it.

---

## 4. Source generator automatically discovers all events in the family

For each Marten convention host type, the generator must:

1. determine which aggregate belongs to that host
2. scan the compilation for all non-abstract event types that belong to that aggregate according to the configured discovery rule
3. emit generated coverage metadata for every discovered event so the analyzer can validate the required Marten convention methods

Example generated output:

```csharp
internal static class OrderProjectionEventCoverage
{
    public static readonly Type[] Events =
    [
        typeof(OrderCreated),
        typeof(ItemAdded),
        typeof(OrderCancelled)
    ];
}
```

If a developer adds:

```csharp
public sealed record OrderArchived() : IDomainEvent;
```

the generated file must automatically become:

```csharp
internal static class OrderProjectionEventCoverage
{
    public static readonly Type[] Events =
    [
        typeof(OrderCreated),
        typeof(ItemAdded),
        typeof(OrderCancelled),
        typeof(OrderArchived)
    ];
}
```

The analyzer must also validate events that are handled through `Create(...)` or any other approved Marten convention method instead of `Apply(...)`.

If `Apply(OrderArchived domainEvent)` or another valid handler is missing, the build must fail.

Generated metadata provides the discovered event set. Analyzer validation enforces that coverage set against `Create(...)`, `Apply(...)`, and the rest of the approved Marten convention surface.

---

## 5. Analyzer must validate convention-method coverage

For each concrete event discovered for the aggregate, the analyzer must inspect the Marten convention host type and determine whether the event is handled by at least one approved Marten convention method.

At minimum, the approved handler set must include:

- `public static TProjection Create(TEvent domainEvent)`
- `public void Apply(TEvent domainEvent)`

If the codebase uses other Marten conventions such as `ShouldDelete(TEvent domainEvent)` or equivalent deletion handlers, those must also be part of the approved set and validated the same way.

This rule is necessary because some Marten convention handlers, especially static `Create(...)`, are not as naturally enforced through normal instance-interface implementation alone.

If the codebase uses `IAggregateRoot` from `Zooper.Lion`, the analyzer may use that type relationship as part of aggregate-boundary discovery and validation.

---

## 6. Analyzer must forbid raw Marten append calls

Even with the above enforcement, a developer could bypass the pattern by directly calling Marten with raw event objects.

We must prevent this.

The analyzer must report diagnostics when code outside the designated infrastructure wrapper directly calls methods such as:

- `AppendOne(...)`
- `AppendMany(...)`
- other raw event append APIs, depending on what is used in the codebase

The intended rule is:

- application code and domain code must use typed wrapper methods only
- direct Marten append calls are forbidden outside the infrastructure layer or wrapper location

---

## 7. Provide a typed append wrapper per aggregate stream

Example:

```csharp
public static class MartenOrderStreamExtensions
{
    public static void AppendOrderEvent<TOrderEvent>(
        this IEventStream<OrderProjection> eventStream,
        TOrderEvent domainEvent)
        where TOrderEvent : IDomainEvent
    {
        eventStream.AppendOne(domainEvent);
    }
}
```

If the codebase uses `IDomainEvent` instead, the analyzer must validate that only events belonging to the `Order` aggregate according to the configured discovery rule can be appended through the official path.

This closes another gap.

---

## Projection Registration Strategy

We want Marten projection logic to live in a dedicated projection / state type, not in the pure domain aggregate.

There are two acceptable implementation patterns:

### Option A: Dedicated projection / state class with conventional Marten handler methods

Example:

```csharp
public class OrderProjection
{
    public Guid Id { get; private set; }

    public static OrderProjection Create(OrderCreated domainEvent)
    {
        return new OrderProjection
        {
            Id = domainEvent.OrderId
        };
    }

    public void Apply(ItemAdded domainEvent)
    {
    }
}
```

### Option B: Dedicated Marten projection class delegating into a separate state type

If necessary, a projection class may delegate into a state type, but the preferred setup is to keep the enforceable `Apply` surface on the projection / state class itself to make compile-time enforcement simpler.

If the codebase uses `SingleStreamProjection` and defines `Create`, `Apply`, `ShouldDelete`, or similar methods there, that `SingleStreamProjection` definition is the thing the analyzer must inspect.

Preferred direction: **Option A**.

---

## Mapping Between Aggregate And Convention Host Type

The source generator must know which Marten convention host type belongs to which aggregate.

There are two viable approaches.

### Preferred Approach

Define a single marker interface or marker attribute on the type that hosts the Marten convention methods and name the aggregate discovery key there.

Example:

```csharp
[EventSourcedProjection(typeof(Order))]
public class OrderProjection
{
}
```

The same idea also works if the annotated type is a `SingleStreamProjection` subclass rather than the projection document type itself.

If the host type or related domain aggregate implements `IAggregateRoot`, the marker may point to that aggregate root type directly.

This is acceptable because it is **not** a manually maintained event list. It only points to the aggregate or other discovery anchor from which concrete events are found automatically.

Adding new events does not require updating this attribute.

### Why this is acceptable

The problem with the earlier attribute idea was that it listed every individual event type manually. That is fragile.

A single aggregate reference is not fragile in the same way because concrete events are discovered automatically from the configured aggregate-discovery rule.

---

## Implementation Rules

### Rule 1

Every event stream must have one automatic, compile-time analyzable discovery rule that determines which concrete events belong to that aggregate.

### Rule 2

The source generator must discover every concrete event for the aggregate and emit the coverage metadata needed to validate the approved Marten convention methods for that aggregate.

### Rule 3

The build must fail when any required Marten convention handler is missing.

### Rule 4

Marten convention methods must only build, mutate, or delete projection state.

They must not:

- create new domain events
- call repositories
- call Marten APIs
- publish messages
- call external services
- perform input validation side effects

### Rule 5

Use cases create and append domain events.

### Rule 6

Raw Marten append calls are forbidden outside the approved wrapper layer.

### Rule 7

No manual per-event registration lists are allowed.

### Rule 8

Each concrete event discovered for the aggregate must be covered by an approved Marten convention method on the Marten convention host type, such as `Create`, `Apply`, or another explicitly supported handler.

---

## Example End State

### Aggregate And Events

```csharp
public sealed class Order : IAggregateRoot;

public sealed record OrderCreated(Guid OrderId) : IDomainEvent;
public sealed record ItemAdded(string ItemName) : IDomainEvent;
public sealed record OrderCancelled() : IDomainEvent;
```

### Projection / State

```csharp
[EventSourcedProjection(typeof(Order))]
public class OrderProjection
{
    private readonly List<string> _itemNames = [];
    private bool _isCancelled;

    public static OrderProjection Create(OrderCreated domainEvent)
    {
        return new OrderProjection();
    }

    public void Apply(ItemAdded domainEvent)
    {
        _itemNames.Add(domainEvent.ItemName);
    }

    public void Apply(OrderCancelled domainEvent)
    {
        _isCancelled = true;
    }
}
```

The analyzer also validates that `OrderCreated` is covered by `Create(OrderCreated domainEvent)` and that any future event is covered by at least one approved Marten convention method on the relevant host type.

### Generated Coverage Metadata

```csharp
internal static class OrderProjectionEventCoverage
{
    public static readonly Type[] Events =
    [
        typeof(OrderCreated),
        typeof(ItemAdded),
        typeof(OrderCancelled)
    ];
}
```

### Typed Append Wrapper

```csharp
public static class MartenOrderStreamExtensions
{
    public static void AppendOrderEvent<TOrderEvent>(
        this IEventStream<OrderProjection> eventStream,
        TOrderEvent domainEvent)
        where TOrderEvent : IDomainEvent
    {
        eventStream.AppendOne(domainEvent);
    }
}
```

### Use Case Example

```csharp
public sealed class AddItemToOrderUseCase
{
    private readonly IDocumentSession _documentSession;

    public AddItemToOrderUseCase(IDocumentSession documentSession)
    {
        _documentSession = documentSession;
    }

    public async Task ExecuteAsync(Guid orderId, string itemName, CancellationToken cancellationToken)
    {
        var eventStream = await _documentSession.Events.FetchForWriting<OrderProjection>(orderId, cancellationToken);
        var orderProjection = eventStream.Aggregate;

        if (orderProjection is null)
        {
            throw new InvalidOperationException("Order not found.");
        }

        // domain checks would happen through explicit methods on the projection or a pure domain model

        var domainEvent = new ItemAdded(itemName);

        eventStream.AppendOrderEvent(domainEvent);

        await _documentSession.SaveChangesAsync(cancellationToken);
    }
}
```

---

## Required Components To Implement

### 1. Contracts Project

Implement:

- `IApply<TEvent>`
- `ICreate<TEvent, TProjection>` if static interface enforcement is used for creation handlers
- `EventSourcedProjectionAttribute` or equivalent marker
- references or adapters for `IDomainEvent` and `IAggregateRoot` from `Zooper.Lion` if the solution chooses to integrate with them

### 2. Source Generator Project

Responsibilities:

- find all types marked as event-sourced convention hosts
- read the aggregate discovery key from the marker
- discover all non-abstract aggregate events in the compilation according to the configured discovery rule
- generate the coverage metadata needed to enforce the approved Marten convention methods for every concrete event type

### 3. Analyzer Project

Responsibilities:

- validate that every concrete event discovered for the aggregate is covered by an approved Marten convention method such as `Create`, `Apply`, or another explicitly supported handler on the relevant host type
- report diagnostics when raw Marten append methods are used outside approved wrapper code
- optionally report diagnostics when an event does not belong to the proper aggregate according to the configured discovery rule but is used in a stream wrapper

### 4. Infrastructure Wrapper Project

Implement:

- typed append extension methods per aggregate stream
- optionally typed fetch helpers per projection family

---

## Acceptance Criteria

The solution is complete only when all of the following are true.

### Compile-Time Enforcement

- adding a new event discovered for the `Order` aggregate automatically causes the build to require a matching approved Marten convention handler on the relevant projection host
- if the required `Create`, `Apply`, or other approved convention method is missing, the build fails
- developers do not have to manually update an event list anywhere
- if `IDomainEvent` and `IAggregateRoot` are used, they may participate in aggregate event discovery rather than being treated as unrelated optional markers

### Safe Appending

- use cases append events only through typed wrappers
- direct raw `AppendOne` or equivalent Marten calls outside the approved wrapper code produce analyzer diagnostics

### Architectural Purity

- `Create`, `Apply`, and other convention methods only rebuild, mutate, or delete projection state
- use cases create and append domain events
- the domain aggregate is not forced to contain Marten-specific replay plumbing

### Developer Experience

- error messages clearly state which event is missing which Marten convention handler
- adding a new event naturally produces a compiler failure until handling is implemented

---

## Non-Goals

This design does **not** aim to:

- auto-generate the contents of `Apply` methods
- infer business rules automatically
- replace domain modeling
- replace Marten projections entirely
- publish integration events automatically

This is strictly about compile-time enforcement of event application coverage and preventing bypasses.

---

## Suggested Diagnostic Messages

Examples of analyzer or compiler-facing messages that should exist:

- `Projection 'OrderProjection' is missing a Marten convention handler for event 'OrderArchived'. Add Apply(OrderArchived domainEvent), Create(OrderArchived domainEvent), or another approved handler.`
- `Projection 'OrderProjection' is missing Apply(OrderArchived domainEvent).`
- `Projection 'OrderProjection' is missing Create(OrderCreated domainEvent).`
- `Direct Marten AppendOne call is forbidden here. Use the typed stream wrapper instead.`
- `Event 'OrderArchived' does not belong to aggregate 'Order' according to the configured discovery rule and cannot be appended to the Order stream.`

---

## Recommended Implementation Order

1. Create `IApply<TEvent>` and `EventSourcedProjectionAttribute`
2. Create one proof-of-concept aggregate such as `Order`
3. Create one proof-of-concept projection such as `OrderProjection`
4. Implement the source generator that emits aggregate-event coverage metadata
5. Implement analyzer validation for `Create(...)` and any other approved Marten convention methods
6. Verify that missing convention handlers fail the build
7. Add typed append wrappers
8. Implement analyzer rules forbidding raw Marten append usage
9. Roll out the pattern across more event families

---

## Final Decision Summary

We will implement the following pattern:

- one automatic aggregate event-discovery rule per stream
- optional or primary integration with `IDomainEvent` and `IAggregateRoot` when using `Zooper.Lion`
- one projection / state type per stream
- enforcement applies to whichever type defines the Marten convention methods, including a projection type, a `SingleStreamProjection` subclass, or a partial type split across files
- one source generator that discovers all concrete aggregate events automatically
- generated aggregate-event coverage metadata plus analyzer validation for `Create`, `Apply`, and other approved Marten convention methods
- use cases create domain events and append them through typed wrappers
- Marten convention methods only rebuild, mutate, or delete projection state
- raw Marten append calls are blocked by analyzer rules

This is the chosen solution because it removes manual event lists, gives real compile-time enforcement across the Marten convention surface we use, and keeps Marten replay concerns out of the pure domain aggregate.
