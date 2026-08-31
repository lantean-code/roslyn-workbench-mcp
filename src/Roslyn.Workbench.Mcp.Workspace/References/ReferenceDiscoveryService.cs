using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.References.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.References;

/// <summary>
/// Finds and caches symbol definitions and reference occurrences within a selected document scope.
/// </summary>
internal sealed class ReferenceDiscoveryService : IReferenceDiscoveryService
{
    private const string _operationName = "find-references";

    private const string _cacheComponentIdentity = "reference-discovery";

    private readonly IWorkspaceQueryCacheScopeFactory _queryCacheScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceDiscoveryService"/> class.
    /// </summary>
    /// <param name="queryCacheScopeFactory">The factory used to create the required query cache scope.</param>
    public ReferenceDiscoveryService(IWorkspaceQueryCacheScopeFactory queryCacheScopeFactory)
    {
        _queryCacheScopeFactory = queryCacheScopeFactory;
    }

    /// <summary>
    /// Finds reference groups for a symbol and filters them to selected documents.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="solution">The solution to search.</param>
    /// <param name="symbol">The symbol represented by the reference-discovery cache key.</param>
    /// <param name="documents">The documents included in the selected scope or cache identity.</param>
    /// <param name="includeDefinitions">Whether symbol definitions are included with reference occurrences.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the matching definition and reference occurrences.</returns>
    public async ValueTask<IReadOnlyList<ReferenceOccurrence>> FindReferencesAsync(
        Guid workspaceId,
        Solution solution,
        ISymbol symbol,
        IReadOnlyList<Document> documents,
        bool includeDefinitions,
        CancellationToken cancellationToken)
    {
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
        Guid workspaceId,
        Solution solution,
        ISymbol symbol,
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken)
    {
        var cacheScope = _queryCacheScopeFactory.CreateScope(
            workspaceId,
            solution,
            _cacheComponentIdentity);

        var cacheKey = new ReferenceDiscoveryCacheKey(symbol, documents);
        var cacheEntry = await cacheScope.GetOrCreateAsync(
            cacheKey,
            async factoryCancellationToken =>
            {
                var documentSet = documents.ToImmutableHashSet();
                IEnumerable<ReferencedSymbol> discoveredReferences;
                using (WorkbenchPerformanceEventSource.Log.StartPhase(_operationName, WorkbenchPerformanceEventSource.DiscoveryPhase))
                {
                    discoveredReferences = await SymbolFinder.FindReferencesAsync(
                        symbol,
                        solution,
                        documentSet,
                        factoryCancellationToken);
                }

                var referencedSymbols = discoveredReferences.ToImmutableArray();
                factoryCancellationToken.ThrowIfCancellationRequested();
                return new ReferenceDiscoveryCacheEntry(referencedSymbols);
            },
            static value => value.Size,
            static _ => true,
            cancellationToken);

        if (cacheEntry is null)
        {
            throw new InvalidOperationException(
                "The reference-discovery cache factory returned an unexpected null value.");
        }

        return cacheEntry.ReferencedSymbols;
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
