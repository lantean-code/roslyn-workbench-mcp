using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetSymbolDependentsTool : QueryToolHandler<GetSymbolDependentsRequest, SymbolDependentsData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-symbol-dependents",
        Title = "Get Symbol Dependents",
        Description = "Returns symbols that directly depend on a resolved symbol.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetSymbolDependentsTool());
    }

    protected override async ValueTask<PluginExecutionResult<SymbolDependentsData>> ExecuteCoreAsync(GetSymbolDependentsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolDependentsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(symbol, context.CurrentSolution, documents.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false);
        var dependents = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations.Where(static item => item.Location.IsInSource))
            {
                if (location.Document is null)
                {
                    continue;
                }

                var containingSymbol = await GetEnclosingSymbolAsync(location.Document, location.Location.SourceSpan.Start, cancellationToken).ConfigureAwait(false);
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
                ToolExecutionHelpers.GetMaxResults(context, request.DependentsLimit)),
        });
    }

    private static async ValueTask<ISymbol?> GetEnclosingSymbolAsync(Document document, int position, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        return semanticModel?.GetEnclosingSymbol(position, cancellationToken);
    }
}
