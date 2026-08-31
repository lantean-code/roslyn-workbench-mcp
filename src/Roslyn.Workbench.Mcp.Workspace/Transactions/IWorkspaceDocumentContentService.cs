namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Serializes Roslyn document text and calculates identities used by mutation preconditions.
/// </summary>
internal interface IWorkspaceDocumentContentService
{
    /// <summary>
    /// Serializes a document while preserving encoding and calculates its content hashes.
    /// </summary>
    /// <param name="document">The document whose serialized content is required.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace document content.</returns>
    ValueTask<WorkspaceDocumentContent> CreateAsync(
        Document document,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether two document-content snapshots have equivalent text and encoding.
    /// </summary>
    /// <param name="expected">The expected document content to compare with the actual content.</param>
    /// <param name="candidate">The candidate document content to compare.</param>
    /// <returns><see langword="true"/> when equivalent content; otherwise, <see langword="false"/>.</returns>
    bool HasEquivalentContent(
        WorkspaceDocumentContent expected,
        WorkspaceDocumentContent candidate);
}
