using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-overrides", "Find Overrides", "Finds overrides of a virtual or abstract member.")]
internal sealed class FindOverridesTool : QueryToolHandler<FindOverridesRequest, OverrideSearchData>
{
    protected override async ValueTask<PluginExecutionResult<OverrideSearchData>> ExecuteCoreAsync(FindOverridesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<OverrideSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        if (symbol is not IMethodSymbol and not IPropertySymbol and not IEventSymbol)
        {
            return ToolExecutionHelpers.Rejected<OverrideSearchData>("InvalidRequest", "Find overrides requires a virtual, abstract, property, or event member symbol.");
        }

        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<OverrideSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var overrides = (await SymbolFinder.FindOverridesAsync(symbol, context.CurrentSolution, scopeResolution.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false))
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(item => context.WorkspaceResolver.CreateSymbolReference(item).DisplayName, StringComparer.Ordinal)
            .Select(context.WorkspaceResolver.CreateSymbolReference)
            .ToArray();

        return PluginExecutionResult<OverrideSearchData>.Success(new OverrideSearchData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Overrides = ToolExecutionHelpers.CreateBoundedCollection(
                overrides,
                ToolExecutionHelpers.GetMaxResults(context, request.OverridesLimit)),
        });
    }
}
