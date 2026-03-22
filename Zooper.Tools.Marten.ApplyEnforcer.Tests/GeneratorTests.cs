using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests;

public sealed class GeneratorTests
{
    private const string Preamble = """
        using System;
        using Marten.Events.Dcb;
        using Zooper.Lion.Domain.Entities;
        using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

        namespace Demo;
        """;

    [Fact]
    public async Task Emits_event_coverage_metadata_for_annotated_aggregate()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
                public void Apply(IItemAdded e) { }
            }

            public interface IOrderCreated : IDomainEvent<Order>
            {
                public sealed record V1(Guid OrderId) : IOrderCreated;
            }

            public interface IItemAdded : IDomainEvent<Order>
            {
                public sealed record V1(string ItemName) : IItemAdded;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generated = GetGeneratedSource(result);

        Assert.Contains("OrderEventCoverage", generated);
        Assert.Contains("typeof(global::Demo.IOrderCreated)", generated);
        Assert.Contains("typeof(global::Demo.IItemAdded)", generated);
    }

    [Fact]
    public async Task Excludes_nested_V1_types_from_metadata()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>
            {
                public sealed record V1(Guid OrderId) : IOrderCreated;
                public sealed record V2(Guid OrderId, string Reason) : IOrderCreated;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generated = GetGeneratedSource(result);

        Assert.Contains("typeof(global::Demo.IOrderCreated)", generated);
        Assert.DoesNotContain("V1", generated);
        Assert.DoesNotContain("V2", generated);
    }

    [Fact]
    public async Task Handles_multiple_aggregates_with_separate_event_lists()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            [EventSourcedAggregate]
            public sealed record Invoice : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Invoice Create(IInvoiceIssued e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>
            {
                public sealed record V1(Guid OrderId) : IOrderCreated;
            }

            public interface IInvoiceIssued : IDomainEvent<Invoice>
            {
                public sealed record V1(Guid InvoiceId) : IInvoiceIssued;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generated = GetGeneratedSource(result);

        Assert.Contains("OrderEventCoverage", generated);
        Assert.Contains("InvoiceEventCoverage", generated);
        Assert.Contains("typeof(global::Demo.IOrderCreated)", generated);
        Assert.Contains("typeof(global::Demo.IInvoiceIssued)", generated);
    }

    [Fact]
    public async Task Produces_no_output_when_no_aggregates_are_annotated()
    {
        var source = Preamble + """
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }

            public interface IOrderCreated : IDomainEvent<Order>
            {
                public sealed record V1(Guid OrderId) : IOrderCreated;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generatedSources = result.GeneratorRunResult.Results
            .SelectMany(r => r.GeneratedSources)
            .ToArray();

        Assert.Empty(generatedSources);
    }

    [Fact]
    public async Task Emits_empty_array_for_aggregate_with_zero_events()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generated = GetGeneratedSource(result);

        Assert.Contains("OrderEventCoverage", generated);
        Assert.Contains("Events =", generated);
    }

    [Fact]
    public async Task Discovers_bare_interface_event_without_nested_versions()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generated = GetGeneratedSource(result);

        Assert.Contains("typeof(global::Demo.IOrderCreated)", generated);
    }

    [Fact]
    public async Task Does_not_count_event_from_other_aggregate()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            public sealed record Invoice : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IInvoicePaid : IDomainEvent<Invoice>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var generated = GetGeneratedSource(result);

        Assert.Contains("typeof(global::Demo.IOrderCreated)", generated);
        Assert.DoesNotContain("IInvoicePaid", generated);
    }

    private static string GetGeneratedSource(CompilationResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.GeneratorRunResult.Results
                .SelectMany(r => r.GeneratedSources)
                .Select(s => s.SourceText.ToString()));
    }
}
