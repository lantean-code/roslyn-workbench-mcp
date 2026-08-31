using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns symbols that directly depend on a resolved symbol.
/// </summary>
[RoslynTool("get-symbol-dependents", "Get Symbol Dependents", "Returns symbols that directly depend on a resolved symbol.")]
internal sealed class GetSymbolDependentsTool : QueryToolHandler<GetSymbolDependentsRequest, SymbolDependentsData>
{
    /// <inheritdoc/>
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
        var semanticModels = new Dictionary<DocumentId, SemanticModel?>();
        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations)
            {
                if (!location.Location.IsInSource || location.Document is null)
                {
                    continue;
                }

                if (!semanticModels.TryGetValue(location.Document.Id, out var semanticModel))
                {
                    semanticModel = await location.Document.GetSemanticModelAsync(cancellationToken);
                    semanticModels.Add(location.Document.Id, semanticModel);
                }

                var containingSymbol = semanticModel?.GetEnclosingSymbol(location.Location.SourceSpan.Start, cancellationToken);
                if (containingSymbol is null || SymbolEqualityComparer.Default.Equals(containingSymbol, symbol))
                {
                    continue;
                }

                dependents.Add(containingSymbol);
            }
        }

        var orderedDependents = dependents
            .Select(item => context.WorkspaceResolver.CreateSymbolReference(item))
            .OrderBy(static item => item.DisplayName, StringComparer.Ordinal);

        var projectedDependents = new List<SymbolReference>();
        foreach (var dependentReference in orderedDependents)
        {
            if (projectedDependents.Count == request.EffectiveDependentsLimit)
            {
                break;
            }

            projectedDependents.Add(dependentReference);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new SymbolDependentsData
        {
            Symbol = symbolReference,
            Dependents = BoundedCollection.CreatePrebounded(projectedDependents, dependents.Count),
        };

        return PluginExecutionResult.Success(data);
    }
}
