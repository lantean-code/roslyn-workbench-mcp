using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Describes the closed query and mutation contracts implemented by a possible plugin handler.
/// </summary>
internal sealed class PluginHandlerContractSet
{
    /// <summary>
    /// Gets the closed query-handler contracts implemented by the type.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> QueryContracts { get; }

    /// <summary>
    /// Gets the closed mutation-handler contracts implemented by the type.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> MutationContracts { get; }

    /// <summary>
    /// Gets a value indicating whether the type implements any handler marker or closed handler contract.
    /// </summary>
    public bool IsHandlerCandidate { get; }

    /// <summary>
    /// Gets a value indicating whether the type implements exactly one closed handler contract from exactly one handler family.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginHandlerContractSet"/> class.
    /// </summary>
    /// <param name="queryContracts">The implemented closed query-handler contracts.</param>
    /// <param name="mutationContracts">The implemented closed mutation-handler contracts.</param>
    /// <param name="isHandlerCandidate">Whether the type implements a handler marker or contract.</param>
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
