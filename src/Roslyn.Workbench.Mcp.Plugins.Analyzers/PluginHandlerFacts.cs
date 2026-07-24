using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal static class PluginHandlerFacts
{
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
