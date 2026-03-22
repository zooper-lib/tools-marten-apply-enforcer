## ADDED Requirements

### Requirement: EventSourcedProjection attribute links projection to aggregate
The system SHALL provide an `EventSourcedProjectionAttribute` that accepts a `Type` parameter representing the aggregate root. When applied to a projection host type, it SHALL declare that the type is responsible for handling all events belonging to that aggregate.

#### Scenario: Attribute applied to projection class
- **WHEN** a class is annotated with `[EventSourcedProjection(typeof(Order))]`
- **THEN** the source generator and analyzer SHALL treat that class as the Marten convention host for the `Order` aggregate

#### Scenario: Attribute references IAggregateRoot type
- **WHEN** the type parameter passed to `EventSourcedProjectionAttribute` implements `IAggregateRoot`
- **THEN** the attribute SHALL be considered valid

### Requirement: IApply interface enforces Apply method signature
The system SHALL provide a generic interface `IApply<in TEvent>` with a single method `void Apply(TEvent domainEvent)`. Projection types MAY implement this interface to get compile-time enforcement of the `Apply` method signature.

#### Scenario: Projection implements IApply for an event
- **WHEN** a projection class implements `IApply<ItemAdded>`
- **THEN** the compiler SHALL require a `void Apply(ItemAdded domainEvent)` method on that class

### Requirement: ICreate interface enforces static Create method signature
The system SHALL provide a generic interface `ICreate<TEvent, TProjection>` with a static abstract method `static abstract TProjection Create(TEvent domainEvent)` using the curiously recurring template pattern (`where TProjection : ICreate<TEvent, TProjection>`).

#### Scenario: Projection implements ICreate for a creation event
- **WHEN** a projection class implements `ICreate<OrderCreated, OrderProjection>`
- **THEN** the compiler SHALL require a `static OrderProjection Create(OrderCreated domainEvent)` method on that class

### Requirement: IDomainEvent generic marker associates events with aggregates
The system SHALL provide (or integrate with Zooper.Lion's) `IDomainEvent<TAggregate>` interface extending `IDomainEvent`. Each concrete event type SHALL declare its aggregate affinity by implementing `IDomainEvent<TAggregate>` where `TAggregate` is the aggregate root type.

#### Scenario: Event declares aggregate affinity
- **WHEN** a record is declared as `public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>`
- **THEN** the event SHALL be discoverable as belonging to the `Order` aggregate

#### Scenario: Event belonging to multiple aggregates
- **WHEN** a record implements both `IDomainEvent<Order>` and `IDomainEvent<Invoice>`
- **THEN** the event SHALL be discoverable for both aggregates and both projection hosts SHALL be required to handle it
