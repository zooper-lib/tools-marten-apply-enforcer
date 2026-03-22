using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Zooper.Tools.Marten.ApplyEnforcer.Analyzers;
using Zooper.Tools.Marten.ApplyEnforcer.Contracts;

namespace Zooper.Tools.Marten.ApplyEnforcer.Tests;

internal static class TestCompilationFactory
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly MetadataReference[] References = BuildReferences();

    public static async Task<CompilationResult> RunAsync(params string[] sources)
    {
        var compilation = CreateCompilation(sources);
        var generator = new EventSourcedProjectionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation,
            out var generatorDiagnostics);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ProjectionCoverageAnalyzer(),
            new RawAppendAnalyzer());

        var analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync();

        return new CompilationResult(
            (CSharpCompilation)outputCompilation,
            generatorDiagnostics,
            analyzerDiagnostics,
            driver.GetRunResult());
    }

    private static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = sources
            .Select((source, index) => CSharpSyntaxTree.ParseText(source, ParseOptions, $"Source{index}.cs"))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: "Zooper.Tools.Marten.ApplyEnforcer.DynamicTests",
            syntaxTrees: syntaxTrees,
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static MetadataReference[] BuildReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var additionalAssemblies = new[]
        {
            typeof(object).Assembly.Location,
            typeof(EventSourcedProjectionAttribute).Assembly.Location,
            typeof(Lion.Domain.Events.IDomainEvent).Assembly.Location,
            typeof(global::Marten.Events.Dcb.IEventBoundary<>).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(additionalAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray<MetadataReference>();
    }
}

internal sealed record CompilationResult(
    CSharpCompilation Compilation,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> AnalyzerDiagnostics,
    GeneratorDriverRunResult GeneratorRunResult)
{
    public ImmutableArray<Diagnostic> AllDiagnostics =>
        Compilation.GetDiagnostics().AddRange(GeneratorDiagnostics).AddRange(AnalyzerDiagnostics);
}