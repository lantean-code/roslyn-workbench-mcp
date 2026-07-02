using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetTypeHierarchyTool : QueryToolHandler<GetTypeHierarchyRequest, TypeHierarchyData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-type-hierarchy",
        Title = "Get Type Hierarchy",
        Description = "Returns base, interface, and optional derived type relationships for a resolved type.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetTypeHierarchyTool());
    }

    protected override async ValueTask<PluginExecutionResult<TypeHierarchyData>> ExecuteCoreAsync(GetTypeHierarchyRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<TypeHierarchyData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return ToolExecutionHelpers.Rejected<TypeHierarchyData>("InvalidRequest", "Get type hierarchy requires a named type symbol.");
        }

        var baseTypes = new List<SymbolReference>();
        for (var current = namedType.BaseType; current is not null; current = current.BaseType)
        {
            baseTypes.Add(context.Resolver.CreateSymbolReference(current));
        }

        IReadOnlyList<TypeHierarchyNode>? derivedTypes = null;
        int? returnedCount = null;
        bool? hasMore = null;
        if (request.IncludeDerived)
        {
            var derived = (await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, context.CurrentSolution.Projects.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false))
                .Distinct(SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>()
                .OrderBy(symbol => context.Resolver.CreateSymbolReference(symbol).DisplayName, StringComparer.Ordinal)
                .Select(symbol => new TypeHierarchyNode
                {
                    Type = context.Resolver.CreateSymbolReference(symbol),
                    Depth = GetTypeDepth(symbol, namedType),
                })
                .ToArray();
            derivedTypes = ApplyLimit(derived, ToolExecutionHelpers.GetMaxResults(context, request.Limit), out var derivedHasMore);
            returnedCount = derivedTypes.Count;
            hasMore = derivedHasMore;
        }

        return ToolExecutionHelpers.EnsureWithinSize(context, new TypeHierarchyData
        {
            Type = context.Resolver.CreateSymbolReference(namedType),
            BaseTypes = baseTypes,
            Interfaces = namedType.AllInterfaces
                .OrderBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
                .Select(context.Resolver.CreateSymbolReference)
                .ToArray(),
            DerivedTypes = derivedTypes,
            ReturnedCount = returnedCount,
            HasMore = hasMore,
        });
    }

    private static IReadOnlyList<T> ApplyLimit<T>(IReadOnlyList<T> items, int maxResults, out bool hasMore)
    {
        hasMore = items.Count > maxResults;
        return hasMore ? items.Take(maxResults).ToArray() : items;
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
