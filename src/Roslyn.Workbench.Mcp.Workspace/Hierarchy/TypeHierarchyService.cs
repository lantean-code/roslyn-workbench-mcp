using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.Hierarchy;

/// <summary>
/// Finds source types derived from a symbol within a selected set of projects.
/// </summary>
internal sealed class TypeHierarchyService : ITypeHierarchyService
{
    /// <summary>
    /// Finds distinct types derived from the supplied root symbol.
    /// </summary>
    /// <param name="root">The base type whose derived types should be found.</param>
    /// <param name="solution">The solution to search.</param>
    /// <param name="projects">The projects included in the selected workspace scope.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the distinct derived types within the selected projects.</returns>
    public async ValueTask<IReadOnlyList<TypeHierarchyMatch>> FindDerivedTypesAsync(
        INamedTypeSymbol root,
        Solution solution,
        IReadOnlyCollection<Project> projects,
        CancellationToken cancellationToken)
    {
        var projectSet = projects.ToImmutableHashSet();
        IEnumerable<INamedTypeSymbol> discoveredTypes;
        if (root.TypeKind == TypeKind.Interface)
        {
            var derivedInterfaces = await SymbolFinder.FindDerivedInterfacesAsync(
                root,
                solution,
                transitive: true,
                projectSet,
                cancellationToken);
            var implementations = await SymbolFinder.FindImplementationsAsync(
                root,
                solution,
                projectSet,
                cancellationToken);
            discoveredTypes = derivedInterfaces.Concat(implementations.OfType<INamedTypeSymbol>());
        }
        else
        {
            discoveredTypes = await SymbolFinder.FindDerivedClassesAsync(
                root,
                solution,
                projectSet,
                cancellationToken);
        }

        var matches = new List<TypeHierarchyMatch>();
        var rootDefinition = root.OriginalDefinition;
        var uniqueTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var discoveredType in discoveredTypes)
        {
            if (!uniqueTypes.Add(discoveredType))
            {
                continue;
            }

            var depth = GetDistance(discoveredType, rootDefinition);
            if (depth != int.MaxValue)
            {
                matches.Add(new TypeHierarchyMatch
                {
                    Type = discoveredType,
                    Depth = depth,
                });
            }
        }

        return matches;
    }

    private static int GetDistance(INamedTypeSymbol symbol, INamedTypeSymbol rootDefinition)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default)
        {
            symbol,
        };
        var pending = new Queue<(INamedTypeSymbol Type, int Depth)>();
        pending.Enqueue((symbol, 0));

        while (pending.Count > 0)
        {
            var (current, depth) = pending.Dequeue();
            foreach (var parent in GetDirectParents(current))
            {
                if (RepresentsTypeDefinition(parent, rootDefinition))
                {
                    return depth + 1;
                }

                if (visited.Add(parent))
                {
                    pending.Enqueue((parent, depth + 1));
                }
            }
        }

        return int.MaxValue;
    }

    private static bool RepresentsTypeDefinition(INamedTypeSymbol symbol, INamedTypeSymbol definition)
    {
        return SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, definition);
    }

    private static IEnumerable<INamedTypeSymbol> GetDirectParents(INamedTypeSymbol symbol)
    {
        if (symbol.BaseType is not null)
        {
            yield return symbol.BaseType;
        }

        foreach (var interfaceSymbol in symbol.Interfaces)
        {
            yield return interfaceSymbol;
        }
    }
}
