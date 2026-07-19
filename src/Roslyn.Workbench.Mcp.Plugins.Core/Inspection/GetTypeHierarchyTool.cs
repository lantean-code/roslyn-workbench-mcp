using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-type-hierarchy", "Get Type Hierarchy", "Returns base, interface, and optional derived type relationships for a resolved type.")]
internal sealed class GetTypeHierarchyTool : QueryToolHandler<GetTypeHierarchyRequest, TypeHierarchyData>
{
    protected override async ValueTask<PluginExecutionResult<TypeHierarchyData>> ExecuteCoreAsync(GetTypeHierarchyRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.MaxDepth < 1)
        {
            return ToolExecutionHelpers.Rejected<TypeHierarchyData>("InvalidRequest", "MaxDepth must be at least 1.");
        }

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<TypeHierarchyData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return ToolExecutionHelpers.Rejected<TypeHierarchyData>("InvalidRequest", "Get type hierarchy requires a named type symbol.");
        }

        var baseTypes = new List<SymbolReference>();
        for (var current = namedType.BaseType; current is not null && baseTypes.Count < request.MaxDepth; current = current.BaseType)
        {
            baseTypes.Add(context.WorkspaceResolver.CreateSymbolReference(current));
        }

        BoundedCollection<TypeHierarchyNode>? derivedTypes = null;
        if (request.IncludeDerived)
        {
            var derived = (await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, context.CurrentSolution.Projects.ToImmutableHashSet(), cancellationToken))
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
            derivedTypes = ToolExecutionHelpers.CreateBoundedCollection(
                derived,
                ToolExecutionHelpers.GetMaxResults(request.DerivedTypesLimit, GetTypeHierarchyRequest._defaultDerivedTypesMaxResults));
        }

        return PluginExecutionResult<TypeHierarchyData>.Success(new TypeHierarchyData
        {
            Type = context.WorkspaceResolver.CreateSymbolReference(namedType),
            BaseTypes = ToolExecutionHelpers.CreateBoundedCollection(
                baseTypes,
                ToolExecutionHelpers.GetMaxResults(request.BaseTypesLimit, GetTypeHierarchyRequest._defaultBaseTypesMaxResults)),
            Interfaces = ToolExecutionHelpers.CreateBoundedCollection(
                namedType.AllInterfaces
                    .OrderBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
                    .Select(context.WorkspaceResolver.CreateSymbolReference)
                    .ToArray(),
                ToolExecutionHelpers.GetMaxResults(request.InterfacesLimit, GetTypeHierarchyRequest._defaultInterfacesMaxResults)),
            DerivedTypes = derivedTypes,
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
