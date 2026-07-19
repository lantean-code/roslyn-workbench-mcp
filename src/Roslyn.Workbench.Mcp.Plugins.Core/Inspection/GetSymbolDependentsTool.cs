using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-symbol-dependents", "Get Symbol Dependents", "Returns symbols that directly depend on a resolved symbol.")]
internal sealed class GetSymbolDependentsTool : QueryToolHandler<GetSymbolDependentsRequest, SymbolDependentsData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolDependentsData>> ExecuteCoreAsync(GetSymbolDependentsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolDependentsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<SymbolDependentsData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(symbol, context.CurrentSolution, documents.Value.ToImmutableHashSet(), cancellationToken);
        var dependents = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations.Where(static item => item.Location.IsInSource))
            {
                if (location.Document is null)
                {
                    continue;
                }

                var containingSymbol = await GetEnclosingSymbolAsync(location.Document, location.Location.SourceSpan.Start, cancellationToken);
                if (containingSymbol is null || SymbolEqualityComparer.Default.Equals(containingSymbol, symbol))
                {
                    continue;
                }

                dependents.Add(containingSymbol);
            }
        }

        var orderedDependents = dependents
            .OrderBy(item => context.WorkspaceResolver.CreateSymbolReference(item).DisplayName, StringComparer.Ordinal)
            .Select(context.WorkspaceResolver.CreateSymbolReference)
            .ToArray();

        return PluginExecutionResult<SymbolDependentsData>.Success(new SymbolDependentsData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Dependents = ToolExecutionHelpers.CreateBoundedCollection(
                orderedDependents,
                request.EffectiveDependentsLimit),
        });
    }

    private static async ValueTask<ISymbol?> GetEnclosingSymbolAsync(Document document, int position, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        return semanticModel?.GetEnclosingSymbol(position, cancellationToken);
    }
}
