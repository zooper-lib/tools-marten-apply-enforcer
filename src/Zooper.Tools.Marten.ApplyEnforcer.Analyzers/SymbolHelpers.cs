using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Zooper.Tools.Marten.ApplyEnforcer.Analyzers;

internal static class SymbolHelpers
{
    private const string ContractsNamespace = "Zooper.Tools.Marten.ApplyEnforcer.Contracts";
    private const string MartenEventsNamespace = "Marten.Events.Dcb";
    private const string ApprovedAppendWrapperAttributeName = "ApprovedAppendWrapperAttribute";
    private const string EventSourcedProjectionAttributeName = "EventSourcedProjectionAttribute";
    private const string GenericDomainEventName = "IDomainEvent`1";
    private const string MartenEventBoundaryName = "IEventBoundary`1";

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            foreach (var nested in GetTypeAndNestedTypes(type))
            {
                yield return nested;
            }
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var nested in GetAllTypes(nestedNamespace))
            {
                yield return nested;
            }
        }
    }

    public static ImmutableArray<INamedTypeSymbol> GetAggregateEvents(Compilation compilation,
        INamedTypeSymbol aggregateType)
    {
        var discovered = new List<INamedTypeSymbol>();

        foreach (var candidate in GetAllTypes(compilation.Assembly.GlobalNamespace))
        {
            if (candidate.IsAbstract)
            {
                continue;
            }

            if (!ImplementsAggregateEvent(candidate, aggregateType))
            {
                continue;
            }

            discovered.Add(candidate);
        }

        return
        [
            ..discovered
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .OrderBy(symbol => symbol.ToDisplayString())
        ];
    }

    public static bool HasApprovedHandler(INamedTypeSymbol hostType, INamedTypeSymbol eventType)
    {
        foreach (var method in hostType.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.Parameters.Length != 1)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType))
            {
                continue;
            }

            switch (method.Name)
            {
                case "Create" when method.IsStatic:
                case "Apply" when method is { IsStatic: false, ReturnsVoid: true }:
                case "ShouldDelete"
                    when method is { IsStatic: false, ReturnType.SpecialType: SpecialType.System_Boolean }:
                    return true;
            }
        }

        return false;
    }

    public static bool IsApprovedAppendWrapper(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (HasAttribute(current, ContractsNamespace, ApprovedAppendWrapperAttributeName))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMartenAppendMethod(IMethodSymbol method)
    {
        if (method.Name is not ("AppendOne" or "AppendMany"))
        {
            return false;
        }

        if (IsMartenEventBoundary(method.ContainingType))
        {
            return true;
        }

        return method.ContainingType.AllInterfaces.Any(IsMartenEventBoundary);
    }

    public static ImmutableArray<INamedTypeSymbol> GetAggregateTypes(INamedTypeSymbol candidate)
    {
        return
        [
            ..candidate.AllInterfaces
                .Where(@interface =>
                    HasMetadataName(@interface.OriginalDefinition, ContractsNamespace, GenericDomainEventName))
                .Select(@interface => @interface.TypeArguments[0] as INamedTypeSymbol)
                .Where(symbol => symbol is not null)
                .Cast<ISymbol>()
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
        ];
    }

    public static bool TryGetProjectionAggregate(INamedTypeSymbol candidate, out INamedTypeSymbol? aggregateType)
    {
        aggregateType = null;

        foreach (var attribute in candidate.GetAttributes())
        {
            if (attribute.AttributeClass is null || !HasMetadataName(attribute.AttributeClass, ContractsNamespace,
                    EventSourcedProjectionAttributeName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length != 1)
            {
                return false;
            }

            aggregateType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            return aggregateType is not null;
        }

        return false;
    }

    public static string GetMetadataClassName(INamedTypeSymbol hostType) => $"{hostType.Name}EventCoverage";

    private static IEnumerable<INamedTypeSymbol> GetTypeAndNestedTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nested in type.GetTypeMembers().SelectMany(GetTypeAndNestedTypes))
        {
            yield return nested;
        }
    }

    private static bool HasAttribute(INamedTypeSymbol candidate, string containingNamespace, string metadataName)
    {
        return candidate.GetAttributes().Any(attribute =>
            attribute.AttributeClass is not null &&
            HasMetadataName(attribute.AttributeClass, containingNamespace, metadataName));
    }

    private static bool ImplementsAggregateEvent(INamedTypeSymbol candidate, INamedTypeSymbol aggregateType)
    {
        return candidate.AllInterfaces.Any(@interface =>
            HasMetadataName(@interface.OriginalDefinition, ContractsNamespace, GenericDomainEventName) &&
            SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], aggregateType));
    }

    private static bool IsMartenEventBoundary(INamedTypeSymbol candidate)
    {
        return HasMetadataName(candidate.OriginalDefinition, MartenEventsNamespace, MartenEventBoundaryName);
    }

    private static bool HasMetadataName(INamedTypeSymbol candidate, string containingNamespace, string metadataName)
    {
        return candidate.MetadataName == metadataName &&
               candidate.ContainingNamespace.ToDisplayString() == containingNamespace;
    }
}