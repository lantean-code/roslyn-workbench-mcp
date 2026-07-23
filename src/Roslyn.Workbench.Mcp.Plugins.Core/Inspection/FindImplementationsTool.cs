using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-implementations", "Find Implementations", "Finds implementations of an interface or abstract member.")]
internal sealed class FindImplementationsTool : QueryToolHandler<FindImplementationsRequest, ImplementationSearchData>
{
    protected override async ValueTask<PluginExecutionResult<ImplementationSearchData>> ExecuteCoreAsync(FindImplementationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ImplementationSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
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
        var discoveredImplementations = await SymbolFinder.FindImplementationsAsync(symbol, context.CurrentSolution, projects, cancellationToken);
        var orderedImplementations = discoveredImplementations
            .Distinct(SymbolEqualityComparer.Default)
            .Select(implementation => context.WorkspaceResolver.CreateSymbolReference(implementation))
            .OrderBy(static implementation => implementation.DisplayName, StringComparer.Ordinal);

        var implementations = new List<SymbolReference>();
        var hasMore = false;
        foreach (var implementationReference in orderedImplementations)
        {
            if (implementations.Count == request.EffectiveImplementationsLimit)
            {
                hasMore = true;
                break;
            }

            implementations.Add(implementationReference);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new ImplementationSearchData
        {
            Symbol = symbolReference,
            Implementations = BoundedCollection<SymbolReference>.CreatePrebounded(
                implementations,
                hasMore),
        };

        return PluginExecutionResult<ImplementationSearchData>.Success(data);
    }
}
