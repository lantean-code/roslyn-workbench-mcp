using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool(_toolName, "Search Symbols", "Searches declarations by name, metadata name and optional semantic filters.")]
internal sealed class SearchSymbolsTool : QueryToolHandler<SearchSymbolsRequest, SymbolSearchData>
{
    private const string _toolName = "search-symbols";

    protected override async ValueTask<PluginExecutionResult<SymbolSearchData>> ExecuteCoreAsync(SearchSymbolsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        Document? scopedDocument = null;
        IReadOnlyList<Project> scopedProjects = [];

        if (request.Scope?.Kind == ScopeKind.Document)
        {
            var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<SymbolSearchData>(request.Scope.Document, context);
            if (documentResolution.HasRejection)
            {
                return documentResolution.Rejection;
            }

            scopedDocument = documentResolution.Value;
            scopedProjects = [scopedDocument.Project];
        }
        else if (request.Scope is not null && request.Scope.Kind != ScopeKind.Solution)
        {
            var projectResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<SymbolSearchData>(request.Scope, context);
            if (projectResolution.HasRejection)
            {
                return projectResolution.Rejection;
            }

            scopedProjects = projectResolution.Value;
        }

        var pattern = !string.IsNullOrWhiteSpace(request.Query)
            ? request.Query
            : request.MetadataName;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return PluginExecutionResult.Rejected<SymbolSearchData>("InvalidRequest", "Search symbols requires query or metadataName.");
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
            if (request.Scope is null || request.Scope.Kind == ScopeKind.Solution)
            {
                var declarations = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(
                    context.CurrentSolution,
                    pattern,
                    SymbolFilter.TypeAndMember,
                    cancellationToken);

                AddMatchingSymbols(
                    declarations,
                    request,
                    requestedKinds,
                    requestedAccessibilities,
                    scopedDocument,
                    context.CurrentSolution,
                    matchedSymbols);
            }
            else
            {
                foreach (var project in scopedProjects)
                {
                    var declarations = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(
                        project,
                        pattern,
                        SymbolFilter.TypeAndMember,
                        cancellationToken);

                    AddMatchingSymbols(
                        declarations,
                        request,
                        requestedKinds,
                        requestedAccessibilities,
                        scopedDocument,
                        context.CurrentSolution,
                        matchedSymbols);
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
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ResultSelectionPhase))
        {
            foreach (var symbolReference in orderedSymbols)
            {
                if (symbols.Count == request.EffectiveSymbolsLimit)
                {
                    break;
                }

                symbols.Add(symbolReference);
            }
        }

        var data = new SymbolSearchData
        {
            Symbols = BoundedCollection.CreatePrebounded(symbols, orderedSymbols.Length),
        };

        return PluginExecutionResult.Success(data);
    }

    private static void AddMatchingSymbols(
        IEnumerable<ISymbol> declarations,
        SearchSymbolsRequest request,
        HashSet<string>? requestedKinds,
        HashSet<string>? requestedAccessibilities,
        Document? scopedDocument,
        Solution solution,
        HashSet<ISymbol> matchedSymbols)
    {
        foreach (var symbol in declarations)
        {
            if (scopedDocument is not null && !IsDeclaredInDocument(symbol, scopedDocument, solution))
            {
                continue;
            }

            if (MatchesSymbolFilters(symbol, request, requestedKinds, requestedAccessibilities))
            {
                matchedSymbols.Add(symbol);
            }
        }
    }

    private static bool IsDeclaredInDocument(ISymbol symbol, Document scopedDocument, Solution solution)
    {
        foreach (var declaration in symbol.DeclaringSyntaxReferences)
        {
            var declaringDocument = solution.GetDocument(declaration.SyntaxTree);
            if (declaringDocument?.Id == scopedDocument.Id)
            {
                return true;
            }
        }

        return false;
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
