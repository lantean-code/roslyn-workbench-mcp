using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Classifies a type's implemented plugin handler contracts and marker interfaces.
/// </summary>
internal static class PluginHandlerFacts
{
    /// <summary>
    /// Collects the closed query and mutation handler contracts implemented by a type.
    /// </summary>
    /// <param name="handlerType">The possible handler type.</param>
    /// <param name="queryHandlerDefinition">The open generic query-handler contract.</param>
    /// <param name="mutationHandlerDefinition">The open generic mutation-handler contract.</param>
    /// <param name="queryHandlerMarker">The optional non-generic query-handler marker.</param>
    /// <param name="mutationHandlerMarker">The optional non-generic mutation-handler marker.</param>
    /// <returns>The implemented contracts and whether the type is a handler candidate.</returns>
    public static PluginHandlerContractSet GetContracts(
        INamedTypeSymbol handlerType,
        INamedTypeSymbol queryHandlerDefinition,
        INamedTypeSymbol mutationHandlerDefinition,
        INamedTypeSymbol? queryHandlerMarker = null,
        INamedTypeSymbol? mutationHandlerMarker = null)
    {
        var queryContracts = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var mutationContracts = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var isHandlerCandidate = false;
        foreach (var interfaceType in handlerType.AllInterfaces)
        {
            var definition = interfaceType.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(definition, queryHandlerDefinition))
            {
                queryContracts.Add(interfaceType);
                isHandlerCandidate = true;
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(definition, mutationHandlerDefinition))
            {
                mutationContracts.Add(interfaceType);
                isHandlerCandidate = true;
                continue;
            }

            var isQueryMarker = SymbolEqualityComparer.Default.Equals(
                interfaceType,
                queryHandlerMarker);

            var isMutationMarker = SymbolEqualityComparer.Default.Equals(
                interfaceType,
                mutationHandlerMarker);

            if (isQueryMarker || isMutationMarker)
            {
                isHandlerCandidate = true;
            }
        }

        var immutableQueryContracts = queryContracts.ToImmutable();
        var immutableMutationContracts = mutationContracts.ToImmutable();

        var contractSet = new PluginHandlerContractSet(
            immutableQueryContracts,
            immutableMutationContracts,
            isHandlerCandidate);

        return contractSet;
    }
}
