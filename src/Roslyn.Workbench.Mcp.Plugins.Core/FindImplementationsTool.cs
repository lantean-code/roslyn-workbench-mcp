using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class FindImplementationsTool : QueryToolHandler<FindImplementationsRequest, ImplementationSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-implementations",
        Title = "Find Implementations",
        Description = "Finds implementations of an interface or abstract member.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindImplementationsTool());
    }

    protected override async ValueTask<PluginExecutionResult<ImplementationSearchData>> ExecuteCoreAsync(FindImplementationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<ImplementationSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var scopeResolution = ToolExecutionHelpers.ResolveProjects<ImplementationSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var projects = scopeResolution.Value.ToImmutableHashSet();
        var implementations = (await SymbolFinder.FindImplementationsAsync(symbol, context.CurrentSolution, projects, cancellationToken).ConfigureAwait(false))
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(implementation => context.Resolver.CreateSymbolReference(implementation).DisplayName, StringComparer.Ordinal)
            .Select(context.Resolver.CreateSymbolReference)
            .ToArray();
        var symbolReference = context.Resolver.CreateSymbolReference(symbol);

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            implementations,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new ImplementationSearchData
            {
                Symbol = symbolReference,
                Implementations = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
