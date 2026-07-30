using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.References.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.References;

internal sealed class ReferenceDiscoveryService : IReferenceDiscoveryService
{
    private const string _operationName = "find-references";

    private readonly IQueryCache _queryCache;

    public ReferenceDiscoveryService(IQueryCache queryCache)
    {
        _queryCache = queryCache;
    }

    public async ValueTask<IReadOnlyList<ReferenceOccurrence>> FindReferencesAsync(
        string workspaceId,
        Solution solution,
        ISymbol symbol,
        IReadOnlyList<Document> documents,
        bool includeDefinitions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(documents);
        cancellationToken.ThrowIfCancellationRequested();

        var referencedSymbols = await GetReferencedSymbolsAsync(
            workspaceId,
            solution,
            symbol,
            documents,
            cancellationToken);

        var selectedDocumentIds = documents
            .Select(static document => document.Id)
            .ToImmutableHashSet();

        var occurrences = new List<ReferenceOccurrence>();
        var occurrenceIdentities = new HashSet<ReferenceOccurrenceIdentity>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_operationName, WorkbenchPerformanceEventSource.CandidateProjectionPhase))
        {
            foreach (var referencedSymbol in referencedSymbols)
            {
                if (includeDefinitions)
                {
                    AddDefinitionOccurrences(
                        referencedSymbol,
                        solution,
                        selectedDocumentIds,
                        occurrences,
                        occurrenceIdentities);
                }

                AddReferenceOccurrences(
                    referencedSymbol,
                    selectedDocumentIds,
                    occurrences,
                    occurrenceIdentities);
            }
        }

        return occurrences;
    }

    private async Task<ImmutableArray<ReferencedSymbol>> GetReferencedSymbolsAsync(
        string workspaceId,
        Solution solution,
        ISymbol symbol,
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken)
    {
        var cacheKey = new ReferenceDiscoveryCacheKey(solution, symbol, documents);
        if (_queryCache.TryGet<ReferenceDiscoveryCacheEntry>(workspaceId, cacheKey, out var cachedEntry))
        {
            return cachedEntry.ReferencedSymbols;
        }

        var documentSet = documents.ToImmutableHashSet();
        IEnumerable<ReferencedSymbol> discoveredReferences;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_operationName, WorkbenchPerformanceEventSource.DiscoveryPhase))
        {
            discoveredReferences = await SymbolFinder.FindReferencesAsync(
                symbol,
                solution,
                documentSet,
                cancellationToken);
        }

        var referencedSymbols = discoveredReferences.ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();

        var cacheEntry = new ReferenceDiscoveryCacheEntry(referencedSymbols);
        _queryCache.Store(workspaceId, cacheKey, cacheEntry, cacheEntry.Size);

        return referencedSymbols;
    }

    private static void AddDefinitionOccurrences(
        ReferencedSymbol referencedSymbol,
        Solution solution,
        ImmutableHashSet<DocumentId> selectedDocumentIds,
        List<ReferenceOccurrence> occurrences,
        HashSet<ReferenceOccurrenceIdentity> occurrenceIdentities)
    {
        foreach (var definitionLocation in referencedSymbol.Definition.Locations)
        {
            if (!definitionLocation.IsInSource
                || GetSelectedDocument(definitionLocation, solution, selectedDocumentIds) is not { } definitionDocument)
            {
                continue;
            }

            var identity = new ReferenceOccurrenceIdentity
            {
                DocumentId = definitionDocument.Id,
                Span = definitionLocation.SourceSpan,
                IsDefinition = true,
                DefinitionId = referencedSymbol.Definition.GetDocumentationCommentId(),
            };

            if (!occurrenceIdentities.Add(identity))
            {
                continue;
            }

            occurrences.Add(new ReferenceOccurrence
            {
                Location = definitionLocation,
                Document = definitionDocument,
                Definition = referencedSymbol.Definition,
                IsDefinition = true,
            });
        }
    }

    private static void AddReferenceOccurrences(
        ReferencedSymbol referencedSymbol,
        ImmutableHashSet<DocumentId> selectedDocumentIds,
        List<ReferenceOccurrence> occurrences,
        HashSet<ReferenceOccurrenceIdentity> occurrenceIdentities)
    {
        foreach (var reference in referencedSymbol.Locations)
        {
            if (!reference.Location.IsInSource
                || !selectedDocumentIds.Contains(reference.Document.Id))
            {
                continue;
            }

            var identity = new ReferenceOccurrenceIdentity
            {
                DocumentId = reference.Document.Id,
                Span = reference.Location.SourceSpan,
                IsDefinition = false,
                DefinitionId = null,
            };

            if (!occurrenceIdentities.Add(identity))
            {
                continue;
            }

            occurrences.Add(new ReferenceOccurrence
            {
                Location = reference.Location,
                Document = reference.Document,
                Definition = referencedSymbol.Definition,
                IsDefinition = false,
            });
        }
    }

    private static Document? GetSelectedDocument(
        Location location,
        Solution solution,
        ImmutableHashSet<DocumentId> selectedDocumentIds)
    {
        if (location.SourceTree is null)
        {
            return null;
        }

        var document = solution.GetDocument(location.SourceTree);
        return document is not null && selectedDocumentIds.Contains(document.Id)
            ? document
            : null;
    }
}
