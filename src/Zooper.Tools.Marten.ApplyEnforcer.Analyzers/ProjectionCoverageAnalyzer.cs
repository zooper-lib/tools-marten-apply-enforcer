using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zooper.Tools.Marten.ApplyEnforcer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProjectionCoverageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticCatalog.MissingConventionHandler];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeProjectionHost, SymbolKind.NamedType);
    }

    private static void AnalyzeProjectionHost(SymbolAnalysisContext context)
    {
        var hostType = (INamedTypeSymbol)context.Symbol;
        if (!SymbolHelpers.TryGetProjectionAggregate(hostType, out var aggregateType) || aggregateType is null)
        {
            return;
        }

        var aggregateEvents = SymbolHelpers.GetAggregateEvents(context.Compilation, aggregateType);
        foreach (var aggregateEvent in aggregateEvents)
        {
            if (SymbolHelpers.HasApprovedHandler(hostType, aggregateEvent))
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(
                DiagnosticCatalog.MissingConventionHandler,
                hostType.Locations.FirstOrDefault(),
                hostType.Name,
                aggregateEvent.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }
}