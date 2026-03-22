using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests;

public sealed class RawAppendAnalyzerTests
{
    private const string Preamble = """
        using System;
        using Marten.Events.Dcb;
        using Zooper.Lion.Domain.Entities;
        using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

        namespace Demo;

        public sealed record Order : IAggregateRoot<Guid>
        {
            public Guid Id { get; init; }
        }

        public interface IItemAdded : IDomainEvent<Order>
        {
            public sealed record V1(string ItemName) : IItemAdded;
        }
        """;

    [Fact]
    public async Task Flags_AppendOne_outside_wrapper()
    {
        var source = Preamble + """
            public static class OrderUseCase
            {
                public static void Do(IEventBoundary<Order> stream)
                {
                    stream.AppendOne(new IItemAdded.V1("Widget"));
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten002(result));

        Assert.Contains("AppendOne", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Flags_AppendMany_outside_wrapper()
    {
        var source = Preamble + """
            public static class OrderUseCase
            {
                public static void Do(IEventBoundary<Order> stream)
                {
                    stream.AppendMany(new IItemAdded.V1("A"), new IItemAdded.V1("B"));
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten002(result));

        Assert.Contains("AppendMany", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Allows_calls_inside_approved_wrapper()
    {
        var source = Preamble + """
            [ApprovedAppendWrapper]
            public static class OrderStreamExtensions
            {
                public static void AppendOrderEvent<TEvent>(this IEventBoundary<Order> stream, TEvent e)
                    where TEvent : IDomainEvent<Order>
                {
                    stream.AppendOne(e);
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten002(result));
    }

    [Fact]
    public async Task Allows_calls_inside_nested_class_of_approved_wrapper()
    {
        var source = Preamble + """
            [ApprovedAppendWrapper]
            public static class OrderStreamExtensions
            {
                public static class Inner
                {
                    public static void Append(IEventBoundary<Order> stream)
                    {
                        stream.AppendOne(new IItemAdded.V1("Widget"));
                    }
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten002(result));
    }

    [Fact]
    public async Task Multiple_raw_calls_produce_multiple_diagnostics()
    {
        var source = Preamble + """
            public static class OrderUseCase
            {
                public static void Do(IEventBoundary<Order> stream)
                {
                    stream.AppendOne(new IItemAdded.V1("A"));
                    stream.AppendOne(new IItemAdded.V1("B"));
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Equal(2, Marten002(result).Length);
    }

    [Fact]
    public async Task Does_not_flag_non_Marten_method_named_AppendOne()
    {
        var source = Preamble + """
            public class MyService
            {
                public void AppendOne(object item) { }
            }

            public static class Consumer
            {
                public static void Do()
                {
                    new MyService().AppendOne("hello");
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Empty(Marten002(result));
    }

    [Fact]
    public async Task Flags_AppendOne_and_AppendMany_in_same_method()
    {
        var source = Preamble + """
            public static class OrderUseCase
            {
                public static void Do(IEventBoundary<Order> stream)
                {
                    stream.AppendOne(new IItemAdded.V1("A"));
                    stream.AppendMany(new IItemAdded.V1("B"), new IItemAdded.V1("C"));
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostics = Marten002(result);

        Assert.Equal(2, diagnostics.Length);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("AppendOne"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("AppendMany"));
    }

    [Fact]
    public async Task Diagnostic_location_points_to_invocation_site()
    {
        var source = Preamble + """
            public static class OrderUseCase
            {
                public static void Do(IEventBoundary<Order> stream)
                {
                    stream.AppendOne(new IItemAdded.V1("Widget"));
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);
        var diagnostic = Assert.Single(Marten002(result));

        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    [Fact]
    public async Task Unapproved_wrapper_class_is_still_flagged()
    {
        var source = Preamble + """
            public static class NotApprovedWrapper
            {
                public static void AppendOrderEvent<TEvent>(this IEventBoundary<Order> stream, TEvent e)
                    where TEvent : IDomainEvent<Order>
                {
                    stream.AppendOne(e);
                }
            }
            """;

        var result = await TestCompilationFactory.RunAsync(source);

        Assert.Single(Marten002(result));
    }

    private static ImmutableArray<Diagnostic> Marten002(CompilationResult result)
    {
        return [.. result.AnalyzerDiagnostics.Where(d => d.Id == "MARTEN002")];
    }
}
