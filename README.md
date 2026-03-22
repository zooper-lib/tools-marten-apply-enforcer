# Zooper.Tools.Marten.ApplyEnforcer

Roslyn analyzers and source generators that enforce correct [Marten](https://martendb.io/) event-sourcing patterns at compile time.

## The Problem

Marten rebuilds aggregate state from events using convention-based methods (`Create`, `Apply`, `ShouldDelete`), but there's no compile-time enforcement. When a developer introduces a new domain event and forgets to add the corresponding handler, the projection silently produces incorrect state at runtime. Similarly, raw Marten append calls (`AppendOne`, `AppendMany`) bypass type safety entirely — nothing prevents appending events that don't belong to a given aggregate's stream.

## The Solution

This toolkit provides **zero-runtime-cost, compile-time enforcement** through three components:

| Component | What it does |
|---|---|
| **Source Generator** | Automatically discovers all events for an aggregate via `IDomainEvent<TAggregate>` |
| **Coverage Analyzer** | Fails the build when a projection is missing a handler for a discovered event (MARTEN001) |
| **Raw Append Analyzer** | Fails the build when code directly calls `AppendOne`/`AppendMany` outside an approved wrapper (MARTEN002) |

No manual event lists. No runtime reflection. Just add a new event and the compiler tells you exactly what's missing.

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **MARTEN001** | Error | Projection is missing a Marten convention handler for a discovered domain event |
| **MARTEN002** | Error | Raw Marten append call used outside an approved wrapper class |

## Quick Start

### 1. Install the packages

```xml
<!-- Contracts: interfaces and attributes for your domain code -->
<PackageReference Include="Zooper.Tools.Marten.ApplyEnforcer.Contracts" Version="1.0.0" />

<!-- Analyzers: source generator + analyzers (no runtime dependency) -->
<PackageReference Include="Zooper.Tools.Marten.ApplyEnforcer.Analyzers" Version="1.0.0"
                  PrivateAssets="all"
                  IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
```

### 2. Declare your events with aggregate affinity

```csharp
public sealed class Order : IAggregateRoot<Guid> { }

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;
public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;
public sealed record OrderCancelled() : IDomainEvent<Order>;
```

The `IDomainEvent<TAggregate>` interface is the source of truth — the generator uses it to automatically discover every event belonging to an aggregate.

### 3. Write your projection

```csharp
[EventSourcedProjection(typeof(Order))]
public sealed class OrderProjection :
    ICreate<OrderCreated, OrderProjection>,
    IApply<ItemAdded>,
    IApply<OrderCancelled>
{
    public List<string> Items { get; } = [];
    public bool IsCancelled { get; private set; }

    public static OrderProjection Create(OrderCreated e) => new();
    public void Apply(ItemAdded e) => Items.Add(e.ItemName);
    public void Apply(OrderCancelled e) => IsCancelled = true;
}
```

The `[EventSourcedProjection(typeof(Order))]` attribute is a **one-time declaration** — it never needs updating when new events are added.

If you now introduce a new `IDomainEvent<Order>` without adding a handler to `OrderProjection`, the build fails with **MARTEN001**.

### 4. Create a typed append wrapper

```csharp
[ApprovedAppendWrapper]
public static class OrderStreamExtensions
{
    public static void AppendOrderEvent<TEvent>(
        this IEventBoundary<OrderProjection> stream,
        TEvent @event)
        where TEvent : IDomainEvent<Order>
    {
        stream.AppendOne(@event);
    }
}
```

The `where TEvent : IDomainEvent<Order>` constraint ensures only events that belong to the `Order` aggregate can be appended. The `[ApprovedAppendWrapper]` attribute tells the analyzer that raw Marten calls inside this class are permitted.

### 5. Use the typed wrapper

```csharp
// ✅ Compiles — type-safe and enforced
eventStream.AppendOrderEvent(new ItemAdded("Widget"));

// ❌ MARTEN002 — raw Marten append is forbidden
eventStream.AppendOne(new ItemAdded("Widget"));
```

## Supported Handler Conventions

The coverage analyzer recognizes the following Marten convention methods:

| Method | Signature | Purpose |
|---|---|---|
| `Create` | `public static TProjection Create(TEvent e)` | Initial projection creation from the first event |
| `Apply` | `public void Apply(TEvent e)` | State mutation from subsequent events |
| `ShouldDelete` | `public bool ShouldDelete(TEvent e)` | Deletion criteria for a given event |

## Contracts Reference

| Type | Kind | Purpose |
|---|---|---|
| `IDomainEvent<TAggregate>` | Interface | Declares which aggregate an event belongs to |
| `IApply<TEvent>` | Interface | Instance-based state evolution contract |
| `ICreate<TEvent, TProjection>` | Interface | Static-abstract creation contract |
| `EventSourcedProjectionAttribute` | Attribute | Marks a type as a projection for a specific aggregate |
| `ApprovedAppendWrapperAttribute` | Attribute | Marks a class as an approved wrapper for raw Marten appends |

## How It Works

```
                    ┌──────────────────┐
                    │  IDomainEvent<T> │  ← Events declare aggregate affinity
                    └────────┬─────────┘
                             │
              ┌──────────────▼──────────────┐
              │   Source Generator (build)   │  ← Discovers all events per aggregate
              └──────────────┬──────────────┘
                             │
                   ┌─────────▼─────────┐
                   │  Generated Type[] │  ← Emitted metadata array
                   └─────────┬─────────┘
                             │
              ┌──────────────▼──────────────┐
              │   Coverage Analyzer         │  ← Checks projection has handler
              │   (MARTEN001)               │    for every discovered event
              └─────────────────────────────┘

              ┌─────────────────────────────┐
              │   Raw Append Analyzer       │  ← Flags direct AppendOne/AppendMany
              │   (MARTEN002)               │    calls outside approved wrappers
              └─────────────────────────────┘
```

1. **At build time**, the source generator scans the compilation for all concrete types implementing `IDomainEvent<TAggregate>` and emits a `Type[]` array per aggregate.
2. **The coverage analyzer** inspects every `[EventSourcedProjection]`-decorated type and verifies it has a matching `Create`, `Apply`, or `ShouldDelete` handler for each discovered event. Missing handlers produce **MARTEN001**.
3. **The raw append analyzer** inspects all method invocations and flags direct calls to `AppendOne` or `AppendMany` unless the containing type is marked with `[ApprovedAppendWrapper]`. Violations produce **MARTEN002**.

## Requirements

- .NET 10.0+ (for contracts and consumer projects)
- C# with Roslyn support (any modern .NET SDK)

## License

[MIT](LICENSE)
