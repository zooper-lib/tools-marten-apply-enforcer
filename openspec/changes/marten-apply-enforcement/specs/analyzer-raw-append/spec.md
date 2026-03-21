## ADDED Requirements

### Requirement: Analyzer forbids raw AppendOne calls outside approved wrappers
The analyzer SHALL report a diagnostic error when `AppendOne` is invoked on a Marten event stream from any type that is not an approved append wrapper.

#### Scenario: Raw AppendOne in a use case class
- **WHEN** a use case class calls `eventStream.AppendOne(new ItemAdded("Widget"))`
- **THEN** the analyzer SHALL report: "Direct Marten AppendOne call is forbidden here. Use the typed stream wrapper instead."

#### Scenario: AppendOne inside an approved wrapper
- **WHEN** a method in a class marked as an approved append wrapper calls `eventStream.AppendOne(domainEvent)`
- **THEN** the analyzer SHALL NOT report a diagnostic

### Requirement: Analyzer forbids raw AppendMany calls outside approved wrappers
The analyzer SHALL report a diagnostic error when `AppendMany` is invoked on a Marten event stream from any type that is not an approved append wrapper.

#### Scenario: Raw AppendMany in application code
- **WHEN** application code calls `eventStream.AppendMany(events)`
- **THEN** the analyzer SHALL report: "Direct Marten AppendMany call is forbidden here. Use the typed stream wrapper instead."

### Requirement: Approved wrapper opt-in mechanism
The system SHALL provide a mechanism to mark a type as an approved append wrapper, exempting it from the raw append diagnostic. This MAY be an attribute such as `[ApprovedAppendWrapper]` or a convention based on the typed wrapper pattern.

#### Scenario: Type marked as approved wrapper
- **WHEN** a static class is annotated with `[ApprovedAppendWrapper]` and calls `AppendOne` internally
- **THEN** the analyzer SHALL NOT report a diagnostic for that call

### Requirement: Raw append diagnostic has error severity
Raw Marten append diagnostics SHALL have error severity so that the build fails when raw append calls exist in non-approved code.

#### Scenario: Build fails on raw append
- **WHEN** a developer adds a raw `AppendOne` call in a use case
- **THEN** the build SHALL fail with a compiler error
