using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-overrides", "Find Overrides", "Finds overrides of a virtual or abstract member.")]
internal sealed class FindOverridesTool : QueryToolHandler<FindOverridesRequest, OverrideSearchData>
{
    protected override async ValueTask<PluginExecutionResult<OverrideSearchData>> ExecuteCoreAsync(FindOverridesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<OverrideSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        if (symbol is not IMethodSymbol and not IPropertySymbol and not IEventSymbol)
        {
            return PluginExecutionResultFactory.Rejected<OverrideSearchData>("InvalidRequest", "Find overrides requires a virtual, abstract, property, or event member symbol.");
        }

        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<OverrideSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var discoveredOverrides = await SymbolFinder.FindOverridesAsync(symbol, context.CurrentSolution, scopeResolution.Value.ToImmutableHashSet(), cancellationToken);
        var orderedOverrides = discoveredOverrides
            .Distinct(SymbolEqualityComparer.Default)
            .Select(item => context.WorkspaceResolver.CreateSymbolReference(item))
            .OrderBy(static item => item.DisplayName, StringComparer.Ordinal);

        var overrides = new List<SymbolReference>();
        var hasMore = false;
        foreach (var overrideReference in orderedOverrides)
        {
            if (overrides.Count == request.EffectiveOverridesLimit)
            {
                hasMore = true;
                break;
            }

            overrides.Add(overrideReference);
        }

        var data = new OverrideSearchData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Overrides = BoundedCollection<SymbolReference>.CreatePrebounded(
                overrides,
                hasMore),
        };

        return PluginExecutionResult<OverrideSearchData>.Success(data);
    }
}
