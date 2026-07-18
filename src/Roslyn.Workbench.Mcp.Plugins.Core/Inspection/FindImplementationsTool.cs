using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-implementations", "Find Implementations", "Finds implementations of an interface or abstract member.")]
internal sealed class FindImplementationsTool : QueryToolHandler<FindImplementationsRequest, ImplementationSearchData>
{
    protected override async ValueTask<PluginExecutionResult<ImplementationSearchData>> ExecuteCoreAsync(FindImplementationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ImplementationSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<ImplementationSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var projects = scopeResolution.Value.ToImmutableHashSet();
        var implementations = (await SymbolFinder.FindImplementationsAsync(symbol, context.CurrentSolution, projects, cancellationToken).ConfigureAwait(false))
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(implementation => context.WorkspaceResolver.CreateSymbolReference(implementation).DisplayName, StringComparer.Ordinal)
            .Select(context.WorkspaceResolver.CreateSymbolReference)
            .ToArray();
        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        return PluginExecutionResult<ImplementationSearchData>.Success(new ImplementationSearchData
        {
            Symbol = symbolReference,
            Implementations = ToolExecutionHelpers.CreateBoundedCollection(
                implementations,
                ToolExecutionHelpers.GetMaxResults(context, request.ImplementationsLimit)),
        });
    }
}
