namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies one changed document by project, path, change kind, content, and encoding.
/// </summary>
internal sealed record WorkspaceMutationDocumentIdentity
{
    /// <summary>
    /// Gets the Roslyn project containing the changed document.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the normalized physical path of the changed document.
    /// </summary>
    public required FileSystemPathKey DocumentPath { get; init; }

    /// <summary>
    /// Gets how the candidate changes the document.
    /// </summary>
    public required WorkspaceMutationDocumentChangeKind ChangeKind { get; init; }

    /// <summary>
    /// Gets the hash of normalized Roslyn text content after the change.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Gets the hash of exact serialized bytes after the change.
    /// </summary>
    public required string SerializedBytesHash { get; init; }

    /// <summary>
    /// Gets the web name of the effective text encoding after the change.
    /// </summary>
    public required string EncodingName { get; init; }
}
