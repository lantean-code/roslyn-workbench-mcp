using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class PluginHandlerContractSet
{
    public ImmutableArray<INamedTypeSymbol> QueryContracts { get; }

    public ImmutableArray<INamedTypeSymbol> MutationContracts { get; }

    public bool IsHandlerCandidate { get; }

    public bool IsValid
    {
        get
        {
            var isQueryHandler = QueryContracts.Length == 1 && MutationContracts.IsEmpty;
            if (isQueryHandler)
            {
                return true;
            }

            return QueryContracts.IsEmpty && MutationContracts.Length == 1;
        }
    }

    public PluginHandlerContractSet(
        ImmutableArray<INamedTypeSymbol> queryContracts,
        ImmutableArray<INamedTypeSymbol> mutationContracts,
        bool isHandlerCandidate)
    {
        QueryContracts = queryContracts;
        MutationContracts = mutationContracts;
        IsHandlerCandidate = isHandlerCandidate;
    }
}
