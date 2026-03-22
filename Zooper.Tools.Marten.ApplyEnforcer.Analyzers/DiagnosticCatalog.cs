using Microsoft.CodeAnalysis;

namespace Zooper.Tools.Marten.ApplyEnforcer.Analyzers;

internal static class DiagnosticCatalog
{
    public static readonly DiagnosticDescriptor MissingConventionHandler = new(
        id: "MARTEN001",
        title: "Aggregate is missing a Marten convention handler",
        messageFormat: "Aggregate '{0}' is missing a Marten convention handler for event '{1}'. Add Apply({1} domainEvent), Create({1} domainEvent), or another approved handler.",
        category: "Marten.ApplyEnforcement",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ForbiddenRawAppend = new(
        id: "MARTEN002",
        title: "Raw Marten append is forbidden",
        messageFormat: "Direct Marten {0} call is forbidden here. Use the typed stream wrapper instead.",
        category: "Marten.ApplyEnforcement",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}