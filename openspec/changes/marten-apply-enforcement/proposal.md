## Why

Marten's convention-based `Create`, `Apply`, and `ShouldDelete` methods for rebuilding aggregate state are easy to forget when a new domain event is introduced. There is no compile-time safety net — a missing handler silently produces incorrect projections at runtime. Developers can also bypass the typed append surface by calling raw Marten append APIs directly, breaking aggregate-stream integrity.

## What Changes

- Introduce a Roslyn source generator that automatically discovers all concrete domain events belonging to an aggregate stream and emits coverage metadata.
- Introduce a Roslyn analyzer that fails the build when a Marten convention handler (`Create`, `Apply`, or another approved method) is missing for any discovered event.
- Introduce a Roslyn analyzer rule that forbids direct Marten `AppendOne`/`AppendMany` calls outside an approved infrastructure wrapper.
- Introduce typed append wrapper extension methods per aggregate stream to enforce event-aggregate membership at compile time.
- Introduce marker contracts (`IApply<TEvent>`, `ICreate<TEvent, TProjection>`, `EventSourcedProjectionAttribute`) to link projection host types to their aggregate and provide enforceable method signatures.
- Integrate with `Zooper.Lion` `IDomainEvent` and `IAggregateRoot` for automatic aggregate event-discovery.

## Capabilities

### New Capabilities
- `contracts`: Marker attribute, apply/create interfaces, and integration points with Zooper.Lion domain primitives.
- `source-generator`: Roslyn source generator that discovers aggregate events and emits coverage metadata.
- `analyzer-coverage`: Roslyn analyzer that validates every discovered event has an approved Marten convention handler on the projection host type.
- `analyzer-raw-append`: Roslyn analyzer that forbids raw Marten append calls outside the approved wrapper layer.
- `typed-append-wrapper`: Typed extension methods per aggregate stream that constrain appended events to the correct aggregate.

### Modified Capabilities

## Impact

- New NuGet analyzer/generator packages will be added to projects that define Marten projections.
- Existing projections without full event coverage will fail to compile until handlers are added.
- Existing code that calls `AppendOne`/`AppendMany` directly will need to migrate to the typed wrapper.
- Depends on `Zooper.Lion` for `IDomainEvent` and `IAggregateRoot` interfaces.
- Depends on Marten for `IDocumentSession`, `IEventStream`, and `SingleStreamProjection`.
