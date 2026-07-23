using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-derived-types", "Find Derived Types", "Finds derived types for a resolved base type.")]
internal sealed class FindDerivedTypesTool : QueryToolHandler<FindDerivedTypesRequest, DerivedTypesData>
{
    protected override async ValueTask<PluginExecutionResult<DerivedTypesData>> ExecuteCoreAsync(FindDerivedTypesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.MaxDepth < 1)
        {
            return PluginExecutionResultFactory.Rejected<DerivedTypesData>("InvalidRequest", "MaxDepth must be at least 1.");
        }

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DerivedTypesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResultFactory.Rejected<DerivedTypesData>("InvalidRequest", "Find derived types requires a named type symbol.");
        }

        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<DerivedTypesData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var discoveredTypes = await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, scopeResolution.Value.ToImmutableHashSet(), cancellationToken);
        var uniqueTypes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var projectedTypes = new List<(INamedTypeSymbol Symbol, SymbolReference Reference)>();
        foreach (var discoveredType in discoveredTypes)
        {
            if (uniqueTypes.Add(discoveredType))
            {
                var reference = context.WorkspaceResolver.CreateSymbolReference(discoveredType);
                projectedTypes.Add((discoveredType, reference));
            }
        }

        var orderedTypes = projectedTypes.OrderBy(static item => item.Reference.DisplayName, StringComparer.Ordinal);

        var derivedTypes = new List<TypeHierarchyNode>();
        var hasMore = false;
        foreach (var (symbol, reference) in orderedTypes)
        {
            var depth = GetTypeDepth(symbol, namedType);
            if (depth > request.MaxDepth)
            {
                continue;
            }

            if (derivedTypes.Count == request.EffectiveDerivedTypesLimit)
            {
                hasMore = true;
                break;
            }

            derivedTypes.Add(new TypeHierarchyNode
            {
                Type = reference,
                Depth = depth,
            });
        }

        var baseType = context.WorkspaceResolver.CreateSymbolReference(namedType);
        var data = new DerivedTypesData
        {
            BaseType = baseType,
            DerivedTypes = BoundedCollection<TypeHierarchyNode>.CreatePrebounded(derivedTypes, hasMore),
        };

        return PluginExecutionResult<DerivedTypesData>.Success(data);
    }

    private static async ValueTask<IReadOnlyList<INamedTypeSymbol>> FindDerivedTypeSymbolsAsync(INamedTypeSymbol root, Solution solution, IImmutableSet<Project> projects, CancellationToken cancellationToken)
    {
        if (root.TypeKind == TypeKind.Interface)
        {
            return (await SymbolFinder.FindImplementationsAsync(root, solution, projects, cancellationToken))
                .OfType<INamedTypeSymbol>()
                .ToArray();
        }

        return (await SymbolFinder.FindDerivedClassesAsync(root, solution, projects, cancellationToken))
            .ToArray();
    }

    private static int GetTypeDepth(INamedTypeSymbol symbol, INamedTypeSymbol root)
    {
        var depth = 0;
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
            if (SymbolEqualityComparer.Default.Equals(current, root))
            {
                return depth;
            }
        }

        return depth;
    }
}
