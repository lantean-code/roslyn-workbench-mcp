using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Plugins.Core.Inspection.Caching;
using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

using ContractReferenceLocation = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.ReferenceLocation;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool(_toolName, "Find References", "Finds source references, optionally including declarations and access classification.")]
internal sealed class FindReferencesTool : QueryToolHandler<FindReferencesRequest, ReferenceSearchData>
{
    private const string _toolName = "find-references";

    protected override async ValueTask<PluginExecutionResult<ReferenceSearchData>> ExecuteCoreAsync(FindReferencesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ReferenceSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<ReferenceSearchData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var referencedSymbols = await GetReferencedSymbolsAsync(symbol, documents.Value, context, cancellationToken);

        var pendingReferences = new List<PendingReference>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.CandidateProjectionPhase))
        {
            foreach (var referencedSymbol in referencedSymbols)
            {
                if (request.IncludeDefinitions)
                {
                    foreach (var definitionLocation in referencedSymbol.Definition.Locations)
                    {
                        if (!definitionLocation.IsInSource
                            || context.WorkspaceResolver.CreateResolvedLocation(definitionLocation) is not { } resolvedLocation)
                        {
                            continue;
                        }

                        pendingReferences.Add(new PendingReference
                        {
                            Location = definitionLocation,
                            ResolvedLocation = resolvedLocation,
                            DefinitionSymbol = referencedSymbol.Definition,
                            IsDefinition = true,
                        });
                    }
                }

                foreach (var reference in referencedSymbol.Locations)
                {
                    if (!reference.Location.IsInSource
                        || context.WorkspaceResolver.CreateResolvedLocation(reference.Location) is not { } resolvedLocation)
                    {
                        continue;
                    }

                    pendingReferences.Add(new PendingReference
                    {
                        Location = reference.Location,
                        ResolvedLocation = resolvedLocation,
                        DefinitionSymbol = referencedSymbol.Definition,
                        Document = reference.Document,
                    });
                }
            }
        }

        var maxResults = request.EffectiveReferencesLimit;
        var selectedReferences = new List<PendingReference>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ResultSelectionPhase))
        {
            var orderedReferences = pendingReferences
                .OrderBy(static reference => reference.ResolvedLocation.Document?.Path, StringComparer.Ordinal)
                .ThenBy(static reference => reference.ResolvedLocation.Span?.Start)
                .ThenBy(static reference => reference.ResolvedLocation.Document?.ProjectId, StringComparer.Ordinal)
                .ThenBy(static reference => reference.ResolvedLocation.Document?.DocumentId, StringComparer.Ordinal)
                .ThenBy(static reference => reference.ResolvedLocation.Span?.Length)
                .ThenBy(static reference => reference.IsDefinition);

            foreach (var reference in orderedReferences)
            {
                if (selectedReferences.Count == maxResults)
                {
                    break;
                }

                selectedReferences.Add(reference);
            }
        }

        var references = new List<ContractReferenceLocation>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ResultEnrichmentPhase))
        {
            foreach (var pendingReference in selectedReferences)
            {
                if (pendingReference.IsDefinition)
                {
                    references.Add(new ContractReferenceLocation
                    {
                        Location = pendingReference.ResolvedLocation,
                        ContainingSymbol = context.WorkspaceResolver.CreateSymbolReference(pendingReference.DefinitionSymbol),
                        IsDefinition = true,
                    });

                    continue;
                }

                ISymbol? containingSymbol = null;
                if (pendingReference.Document is not null)
                {
                    containingSymbol = await context.ToolExecutionServices.InspectionContextService.TryCreateContainingSymbolAsync(
                        pendingReference.Document,
                        pendingReference.Location.SourceSpan.Start,
                        cancellationToken);
                }

                string? contextLine = null;
                if (request.IncludeContext)
                {
                    contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(
                        pendingReference.Document,
                        pendingReference.Location.SourceSpan,
                        cancellationToken);
                }

                references.Add(new ContractReferenceLocation
                {
                    Location = pendingReference.ResolvedLocation,
                    ContainingSymbol = containingSymbol is null ? null : context.WorkspaceResolver.CreateSymbolReference(containingSymbol),
                    IsWrite = await IsWriteReferenceAsync(pendingReference.Document, pendingReference.Location, cancellationToken),
                    Context = contextLine,
                });
            }
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        var data = new ReferenceSearchData
        {
            Symbol = symbolReference,
            References = BoundedCollection<ContractReferenceLocation>.CreatePrebounded(
                references,
                pendingReferences.Count),
        };

        return PluginExecutionResult<ReferenceSearchData>.Success(data);
    }

    private static async Task<ImmutableArray<ReferencedSymbol>> GetReferencedSymbolsAsync(
        ISymbol symbol,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var queryCache = context.ToolExecutionServices.QueryCache;
        var cacheKey = new ReferenceDiscoveryCacheKey(context.CurrentSolution, symbol, documents);
        var workspaceId = context.WorkspaceIdentity.WorkspaceId;
        if (queryCache.TryGet<ReferenceDiscoveryCacheEntry>(workspaceId, cacheKey, out var cachedEntry))
        {
            return cachedEntry.ReferencedSymbols;
        }

        var documentSet = documents.ToImmutableHashSet();
        IEnumerable<ReferencedSymbol> discoveredReferences;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.DiscoveryPhase))
        {
            discoveredReferences = await SymbolFinder.FindReferencesAsync(
                symbol,
                context.CurrentSolution,
                documentSet,
                cancellationToken);
        }

        var referencedSymbols = discoveredReferences.ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();

        var cacheEntry = new ReferenceDiscoveryCacheEntry(referencedSymbols);
        queryCache.Store(workspaceId, cacheKey, cacheEntry, cacheEntry.Size);

        return referencedSymbols;
    }

    private static async ValueTask<bool> IsWriteReferenceAsync(Document? document, Location location, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return false;
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        if (syntaxRoot is null)
        {
            return false;
        }

        var node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AssignmentExpressionSyntax assignmentExpressionSyntax
                    when assignmentExpressionSyntax.Left.Span.Contains(location.SourceSpan):
                    return true;

                case PrefixUnaryExpressionSyntax prefixUnaryExpressionSyntax
                    when prefixUnaryExpressionSyntax.IsKind(SyntaxKind.PreIncrementExpression)
                        || prefixUnaryExpressionSyntax.IsKind(SyntaxKind.PreDecrementExpression):
                    return true;

                case PostfixUnaryExpressionSyntax postfixUnaryExpressionSyntax
                    when postfixUnaryExpressionSyntax.IsKind(SyntaxKind.PostIncrementExpression)
                        || postfixUnaryExpressionSyntax.IsKind(SyntaxKind.PostDecrementExpression):
                    return true;

                case ArgumentSyntax argumentSyntax
                    when argumentSyntax.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)
                        || argumentSyntax.RefKindKeyword.IsKind(SyntaxKind.RefKeyword):
                    return true;
            }
        }

        return false;
    }

    private readonly record struct PendingReference
    {
        public required Location Location { get; init; }

        public required ResolvedLocation ResolvedLocation { get; init; }

        public required ISymbol DefinitionSymbol { get; init; }

        public Document? Document { get; init; }

        public bool IsDefinition { get; init; }
    }
}
