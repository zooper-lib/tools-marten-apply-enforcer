## ADDED Requirements

### Requirement: Generator discovers all EventSourcedProjection-annotated types
The source generator SHALL scan the compilation for all types annotated with `[EventSourcedProjection(typeof(TAggregate))]` and treat each as a convention host requiring event coverage enforcement.

#### Scenario: Single projection type in compilation
- **WHEN** the compilation contains `[EventSourcedProjection(typeof(Order))] public class OrderProjection`
- **THEN** the generator SHALL process `OrderProjection` as a convention host for the `Order` aggregate

#### Scenario: Multiple projection types in compilation
- **WHEN** the compilation contains projections for `Order` and `Invoice` aggregates
- **THEN** the generator SHALL independently process each projection and its associated aggregate events

### Requirement: Generator discovers all concrete events for an aggregate
For each convention host, the source generator SHALL discover all non-abstract types in the compilation that implement `IDomainEvent<TAggregate>` for the aggregate referenced by the `[EventSourcedProjection]` attribute.

#### Scenario: Three events belong to Order aggregate
- **WHEN** the compilation contains `OrderCreated : IDomainEvent<Order>`, `ItemAdded : IDomainEvent<Order>`, and `OrderCancelled : IDomainEvent<Order>`
- **THEN** the generator SHALL discover all three types as belonging to the `Order` aggregate

#### Scenario: Abstract event is excluded
- **WHEN** the compilation contains `abstract record BaseOrderEvent : IDomainEvent<Order>`
- **THEN** the generator SHALL NOT include the abstract type in the discovered event set

#### Scenario: New event added to codebase
- **WHEN** a developer adds `public sealed record OrderArchived() : IDomainEvent<Order>`
- **THEN** the generator SHALL automatically include `OrderArchived` in the coverage metadata on the next compilation without any manual registration

### Requirement: Generator emits coverage metadata class
For each convention host, the source generator SHALL emit an internal static class containing a `Type[]` array of all discovered concrete events for that aggregate.

#### Scenario: Coverage metadata generated for OrderProjection
- **WHEN** the `Order` aggregate has events `OrderCreated`, `ItemAdded`, and `OrderCancelled`
- **THEN** the generator SHALL emit a class like `internal static class OrderProjectionEventCoverage` containing `public static readonly Type[] Events` with all three event types

#### Scenario: Coverage metadata updates automatically
- **WHEN** `OrderArchived` is added to the codebase as `IDomainEvent<Order>`
- **THEN** the regenerated `OrderProjectionEventCoverage.Events` array SHALL include `OrderArchived` without any manual changes

### Requirement: Generator uses incremental generation
The source generator SHALL use the Roslyn incremental generator API (`IIncrementalGenerator`) to minimize rebuild overhead.

#### Scenario: Unrelated file changes
- **WHEN** a file unrelated to any aggregate events or projections is modified
- **THEN** the generator SHALL NOT re-execute its full pipeline for already-processed projections
