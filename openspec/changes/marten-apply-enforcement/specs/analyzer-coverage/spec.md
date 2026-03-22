## ADDED Requirements

### Requirement: Analyzer validates every event has an approved convention handler
For each event in the generated coverage metadata, the analyzer SHALL verify that the projection host type defines at least one approved Marten convention method that handles that event type. Approved methods are: `static TProjection Create(TEvent)`, `void Apply(TEvent)`, and `bool ShouldDelete(TEvent)`.

#### Scenario: All events covered by Apply or Create
- **WHEN** `OrderProjection` has `Create(OrderCreated)`, `Apply(ItemAdded)`, and `Apply(OrderCancelled)`
- **THEN** the analyzer SHALL report no diagnostics

#### Scenario: Missing handler for one event
- **WHEN** `OrderArchived` is discovered for the `Order` aggregate but `OrderProjection` has no `Create`, `Apply`, or `ShouldDelete` method accepting `OrderArchived`
- **THEN** the analyzer SHALL report a diagnostic error on `OrderProjection`

#### Scenario: Event handled by ShouldDelete instead of Apply
- **WHEN** `OrderCancelled` is handled by `bool ShouldDelete(OrderCancelled)` instead of `Apply`
- **THEN** the analyzer SHALL accept this as valid coverage for `OrderCancelled`

### Requirement: Analyzer inspects combined partial type declarations
When the projection host type is declared as partial across multiple files, the analyzer SHALL resolve the full type symbol and inspect all method declarations across all partial files.

#### Scenario: Apply methods split across two partial files
- **WHEN** `OrderProjection` is partial with `Apply(ItemAdded)` in one file and `Apply(OrderCancelled)` in another
- **THEN** the analyzer SHALL treat both methods as part of the same projection and validate coverage accordingly

### Requirement: Analyzer inspects SingleStreamProjection subclasses
When the `[EventSourcedProjection]` attribute is applied to a type that inherits from Marten's `SingleStreamProjection<T>`, the analyzer SHALL inspect that subclass for convention methods.

#### Scenario: Convention methods on SingleStreamProjection subclass
- **WHEN** `OrderSingleStreamProjection : SingleStreamProjection<OrderProjection>` is annotated with `[EventSourcedProjection(typeof(Order))]` and defines `Apply(ItemAdded)`
- **THEN** the analyzer SHALL validate convention coverage on the `OrderSingleStreamProjection` type

### Requirement: Diagnostic message clearly identifies missing handler
When a convention handler is missing, the diagnostic message SHALL include the projection host type name and the unhandled event type name, and SHALL suggest the approved handler signatures.

#### Scenario: Missing handler diagnostic message
- **WHEN** `OrderProjection` is missing a handler for `OrderArchived`
- **THEN** the diagnostic message SHALL read: "Projection 'OrderProjection' is missing a Marten convention handler for event 'OrderArchived'. Add Apply(OrderArchived domainEvent), Create(OrderArchived domainEvent), or another approved handler."

### Requirement: Analyzer reports error severity
Missing convention handler diagnostics SHALL have error severity so that the build fails.

#### Scenario: Build fails on missing handler
- **WHEN** a required convention handler is missing
- **THEN** the build SHALL fail with a compiler error, not a warning
