using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class FindOverridesTool : QueryToolHandler<FindOverridesRequest, OverrideSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-overrides",
        Title = "Find Overrides",
        Description = "Finds overrides of a virtual or abstract member.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindOverridesTool());
    }

    protected override async ValueTask<PluginExecutionResult<OverrideSearchData>> ExecuteCoreAsync(FindOverridesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
