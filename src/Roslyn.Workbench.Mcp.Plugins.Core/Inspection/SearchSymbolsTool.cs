namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("search-symbols", "Search Symbols", "Searches declarations by name, metadata name and optional semantic filters.")]
internal sealed class SearchSymbolsTool : QueryToolHandler<SearchSymbolsRequest, SymbolSearchData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolSearchData>> ExecuteCoreAsync(SearchSymbolsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<SymbolSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        if (string.IsNullOrWhiteSpace(request.Query) && string.IsNullOrWhiteSpace(request.MetadataName))
        {
            return ToolExecutionHelpers.Rejected<SymbolSearchData>("InvalidRequest", "Search symbols requires query or metadataName.");
        }

        var pattern = request.Query ?? request.MetadataName
            ?? throw new InvalidOperationException("A validated symbol search must contain a query or metadata name.");
        var matchedSymbols = new List<ISymbol>();
        foreach (var project in scopeResolution.Value)
        {
            var declarations = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(project, pattern, SymbolFilter.TypeAndMember, cancellationToken);
            matchedSymbols.AddRange(declarations.Where(symbol => MatchesSymbolFilters(symbol, request)));
        }

        var symbols = matchedSymbols
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(symbol => context.WorkspaceResolver.CreateSymbolReference(symbol).DisplayName, StringComparer.Ordinal)
            .ThenBy(symbol => context.WorkspaceResolver.CreateSymbolReference(symbol).Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .Select(context.WorkspaceResolver.CreateSymbolReference)
            .ToArray();

        return PluginExecutionResult<SymbolSearchData>.Success(new SymbolSearchData
        {
            Symbols = ToolExecutionHelpers.CreateBoundedCollection(
                symbols,
                ToolExecutionHelpers.GetMaxResults(context, request.SymbolsLimit)),
        });
    }

    private static bool MatchesSymbolFilters(ISymbol symbol, SearchSymbolsRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.MetadataName)
            && !string.Equals(symbol.MetadataName, request.MetadataName, StringComparison.Ordinal)
            && !symbol.MetadataName.Contains(request.MetadataName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.Kinds is not null && request.Kinds.Count > 0 && !request.Kinds.Contains(symbol.Kind.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.Accessibilities is not null
            && request.Accessibilities.Count > 0
            && !request.Accessibilities.Contains(symbol.DeclaredAccessibility.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Namespace))
        {
            var symbolNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (!string.Equals(symbolNamespace, request.Namespace, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
