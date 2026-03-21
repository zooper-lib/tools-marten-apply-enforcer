## 1. Solution and Project Setup

- [ ] 1.1 Create .NET solution with projects: Contracts, Analyzers (source generator + analyzer), and a Tests project
- [ ] 1.2 Add NuGet references for Roslyn analyzer/generator SDK (`Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Analyzers`)
- [ ] 1.3 Add NuGet references for Marten and Zooper.Lion in the test/sample project
- [ ] 1.4 Configure the Analyzers project to output as an analyzer/generator assembly (`<IsRoslynComponent>true</IsRoslynComponent>`)

## 2. Contracts

- [ ] 2.1 Implement `IDomainEvent<TAggregate>` interface extending `IDomainEvent` (or integrate with Zooper.Lion if it already provides this)
- [ ] 2.2 Implement `IApply<in TEvent>` interface with `void Apply(TEvent domainEvent)`
- [ ] 2.3 Implement `ICreate<TEvent, TProjection>` interface with `static abstract TProjection Create(TEvent domainEvent)` and CRTP constraint
- [ ] 2.4 Implement `EventSourcedProjectionAttribute` accepting `Type aggregateType` parameter
- [ ] 2.5 Implement `ApprovedAppendWrapperAttribute` for marking typed wrapper types

## 3. Source Generator

- [ ] 3.1 Implement `IIncrementalGenerator` entry point that registers syntax and semantic providers
- [ ] 3.2 Implement provider to discover all types annotated with `[EventSourcedProjection(typeof(T))]`
- [ ] 3.3 Implement provider to discover all non-abstract types implementing `IDomainEvent<TAggregate>` for each aggregate
- [ ] 3.4 Emit the coverage metadata static class (e.g., `OrderProjectionEventCoverage`) with the `Type[]` array for each projection host
- [ ] 3.5 Write unit tests verifying generator discovers events and emits correct metadata

## 4. Coverage Analyzer

- [ ] 4.1 Implement `DiagnosticAnalyzer` that inspects types with `[EventSourcedProjection]`
- [ ] 4.2 Implement logic to resolve all convention methods (`Create`, `Apply`, `ShouldDelete`) on the host type including across partial declarations
- [ ] 4.3 Implement cross-reference of discovered events against convention methods — report error for each unhandled event
- [ ] 4.4 Implement clear diagnostic messages including projection name, missing event name, and suggested handler signatures
- [ ] 4.5 Verify analyzer works with `SingleStreamProjection<T>` subclasses as convention hosts
- [ ] 4.6 Write unit tests: all events covered → no diagnostic; missing handler → error diagnostic; partial types → correct resolution

## 5. Raw Append Analyzer

- [ ] 5.1 Implement `DiagnosticAnalyzer` that detects `AppendOne` and `AppendMany` invocations
- [ ] 5.2 Implement logic to check whether the containing type is marked with `[ApprovedAppendWrapper]` — suppress diagnostic if so
- [ ] 5.3 Implement diagnostic messages for forbidden raw append calls
- [ ] 5.4 Write unit tests: raw append in use case → error; raw append in approved wrapper → no error

## 6. Typed Append Wrappers

- [ ] 6.1 Implement a sample typed append extension method (`AppendOrderEvent<TEvent>`) for a proof-of-concept aggregate
- [ ] 6.2 Mark the wrapper type with `[ApprovedAppendWrapper]` so it passes the raw-append analyzer
- [ ] 6.3 Verify generic constraint `where TEvent : IDomainEvent<Order>` rejects wrong-aggregate events at compile time

## 7. End-to-End Verification

- [ ] 7.1 Create a proof-of-concept `Order` aggregate, events (`OrderCreated`, `ItemAdded`, `OrderCancelled`), and `OrderProjection` with full coverage
- [ ] 7.2 Verify build succeeds with full event coverage
- [ ] 7.3 Add a new event (`OrderArchived`) without a handler and verify build fails with correct diagnostic
- [ ] 7.4 Add the missing handler and verify build succeeds again
- [ ] 7.5 Add a raw `AppendOne` call in a use case and verify build fails with correct diagnostic
- [ ] 7.6 Move the append call to the typed wrapper and verify build succeeds
