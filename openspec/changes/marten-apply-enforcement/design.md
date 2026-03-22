## Context

Marten is an event-sourcing library for .NET that uses convention-based methods (`Create`, `Apply`, `ShouldDelete`) on projection types to rebuild aggregate state from event streams. These conventions are discovered at runtime; there is no compile-time enforcement that every domain event has a matching handler. When a developer introduces a new event and forgets the handler, the projection silently ignores it, producing incorrect state. Additionally, nothing prevents developers from calling raw Marten append APIs (`AppendOne`, `AppendMany`) with arbitrary event types, bypassing aggregate-stream type safety.

The codebase uses `Zooper.Lion` which provides `IDomainEvent` and `IAggregateRoot` as first-class domain primitives. Events are associated with aggregates through a naming/discovery convention, and projections are the types that host Marten convention methods.

## Goals / Non-Goals

**Goals:**
- Compile-time enforcement that every concrete domain event for an aggregate has a matching Marten convention handler on the projection host type.
- Automatic discovery of aggregate events — no manually maintained event lists.
- Build failure with clear diagnostics when a handler is missing.
- Prevention of raw Marten append calls outside an approved infrastructure wrapper.
- Keep Marten replay plumbing out of the pure domain aggregate.

**Non-Goals:**
- Auto-generating the body of `Apply` or `Create` methods.
- Inferring business rules or replacing domain modeling.
- Replacing Marten's projection system or publishing integration events.
- Runtime enforcement — this is strictly compile-time.

## Decisions

### 1. Aggregate-to-event discovery via `IDomainEvent<TAggregate>` generic marker

**Decision**: Introduce a generic interface `IDomainEvent<TAggregate>` (extending `IDomainEvent`) so that each event type declares its aggregate affinity at the type level. The source generator scans the compilation for all non-abstract types implementing `IDomainEvent<TAggregate>` for a given `TAggregate`.

**Rationale**: A generic type parameter is unambiguous, compile-time analyzable, and requires no attribute lists. The type system enforces that each event is mapped to exactly one aggregate. Alternative approaches like namespace conventions or naming conventions are fragile and not enforceable by the compiler. A marker attribute on each event would be equivalent but less idiomatic in C#.

**Alternative considered**: Namespace-based discovery (events in `Orders.Events` namespace belong to `Order`). Rejected because namespace conventions are not compiler-enforced and refactoring can silently break discovery.

### 2. `EventSourcedProjectionAttribute` links projection host to aggregate

**Decision**: A single `[EventSourcedProjection(typeof(Order))]` attribute on the projection host type declares which aggregate's events it must cover. This is the entry point for the source generator.

**Rationale**: This is a one-time declaration per projection, not a per-event list. Adding new events never requires updating this attribute. It works on plain classes, `SingleStreamProjection` subclasses, and partial types.

**Alternative considered**: Convention-based naming (e.g., `OrderProjection` → `Order`). Rejected because it's fragile and cannot be validated by the compiler without additional heuristics.

### 3. Source generator emits coverage metadata as a static class

**Decision**: For each `[EventSourcedProjection]`-annotated type, the generator emits an internal static class containing a `Type[]` array of all discovered events. This metadata is consumed by the analyzer at compile time.

**Rationale**: A simple static array is easy to emit, easy to inspect, and provides the full event set as a compilation artifact. The analyzer cross-references this set against the methods declared on the host type.

### 4. Analyzer validates Create, Apply, and ShouldDelete coverage

**Decision**: The analyzer inspects the projection host type for methods matching Marten conventions: `static T Create(TEvent)`, `void Apply(TEvent)`, and `bool ShouldDelete(TEvent)`. Each event in the coverage set must be handled by at least one of these methods.

**Rationale**: Marten recognizes these three method families. Requiring at least one handler per event ensures no event is silently dropped. The analyzer does not dictate which handler to use — that's a domain decision.

**Alternative considered**: Only enforcing `IApply<T>` interface implementation. Rejected because `Create` is static and cannot be enforced through a standard instance interface alone without the analyzer.

### 5. Analyzer forbids raw append calls outside wrapper types

**Decision**: A separate analyzer rule reports diagnostics when `AppendOne` or `AppendMany` is invoked outside a type that is explicitly marked as an approved wrapper (e.g., via an `[ApprovedAppendWrapper]` attribute or by being the typed wrapper itself).

**Rationale**: Without this rule, a developer could append an event to the wrong stream or append an event type that the projection doesn't handle. The typed wrapper constrains the generic parameter to `IDomainEvent<TAggregate>`, closing this gap.

### 6. Typed append wrappers as extension methods

**Decision**: Each aggregate stream gets an extension method like `AppendOrderEvent<TEvent>(this IEventStream<OrderProjection>, TEvent)` where `TEvent : IDomainEvent<Order>`. The generic constraint ensures only events belonging to the `Order` aggregate can be appended.

**Rationale**: Extension methods integrate naturally with Marten's `FetchForWriting` API. The generic constraint provides compile-time type safety. These wrappers are the only approved way to append events.

### 7. Contracts live in a shared project, generator and analyzer in a separate analyzers package

**Decision**: The solution is structured as:
- **Contracts project**: `IApply<T>`, `ICreate<T,P>`, `EventSourcedProjectionAttribute`, `IDomainEvent<T>` (if not already in Zooper.Lion).
- **Generator + Analyzer project**: Roslyn source generator and analyzer rules, packaged as a single analyzer NuGet.
- **Infrastructure project**: Typed append wrappers.

**Rationale**: Separating contracts from analyzers follows the standard .NET analyzer packaging pattern. Consumer projects reference the contracts package for compilation and the analyzer package for enforcement.

## Risks / Trade-offs

- **[Risk] Source generator performance on large codebases** → Mitigation: The generator only scans types implementing `IDomainEvent<T>` and types with `[EventSourcedProjection]`, limiting the search space. Incremental generation is used where possible.
- **[Risk] False positives on legitimate Marten append usage in infrastructure code** → Mitigation: The raw-append analyzer rule allows an opt-out attribute (`[ApprovedAppendWrapper]`) for designated infrastructure types.
- **[Risk] Partial type declarations spanning multiple files** → Mitigation: The analyzer resolves the full type symbol including all partial declarations before checking method coverage.
- **[Risk] Events shared across multiple aggregates** → Mitigation: `IDomainEvent<T>` only supports a single aggregate type parameter. If an event genuinely belongs to multiple streams, it must implement multiple `IDomainEvent<T>` interfaces, and each projection must handle it. This is intentional — shared events should be explicit.
- **[Trade-off] Requires `IDomainEvent<T>` adoption** → All domain events must use the generic marker. This is a one-time migration cost that pays off in permanent compile-time safety.
