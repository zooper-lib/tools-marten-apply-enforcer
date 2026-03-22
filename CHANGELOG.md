# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-03-22

### Changed

- **Breaking:** Replaced `[EventSourcedProjection(typeof(TAggregate))]` with parameterless `[EventSourcedAggregate]` attribute — the attribute is now placed directly on the aggregate instead of a separate projection class
- **Breaking:** Removed `IApply<TEvent>` and `ICreate<TEvent, TProjection>` interfaces — aggregates use Marten convention methods (`Create`, `Apply`, `ShouldDelete`) directly
- Event discovery now uses **direct** interface implementation (`Interfaces`) instead of `AllInterfaces`, so versioned concrete types (e.g. `V1`, `V2`) nested inside an event interface are automatically excluded
- Events are now declared as interfaces (e.g. `IOrderCreated : IDomainEvent<Order>`) with nested versioned records, matching real-world aggregate patterns
- MARTEN001 diagnostic message updated from "Projection" to "Aggregate"

### Removed

- `EventSourcedProjectionAttribute` — replaced by `EventSourcedAggregateAttribute`
- `IApply<TEvent>` interface
- `ICreate<TEvent, TProjection>` interface

### Added

- `EventSourcedAggregateAttribute` — parameterless attribute placed on the aggregate type itself
- Comprehensive test suite (42 tests across `GeneratorTests`, `CoverageAnalyzerTests`, `RawAppendAnalyzerTests`)

## [1.0.0] - 2025-01-01

### Added

- Initial release
- `EventSourcedProjectionGenerator` source generator for discovering `IDomainEvent<T>` types per aggregate
- `ProjectionCoverageAnalyzer` (MARTEN001) for enforcing handler coverage on projections
- `RawAppendAnalyzer` (MARTEN002) for flagging direct `AppendOne`/`AppendMany` calls
- `IDomainEvent<TAggregate>` interface
- `IApply<TEvent>` interface
- `ICreate<TEvent, TProjection>` interface
- `EventSourcedProjectionAttribute`
- `ApprovedAppendWrapperAttribute`
