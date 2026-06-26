using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class SearchSymbolsTool : QueryToolHandler<SearchSymbolsRequest, SymbolSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "search-symbols",
        Title = "Search Symbols",
        Description = "Searches declarations by name, metadata name and optional semantic filters.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new SearchSymbolsTool());
    }

    protected override async ValueTask<PluginExecutionResult<SymbolSearchData>> ExecuteCoreAsync(SearchSymbolsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scopeResolution = ToolExecutionHelpers.ResolveProjects<SymbolSearchData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        if (string.IsNullOrWhiteSpace(request.Query) && string.IsNullOrWhiteSpace(request.MetadataName))
        {
            return ToolExecutionHelpers.Rejected<SymbolSearchData>("InvalidRequest", "Search symbols requires query or metadataName.");
        }

        var pattern = request.Query ?? request.MetadataName!;
        var matchedSymbols = new List<ISymbol>();
        foreach (var project in scopeResolution.Value)
        {
            var declarations = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(project, pattern, SymbolFilter.TypeAndMember, cancellationToken).ConfigureAwait(false);
            matchedSymbols.AddRange(declarations.Where(symbol => MatchesSymbolFilters(symbol, request)));
        }

        var symbols = matchedSymbols
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(symbol => context.Resolver.CreateSymbolReference(symbol).DisplayName, StringComparer.Ordinal)
            .ThenBy(symbol => context.Resolver.CreateSymbolReference(symbol).Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .Select(context.Resolver.CreateSymbolReference)
            .ToArray();

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            symbols,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            static (items, hasMore) => new SymbolSearchData
            {
                Symbols = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
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
