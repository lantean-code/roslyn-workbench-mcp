namespace Roslyn.Workbench.Mcp.Workspace.References;

/// <summary>
/// Discovers unique source references within a selected set of Roslyn documents.
/// </summary>
public interface IReferenceDiscoveryService
{
    /// <summary>
    /// Finds source references for a symbol, restricts them to the selected documents and removes duplicate occurrences within each document.
    /// </summary>
    /// <param name="workspaceId">The stable workspace identifier used to partition cached discovery results.</param>
    /// <param name="solution">The immutable solution snapshot to search.</param>
    /// <param name="symbol">The symbol whose references should be discovered.</param>
    /// <param name="documents">The documents that define the search scope.</param>
    /// <param name="includeDefinitions">A value indicating whether related definitions should be included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique source occurrences within the selected documents.</returns>
    ValueTask<IReadOnlyList<ReferenceOccurrence>> FindReferencesAsync(
        string workspaceId,
        Solution solution,
        ISymbol symbol,
        IReadOnlyList<Document> documents,
        bool includeDefinitions,
        CancellationToken cancellationToken);
}
