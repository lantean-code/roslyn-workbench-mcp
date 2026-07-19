using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-derived-types", "Find Derived Types", "Finds derived types for a resolved base type.")]
internal sealed class FindDerivedTypesTool : QueryToolHandler<FindDerivedTypesRequest, DerivedTypesData>
{
    protected override async ValueTask<PluginExecutionResult<DerivedTypesData>> ExecuteCoreAsync(FindDerivedTypesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.MaxDepth < 1)
        {
            return ToolExecutionHelpers.Rejected<DerivedTypesData>("InvalidRequest", "MaxDepth must be at least 1.");
        }

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DerivedTypesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return ToolExecutionHelpers.Rejected<DerivedTypesData>("InvalidRequest", "Find derived types requires a named type symbol.");
        }

        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<DerivedTypesData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var derivedTypes = (await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, scopeResolution.Value.ToImmutableHashSet(), cancellationToken))
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .OrderBy(symbol => context.WorkspaceResolver.CreateSymbolReference(symbol).DisplayName, StringComparer.Ordinal)
            .Select(symbol => new TypeHierarchyNode
            {
                Type = context.WorkspaceResolver.CreateSymbolReference(symbol),
                Depth = GetTypeDepth(symbol, namedType),
            })
            .Where(node => node.Depth <= request.MaxDepth)
            .ToArray();

        return PluginExecutionResult<DerivedTypesData>.Success(new DerivedTypesData
        {
            BaseType = context.WorkspaceResolver.CreateSymbolReference(namedType),
            DerivedTypes = ToolExecutionHelpers.CreateBoundedCollection(
                derivedTypes,
                ToolExecutionHelpers.GetMaxResults(request.DerivedTypesLimit, FindDerivedTypesRequest._defaultDerivedTypesMaxResults)),
        });
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
