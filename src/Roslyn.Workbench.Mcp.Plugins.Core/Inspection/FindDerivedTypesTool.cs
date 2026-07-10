using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class FindDerivedTypesTool : QueryToolHandler<FindDerivedTypesRequest, DerivedTypesData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-derived-types",
        Title = "Find Derived Types",
        Description = "Finds derived types for a resolved base type.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindDerivedTypesTool());
    }

    protected override async ValueTask<PluginExecutionResult<DerivedTypesData>> ExecuteCoreAsync(FindDerivedTypesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DerivedTypesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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

        var derivedTypes = (await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, scopeResolution.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false))
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .OrderBy(symbol => context.WorkspaceResolver.CreateSymbolReference(symbol).DisplayName, StringComparer.Ordinal)
            .Select(symbol => new TypeHierarchyNode
            {
                Type = context.WorkspaceResolver.CreateSymbolReference(symbol),
                Depth = GetTypeDepth(symbol, namedType),
            })
            .ToArray();

        return PluginExecutionResult<DerivedTypesData>.Success(new DerivedTypesData
        {
            BaseType = context.WorkspaceResolver.CreateSymbolReference(namedType),
            DerivedTypes = ToolExecutionHelpers.CreateBoundedCollection(
                derivedTypes,
                ToolExecutionHelpers.GetMaxResults(context, request.DerivedTypesLimit)),
        });
    }

    private static async ValueTask<IReadOnlyList<INamedTypeSymbol>> FindDerivedTypeSymbolsAsync(INamedTypeSymbol root, Solution solution, IImmutableSet<Project> projects, CancellationToken cancellationToken)
    {
        if (root.TypeKind == TypeKind.Interface)
        {
            return (await SymbolFinder.FindImplementationsAsync(root, solution, projects, cancellationToken).ConfigureAwait(false))
                .OfType<INamedTypeSymbol>()
                .ToArray();
        }

        return (await SymbolFinder.FindDerivedClassesAsync(root, solution, projects, cancellationToken).ConfigureAwait(false))
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
