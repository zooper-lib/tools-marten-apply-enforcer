using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

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

        Assert.Contains("OrderProjectionEventCoverage", generatedSource);
        Assert.Contains("typeof(global::Demo.OrderCreated)", generatedSource);
        Assert.Contains("typeof(global::Demo.ItemAdded)", generatedSource);
        Assert.Contains("typeof(global::Demo.OrderCancelled)", generatedSource);
    }

    [Fact]
    public async Task Coverage_analyzer_allows_fully_covered_projection()
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

        Assert.Contains("OrderProjection", diagnostic.GetMessage());
        Assert.Contains("OrderArchived", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Coverage_analyzer_combines_partial_projection_types()
    {
        var result = await TestCompilationFactory.RunAsync(PartialProjectionPartOne, PartialProjectionPartTwo);

        Assert.Empty(GetDiagnostics(result, "MARTEN001"));
    }

    [Fact]
    public async Task Coverage_analyzer_supports_single_stream_projection_hosts()
    {
        var result = await TestCompilationFactory.RunAsync(SingleStreamProjectionSource);

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
        return [..result.AnalyzerDiagnostics.Where(diagnostic => diagnostic.Id == id)];
    }

    private const string CommonPreamble = """
using System;
using Marten.Events.Aggregation;
using Marten.Events.Dcb;
using Zooper.Lion.Domain.Entities;
using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

namespace Demo;
""";

    private const string AllHandlersCoveredSource = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;
public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;
public sealed record OrderCancelled() : IDomainEvent<Order>;

[EventSourcedProjection(typeof(Order))]
public sealed class OrderProjection
{
    public static OrderProjection Create(OrderCreated domainEvent) => new();
    public void Apply(ItemAdded domainEvent) { }
    public void Apply(OrderCancelled domainEvent) { }
}
""";

    private const string AllHandlersCoveredWithArchiveSource = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;
public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;
public sealed record OrderCancelled() : IDomainEvent<Order>;
public sealed record OrderArchived() : IDomainEvent<Order>;

[EventSourcedProjection(typeof(Order))]
public sealed class OrderProjection
{
    public static OrderProjection Create(OrderCreated domainEvent) => new();
    public void Apply(ItemAdded domainEvent) { }
    public void Apply(OrderCancelled domainEvent) { }
    public void Apply(OrderArchived domainEvent) { }
}
""";

    private const string MissingHandlerSource = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;
public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;
public sealed record OrderCancelled() : IDomainEvent<Order>;
public sealed record OrderArchived() : IDomainEvent<Order>;

[EventSourcedProjection(typeof(Order))]
public sealed class OrderProjection
{
    public static OrderProjection Create(OrderCreated domainEvent) => new();
    public void Apply(ItemAdded domainEvent) { }
    public void Apply(OrderCancelled domainEvent) { }
}
""";

    private const string PartialProjectionPartOne = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;
public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;
public sealed record OrderCancelled() : IDomainEvent<Order>;

[EventSourcedProjection(typeof(Order))]
public sealed partial class OrderProjection
{
    public static OrderProjection Create(OrderCreated domainEvent) => new();
    public void Apply(ItemAdded domainEvent) { }
}
""";

    private const string PartialProjectionPartTwo = CommonPreamble + """
public sealed partial class OrderProjection
{
    public void Apply(OrderCancelled domainEvent) { }
}
""";

    private const string SingleStreamProjectionSource = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed class OrderState
{
}

public sealed record OrderCreated(Guid OrderId) : IDomainEvent<Order>;
public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;

[EventSourcedProjection(typeof(Order))]
public sealed class OrderProjection : SingleStreamProjection<OrderState, Guid>
{
    public static OrderState Create(OrderCreated domainEvent) => new();
    public void Apply(ItemAdded domainEvent) { }
}
""";

    private const string RawAppendSource = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;

public static class OrderUseCase
{
    public static void Append(IEventBoundary<Order> eventBoundary)
    {
        eventBoundary.AppendOne(new ItemAdded("Widget"));
    }
}
""";

    private const string ApprovedWrapperSource = CommonPreamble + """
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;

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
public sealed class Order : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed class Invoice : IAggregateRoot<Guid>
{
    public Guid Id { get; init; }
}

public sealed record ItemAdded(string ItemName) : IDomainEvent<Order>;
public sealed record InvoicePaid(Guid InvoiceId) : IDomainEvent<Invoice>;

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
        eventBoundary.AppendOrderEvent(new InvoicePaid(Guid.NewGuid()));
    }
}
""";
}