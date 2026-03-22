## ADDED Requirements

### Requirement: Typed append extension method per aggregate stream
The system SHALL provide a typed extension method for each aggregate stream that constrains appended events to the correct aggregate type using a generic constraint on `IDomainEvent<TAggregate>`.

#### Scenario: Append valid event to Order stream
- **WHEN** a use case calls `eventStream.AppendOrderEvent(new ItemAdded("Widget"))` and `ItemAdded` implements `IDomainEvent<Order>`
- **THEN** the event SHALL be appended to the Marten event stream without diagnostics

#### Scenario: Attempt to append wrong aggregate's event
- **WHEN** a use case attempts `eventStream.AppendOrderEvent(new InvoicePaid())` and `InvoicePaid` implements `IDomainEvent<Invoice>` but NOT `IDomainEvent<Order>`
- **THEN** the compiler SHALL reject the call due to the generic constraint violation

### Requirement: Typed wrapper delegates to Marten AppendOne
The typed append wrapper method SHALL internally delegate to Marten's `AppendOne` (or equivalent). The wrapper is the single approved location for calling raw Marten append APIs for that aggregate stream.

#### Scenario: Wrapper calls AppendOne internally
- **WHEN** the typed wrapper `AppendOrderEvent` is called
- **THEN** it SHALL internally invoke `eventStream.AppendOne(domainEvent)` to perform the actual append

### Requirement: Typed wrapper is the only approved append path
Use cases and application code SHALL use the typed wrapper method as the sole mechanism for appending events to an aggregate stream. Direct Marten append calls outside the wrapper are forbidden (enforced by the raw-append analyzer).

#### Scenario: Use case appends through typed wrapper
- **WHEN** a use case needs to append an `ItemAdded` event to an Order stream
- **THEN** it SHALL call `eventStream.AppendOrderEvent(domainEvent)` instead of `eventStream.AppendOne(domainEvent)`
