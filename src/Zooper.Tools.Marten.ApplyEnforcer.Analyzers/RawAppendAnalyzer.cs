using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Zooper.Tools.Marten.ApplyEnforcer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawAppendAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticCatalog.ForbiddenRawAppend);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!SymbolHelpers.IsMartenAppendMethod(invocation.TargetMethod))
        {
            return;
        }

        if (SymbolHelpers.IsApprovedAppendWrapper(context.ContainingSymbol?.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticCatalog.ForbiddenRawAppend,
            invocation.Syntax.GetLocation(),
            invocation.TargetMethod.Name));
    }
}