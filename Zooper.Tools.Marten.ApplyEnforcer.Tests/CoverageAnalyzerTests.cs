using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests;

public sealed class CoverageAnalyzerTests
{
    private const string Preamble = """
        using System;
        using Marten.Events.Dcb;
        using Zooper.Lion.Domain.Entities;
        using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

        namespace Demo;
        """;

    [Fact]
    public async Task Allows_fully_covered_aggregate()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
                public void Apply(IItemAdded e) { }
                public void Apply(IOrderCancelled e) { }
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

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten001(result));
    }

    [Fact]
    public async Task Reports_single_missing_handler()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IOrderArchived : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("Order", diagnostic.GetMessage());
        Assert.Contains("IOrderArchived", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_multiple_missing_handlers()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IItemAdded : IDomainEvent<Order>;
            public interface IOrderCancelled : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostics = Marten001(result);

        Assert.Equal(2, diagnostics.Length);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("IItemAdded"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("IOrderCancelled"));
    }

    [Fact]
    public async Task Accepts_ShouldDelete_as_valid_handler()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
                public bool ShouldDelete(IOrderDeleted e) => true;
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IOrderDeleted : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten001(result));
    }

    [Fact]
    public async Task Rejects_static_Apply_method()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
                public static void Apply(IItemAdded e) { }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IItemAdded : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("IItemAdded", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Rejects_non_static_Create_method()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public Order Create(IOrderCreated e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("IOrderCreated", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Rejects_ShouldDelete_returning_void()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
                public void ShouldDelete(IOrderDeleted e) { }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IOrderDeleted : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("IOrderDeleted", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Rejects_handler_accepting_concrete_V1_instead_of_interface()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated.V1 e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>
            {
                public sealed record V1(Guid OrderId) : IOrderCreated;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("IOrderCreated", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Does_not_check_aggregate_without_attribute()
    {
        var source = Preamble + """
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IItemAdded : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten001(result));
    }

    [Fact]
    public async Task Aggregate_with_zero_events_produces_no_diagnostics()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten001(result));
    }

    [Fact]
    public async Task Multiple_aggregates_each_checked_against_own_events()
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

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IInvoiceIssued : IDomainEvent<Invoice>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten001(result));
    }

    [Fact]
    public async Task Multiple_aggregates_only_missing_cross_aggregate_handler_flagged()
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
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IInvoiceIssued : IDomainEvent<Invoice>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("Invoice", diagnostic.GetMessage());
        Assert.Contains("IInvoiceIssued", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Combines_partial_aggregate_handlers()
    {
        var part1 = Preamble + """
            [EventSourcedAggregate]
            public sealed partial record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IItemAdded : IDomainEvent<Order>;
            """;

        var part2 = Preamble + """
            public sealed partial record Order
            {
                public void Apply(IItemAdded e) { }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(part1, part2);

        Assert.Empty(Marten001(result));
    }

    [Fact]
    public async Task Bare_interface_event_without_nested_versions_is_discovered()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("IOrderCreated", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Handler_with_two_parameters_is_not_accepted()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public void Apply(IOrderCreated e, int extra) { }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Single(Marten001(result));
    }

    [Fact]
    public async Task Diagnostic_location_points_to_aggregate_type()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    [Fact]
    public async Task Apply_returning_non_void_is_not_accepted()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public static Order Create(IOrderCreated e) => new();
                public int Apply(IItemAdded e) => 0;
            }

            public interface IOrderCreated : IDomainEvent<Order>;
            public interface IItemAdded : IDomainEvent<Order>;
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten001(result));

        Assert.Contains("IItemAdded", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_missing_handler_when_events_in_separate_file_and_namespace()
    {
        var aggregateSource = """
            using System;
            using Zooper.Lion.Domain.Entities;
            using Zooper.Tools.Marten.ApplyEnforcer.Contracts;
            using Demo.Events;

            namespace Demo.Aggregates;

            [EventSourcedAggregate]
            public sealed record Order : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }
                public void Apply(IItemAdded e) { }
            }
            """;

        var eventsSource = """
            using System;
            using Zooper.Tools.Marten.ApplyEnforcer.Contracts;
            using Demo.Aggregates;

            namespace Demo.Events;

            public interface IOrderCreated : IDomainEvent<Order>
            {
                public sealed record V1(Guid OrderId) : IOrderCreated;
            }

            public interface IItemAdded : IDomainEvent<Order>
            {
                public sealed record V1(string ItemName) : IItemAdded;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(aggregateSource, eventsSource);
        var diagnostics = Marten001(result);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("IOrderCreated", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_missing_Create_when_many_Apply_handlers_exist()
    {
        var source = Preamble + """
            [EventSourcedAggregate]
            public sealed record AudioFile : IAggregateRoot<Guid>
            {
                public Guid Id { get; init; }

                // Create handler is missing!

                public void Apply(IAudioFileMarkedAsNotFoundDomainEvent e) { }
                public void Apply(IAudioFileLocalRestoredDomainEvent e) { }
                public void Apply(IAudioFileSpotifyRestoredDomainEvent e) { }
                public void Apply(IAudioFileUnboundDomainEvent e) { }
                public void Apply(IAudioFileBoundDomainEvent e) { }
                public void Apply(IAudioFileRenamedDomainEvent e) { }
                public void Apply(IAudioFileMetadataAnalyzingDomainEvent e) { }
                public void Apply(IAudioFileMetadataAnalyzedDomainEvent e) { }
                public void Apply(IAudioFileMetadataUpdatedDomainEvent e) { }
                public void Apply(IAudioFileMetadataAnalysisFailedDomainEvent e) { }
                public void Apply(IAudioFileWaveformGeneratingDomainEvent e) { }
                public void Apply(IAudioFileWaveformAvailableDomainEvent e) { }
                public void Apply(IAudioFileWaveformGenerationFailedDomainEvent e) { }
                public void Apply(IAudioFileWaveformAnalysisResetDomainEvent e) { }
            }

            public interface IAudioFileCreatedDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1(Guid Id) : IAudioFileCreatedDomainEvent;
            }

            public interface IAudioFileMarkedAsNotFoundDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileMarkedAsNotFoundDomainEvent;
            }

            public interface IAudioFileLocalRestoredDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileLocalRestoredDomainEvent;
            }

            public interface IAudioFileSpotifyRestoredDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileSpotifyRestoredDomainEvent;
            }

            public interface IAudioFileUnboundDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileUnboundDomainEvent;
            }

            public interface IAudioFileBoundDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileBoundDomainEvent;
            }

            public interface IAudioFileRenamedDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileRenamedDomainEvent;
            }

            public interface IAudioFileMetadataAnalyzingDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileMetadataAnalyzingDomainEvent;
            }

            public interface IAudioFileMetadataAnalyzedDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileMetadataAnalyzedDomainEvent;
            }

            public interface IAudioFileMetadataUpdatedDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileMetadataUpdatedDomainEvent;
            }

            public interface IAudioFileMetadataAnalysisFailedDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileMetadataAnalysisFailedDomainEvent;
            }

            public interface IAudioFileWaveformGeneratingDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileWaveformGeneratingDomainEvent;
            }

            public interface IAudioFileWaveformAvailableDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileWaveformAvailableDomainEvent;
            }

            public interface IAudioFileWaveformGenerationFailedDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileWaveformGenerationFailedDomainEvent;
            }

            public interface IAudioFileWaveformAnalysisResetDomainEvent : IDomainEvent<AudioFile>
            {
                public sealed record V1() : IAudioFileWaveformAnalysisResetDomainEvent;
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostics = Marten001(result);

        // Should report exactly one missing handler: IAudioFileCreatedDomainEvent
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("IAudioFileCreatedDomainEvent", diagnostic.GetMessage());
    }

    private static ImmutableArray<Diagnostic> Marten001(CompilationResult result)
    {
        return [.. result.AnalyzerDiagnostics.Where(d => d.Id == "MARTEN001")];
    }
}
