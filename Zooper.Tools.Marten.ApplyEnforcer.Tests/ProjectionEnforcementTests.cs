using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests;

public sealed class ProjectionEnforcementTests
{
    [Fact]
    public async Task Generator_emits_event_coverage_metadata()
    {
        var result = await TestCompilationFactory.RunAsync(AllHandlersCoveredSource);

        var generatedSource = string.Join(
            Environment.NewLine,
            result.GeneratorRunResult.Results.SelectMany(generator => generator.GeneratedSources).Select(source => source.SourceText.ToString()));

        Assert.Contains("OrderEventCoverage", generatedSource);
        Assert.Contains("typeof(global::Demo.IOrderCreated)", generatedSource);
        Assert.Contains("typeof(global::Demo.IItemAdded)", generatedSource);
        Assert.Contains("typeof(global::Demo.IOrderCancelled)", generatedSource);
        Assert.DoesNotContain("V1", generatedSource);
    }

    [Fact]
    public async Task Coverage_analyzer_allows_fully_covered_aggregate()
    {
        var result = await TestCompilationFactory.RunAsync(AllHandlersCoveredSource);

        Assert.Empty(GetDiagnostics(result, "MARTEN001"));
        Assert.DoesNotContain(result.Compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Coverage_analyzer_reports_missing_event_handler()
    {
        var result = await TestCompilationFactory.RunAsync(MissingHandlerSource);
        var diagnostic = Assert.Single(GetDiagnostics(result, "MARTEN001"));

        Assert.Contains("Order", diagnostic.GetMessage());
        Assert.Contains("IOrderArchived", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Coverage_analyzer_combines_partial_aggregate_types()
    {
        var result = await TestCompilationFactory.RunAsync(PartialAggregatePartOne, PartialAggregatePartTwo);

        Assert.Empty(GetDiagnostics(result, "MARTEN001"));
    }

    [Fact]
    public async Task Raw_append_analyzer_rejects_direct_append_calls()
    {
        var result = await TestCompilationFactory.RunAsync(RawAppendSource);
        var diagnostic = Assert.Single(GetDiagnostics(result, "MARTEN002"));

        Assert.Contains("AppendOne", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Raw_append_analyzer_allows_approved_wrapper_calls()
    {
        var result = await TestCompilationFactory.RunAsync(ApprovedWrapperSource);

        Assert.Empty(GetDiagnostics(result, "MARTEN002"));
    }

    [Fact]
    public async Task Typed_wrapper_rejects_wrong_aggregate_event_at_compile_time()
    {
        var result = await TestCompilationFactory.RunAsync(WrongAggregateEventSource);

        Assert.Contains(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
                          diagnostic.GetMessage().Contains("IDomainEvent<Demo.Order>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task End_to_end_missing_handler_then_fix_behaves_as_expected()
    {
        var failingResult = await TestCompilationFactory.RunAsync(MissingHandlerSource);
        var passingResult = await TestCompilationFactory.RunAsync(AllHandlersCoveredWithArchiveSource);

        Assert.Single(GetDiagnostics(failingResult, "MARTEN001"));
        Assert.Empty(GetDiagnostics(passingResult, "MARTEN001"));
        Assert.DoesNotContain(passingResult.Compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task End_to_end_raw_append_then_wrapper_behaves_as_expected()
    {
        var failingResult = await TestCompilationFactory.RunAsync(RawAppendSource);
        var passingResult = await TestCompilationFactory.RunAsync(ApprovedWrapperSource);

        Assert.Single(GetDiagnostics(failingResult, "MARTEN002"));
        Assert.Empty(GetDiagnostics(passingResult, "MARTEN002"));
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(CompilationResult result, string id)
    {
        return [.. result.AnalyzerDiagnostics.Where(diagnostic => diagnostic.Id == id)];
    }

    private const string CommonPreamble = """
using System;
using Marten.Events.Dcb;
using Zooper.Lion.Domain.Entities;
using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

namespace Demo;
""";

    private const string AllHandlersCoveredSource = CommonPreamble + """
[EventSourcedAggregate]
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }

    public static Order Create(IOrderCreated domainEvent) => new();
    public void Apply(IItemAdded domainEvent) { }
    public void Apply(IOrderCancelled domainEvent) { }
}

public interface IOrderCreated : IDomainEvent<Order>
{
    public sealed record V1(Guid OrderId) : IOrderCreated;
}

public interface IItemAdded : IDomainEvent<Order>
{
    public sealed record V1(string ItemName) : IItemAdded;
}

public interface IOrderCancelled : IDomainEvent<Order>
{
    public sealed record V1() : IOrderCancelled;
}
""";

    private const string AllHandlersCoveredWithArchiveSource = CommonPreamble + """
[EventSourcedAggregate]
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }

    public static Order Create(IOrderCreated domainEvent) => new();
    public void Apply(IItemAdded domainEvent) { }
    public void Apply(IOrderCancelled domainEvent) { }
    public void Apply(IOrderArchived domainEvent) { }
}

public interface IOrderCreated : IDomainEvent<Order>
{
    public sealed record V1(Guid OrderId) : IOrderCreated;
}

public interface IItemAdded : IDomainEvent<Order>
{
    public sealed record V1(string ItemName) : IItemAdded;
}

public interface IOrderCancelled : IDomainEvent<Order>
{
    public sealed record V1() : IOrderCancelled;
}

public interface IOrderArchived : IDomainEvent<Order>
{
    public sealed record V1() : IOrderArchived;
}
""";

    private const string MissingHandlerSource = CommonPreamble + """
[EventSourcedAggregate]
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }

    public static Order Create(IOrderCreated domainEvent) => new();
    public void Apply(IItemAdded domainEvent) { }
    public void Apply(IOrderCancelled domainEvent) { }
}

public interface IOrderCreated : IDomainEvent<Order>
{
    public sealed record V1(Guid OrderId) : IOrderCreated;
}

public interface IItemAdded : IDomainEvent<Order>
{
    public sealed record V1(string ItemName) : IItemAdded;
}

public interface IOrderCancelled : IDomainEvent<Order>
{
    public sealed record V1() : IOrderCancelled;
}

public interface IOrderArchived : IDomainEvent<Order>
{
    public sealed record V1() : IOrderArchived;
}
""";

    private const string PartialAggregatePartOne = CommonPreamble + """
[EventSourcedAggregate]
public sealed partial record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }

    public static Order Create(IOrderCreated domainEvent) => new();
    public void Apply(IItemAdded domainEvent) { }
}

public interface IOrderCreated : IDomainEvent<Order>;
public interface IItemAdded : IDomainEvent<Order>;
public interface IOrderCancelled : IDomainEvent<Order>;
""";

    private const string PartialAggregatePartTwo = CommonPreamble + """
public sealed partial record Order
{
    public void Apply(IOrderCancelled domainEvent) { }
}
""";

    private const string RawAppendSource = CommonPreamble + """
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public interface IItemAdded : IDomainEvent<Order>
{
    public sealed record V1(string ItemName) : IItemAdded;
}

public static class OrderUseCase
{
    public static void Append(IEventBoundary<Order> eventBoundary)
    {
        eventBoundary.AppendOne(new IItemAdded.V1("Widget"));
    }
}
""";

    private const string ApprovedWrapperSource = CommonPreamble + """
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public interface IItemAdded : IDomainEvent<Order>
{
    public sealed record V1(string ItemName) : IItemAdded;
}

[ApprovedAppendWrapper]
public static class OrderStreamExtensions
{
    public static void AppendOrderEvent<TEvent>(this IEventBoundary<Order> eventBoundary, TEvent domainEvent)
        where TEvent : IDomainEvent<Order>
    {
        eventBoundary.AppendOne(domainEvent);
    }
}
""";

    private const string WrongAggregateEventSource = CommonPreamble + """
public sealed record Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record Invoice : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public interface IItemAdded : IDomainEvent<Order>
{
    public sealed record V1(string ItemName) : IItemAdded;
}

public interface IInvoicePaid : IDomainEvent<Invoice>
{
    public sealed record V1(Guid InvoiceId) : IInvoicePaid;
}

[ApprovedAppendWrapper]
public static class OrderStreamExtensions
{
    public static void AppendOrderEvent<TEvent>(this IEventBoundary<Order> eventBoundary, TEvent domainEvent)
        where TEvent : IDomainEvent<Order>
    {
        eventBoundary.AppendOne(domainEvent);
    }
}

public static class OrderUseCase
{
    public static void Append(IEventBoundary<Order> eventBoundary)
    {
        eventBoundary.AppendOrderEvent(new IInvoicePaid.V1(Guid.NewGuid()));
    }
}
""";
}