using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool(_toolName, "Search Symbols", "Searches declarations by name, metadata name and optional semantic filters.")]
internal sealed class SearchSymbolsTool : QueryToolHandler<SearchSymbolsRequest, SymbolSearchData>
{
    private const string _toolName = "search-symbols";

    protected override async ValueTask<PluginExecutionResult<SymbolSearchData>> ExecuteCoreAsync(SearchSymbolsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<SymbolSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var pattern = !string.IsNullOrWhiteSpace(request.Query)
            ? request.Query
            : request.MetadataName;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return PluginExecutionResultFactory.Rejected<SymbolSearchData>("InvalidRequest", "Search symbols requires query or metadataName.");
        }

        var requestedKinds = request.Kinds is { Count: > 0 }
            ? new HashSet<string>(request.Kinds, StringComparer.OrdinalIgnoreCase)
            : null;

        var requestedAccessibilities = request.Accessibilities is { Count: > 0 }
            ? new HashSet<string>(request.Accessibilities, StringComparer.OrdinalIgnoreCase)
            : null;

        var matchedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.DiscoveryPhase))
        {
            foreach (var project in scopeResolution.Value)
            {
                var declarations = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(project, pattern, SymbolFilter.TypeAndMember, cancellationToken);
                foreach (var symbol in declarations)
                {
                    if (MatchesSymbolFilters(symbol, request, requestedKinds, requestedAccessibilities))
                    {
                        matchedSymbols.Add(symbol);
                    }
                }
            }
        }

        SymbolReference[] orderedSymbols;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.CandidateProjectionPhase))
        {
            var symbolReferences = new List<SymbolReference>();
            foreach (var matchedSymbol in matchedSymbols)
            {
                symbolReferences.Add(context.WorkspaceResolver.CreateSymbolReference(matchedSymbol));
            }

            orderedSymbols = symbolReferences
                .OrderBy(static symbol => symbol.DisplayName, StringComparer.Ordinal)
                .ThenBy(static symbol => symbol.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        var symbols = new List<SymbolReference>();
        var hasMore = false;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ResultSelectionPhase))
        {
            foreach (var symbolReference in orderedSymbols)
            {
                if (symbols.Count == request.EffectiveSymbolsLimit)
                {
                    hasMore = true;
                    break;
                }

                symbols.Add(symbolReference);
            }
        }

        var data = new SymbolSearchData
        {
            Symbols = BoundedCollection<SymbolReference>.CreatePrebounded(symbols, hasMore),
        };

        return PluginExecutionResult<SymbolSearchData>.Success(data);
    }

    private static bool MatchesSymbolFilters(
        ISymbol symbol,
        SearchSymbolsRequest request,
        HashSet<string>? requestedKinds,
        HashSet<string>? requestedAccessibilities)
    {
        if (!string.IsNullOrWhiteSpace(request.MetadataName)
            && !string.Equals(symbol.MetadataName, request.MetadataName, StringComparison.Ordinal)
            && !symbol.MetadataName.Contains(request.MetadataName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requestedKinds is not null && !requestedKinds.Contains(symbol.Kind.ToString()))
        {
            return false;
        }

        if (requestedAccessibilities is not null && !requestedAccessibilities.Contains(symbol.DeclaredAccessibility.ToString()))
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
