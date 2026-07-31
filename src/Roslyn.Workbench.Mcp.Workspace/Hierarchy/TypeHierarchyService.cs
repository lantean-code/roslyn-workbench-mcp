using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.Hierarchy;

internal sealed class TypeHierarchyService : ITypeHierarchyService
{
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
        var uniqueTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var discoveredType in discoveredTypes)
        {
            if (!uniqueTypes.Add(discoveredType))
            {
                continue;
            }

            var depth = GetDistance(discoveredType, root);
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

    private static int GetDistance(INamedTypeSymbol symbol, INamedTypeSymbol root)
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
                if (SymbolEqualityComparer.Default.Equals(parent, root))
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
