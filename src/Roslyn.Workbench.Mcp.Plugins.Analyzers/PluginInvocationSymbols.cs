using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class PluginInvocationSymbols
{
    public INamedTypeSymbol QueryHandlerDefinition { get; }

    public INamedTypeSymbol MutationHandlerDefinition { get; }

    public INamedTypeSymbol CancellationTokenType { get; }

    public INamedTypeSymbol BoundedCollectionDefinition { get; }

    public ImmutableArray<INamedTypeSymbol> RawCollectionDefinitions { get; }

    public PluginInvocationSymbols(
        INamedTypeSymbol queryHandlerDefinition,
        INamedTypeSymbol mutationHandlerDefinition,
        INamedTypeSymbol cancellationTokenType,
        INamedTypeSymbol boundedCollectionDefinition,
        ImmutableArray<INamedTypeSymbol> rawCollectionDefinitions)
    {
        QueryHandlerDefinition = queryHandlerDefinition;
        MutationHandlerDefinition = mutationHandlerDefinition;
        CancellationTokenType = cancellationTokenType;
        BoundedCollectionDefinition = boundedCollectionDefinition;
        RawCollectionDefinitions = rawCollectionDefinitions;
    }
}
